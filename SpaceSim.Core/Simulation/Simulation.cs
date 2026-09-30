using System.Numerics;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Combat;
using SpaceSim.Core.AI;
using SpaceSim.Core.Power;

namespace SpaceSim.Core.Simulation;

/// <summary>Authoritative, engine-independent simulation, advanced exactly one fixed tick at a time.</summary>
public sealed class Simulation
{
    public SimulationSettings Settings { get; }
    public WorldState World { get; }
    private readonly List<SimulationEvent> _events = new();
    private readonly Random _damageRandom;
    /// <summary>Events for the last completed tick (or initial spawns). Read before the next Step.</summary>
    public IReadOnlyList<SimulationEvent> Events { get; }

    public Simulation(SimulationSettings? settings = null, ShipInitialState initialShip = default,
        int randomSeed = 42, IEnumerable<Vector3>? initialTargets = null,
        ShipInitialState? enemyInitial = null,
        bool spawnEnemy = true)
    {
        Settings = settings ?? new SimulationSettings();
        Settings.Validate();
        _damageRandom = new Random(randomSeed + 20_021);
        Events = _events.AsReadOnly();
        ValidateInitial(initialShip);
        var encounters = new[]
        {
            new EncounterState(1, "Encounter 1", Settings.TargetCount),
            new EncounterState(2, "Encounter 2", Settings.EncounterTwoTargetCount),
            new EncounterState(3, "Encounter 3", Settings.EncounterThreeTargetCount),
            new EncounterState(4, "Encounter 4", Settings.EncounterFourTargetCount)
        };
        World = new WorldState(CreateShip(initialShip), encounters);
        if (Settings.StartWarpReady)
        {
            World.WarpDrive.ChargedSeconds = Settings.WarpChargeSeconds;
            World.WarpDrive.ChargeFraction = 1f;
            World.WarpDrive.RemainingSeconds = 0;
            World.WarpDrive.IsReady = true;
        }
        else World.WarpDrive.RemainingSeconds = Settings.WarpChargeSeconds;
        var targets = new TargetSystem(Settings, randomSeed);
        targets.Initialize(encounters[0], initialShip.Position, _events, initialTargets);
        targets.Initialize(encounters[1], Vector3.Zero, _events);
        targets.Initialize(encounters[2], Vector3.Zero, _events);
        targets.Initialize(encounters[3], Vector3.Zero, _events);
        if (spawnEnemy)
        {
            AddEnemy(encounters[1], 1, CreatePatrolInitial(randomSeed + 10_007),
                EnemyDifficulty.Easy, randomSeed + 10_007);
            ShipInitialState mediumStart = enemyInitial ?? CreatePatrolInitial(randomSeed + 10_008);
            AddEnemy(encounters[2], 2, mediumStart, EnemyDifficulty.Medium, randomSeed + 10_008);
            AddEnemy(encounters[3], 3, CreatePatrolInitial(randomSeed + 10_009), EnemyDifficulty.Hard, randomSeed + 10_009);
        }
    }

    public void Step(ShipCommand command, NavigationCommand navigation = default,
        ReactorCommand reactorCommand = default)
    {
        _events.Clear();
        if (World.GameState == GameState.GameOver) return;

        if (reactorCommand.OperatingLevelPercent is float level)
            PowerDistributionSystem.SetReactorOperatingLevel(World.Ship.Reactor, level);
        if (reactorCommand.Allocation is PowerAllocation allocation)
            PowerDistributionSystem.SetPlayerAllocation(World.Ship.Power, allocation);

        PowerDistributionSystem.StepReactor(World.Ship.Reactor, Settings.Power);
        PowerDistributionSystem.ApplyPlayerDemand(World.Ship, World.Lance, command);
        ShieldSystem.Recharge(World.Ship.Shield, World.Ship.Power.ShieldsPowerFactor * World.Ship.Systems.ShieldsCondition, Settings.Shield);
        LanceSystem.Charge(World.Lance, Settings, World.Ship.Power.WeaponsPowerFactor * World.Ship.Systems.WeaponsCondition);
        LanceAimSystem.Step(World.LanceAim, command, Settings);
        EnemyShipState[] enemies = World.CurrentEnemies.ToArray();
        var enemyCommands = new Dictionary<int, ShipCommand>(enemies.Length);
        foreach (EnemyShipState enemy in enemies)
        {
            EnemyAiController? ai = World.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
            enemyCommands[enemy.EnemyId] = ai?.Tick(enemy, World.Ship, World.Lance,
                Settings.LanceRangeMeters, World.GameState) ?? default;
            if (ai is not null)
                PowerDistributionSystem.SetReactorOperatingLevel(enemy.Ship.Reactor, ai.DesiredReactorOperatingLevelPercent);
            PowerDistributionSystem.StepReactor(enemy.Ship.Reactor, Settings.Power);
            if (ai?.IsPlayerDetected == true) PowerDistributionSystem.ApplyEnemyCombatDemand(enemy.Ship);
            else if (ai is not null) PowerDistributionSystem.ApplyEnemyPatrolDemand(enemy.Ship, Settings.EnemyAi.PatrolPropulsionDraw);
            ShieldSystem.Recharge(enemy.Ship.Shield, enemy.Ship.Power.ShieldsPowerFactor * enemy.Ship.Systems.ShieldsCondition, Settings.Shield);
            LanceSystem.Charge(enemy.Lance, Settings, enemy.Ship.Power.WeaponsPowerFactor * enemy.Ship.Systems.WeaponsCondition);
        }
        ShipPhysics.Step(World.Ship, command, Settings);
        foreach (EnemyShipState enemy in enemies)
            ShipPhysics.Step(enemy.Ship, enemyCommands[enemy.EnemyId], Settings);
        if (ShipCollisionSystem.Resolve(World, enemies, Settings, _events))
        {
            foreach (EnemyShipState enemy in enemies.Where(enemy => enemy.IsDestroyed))
                World.CurrentEncounter.GetEnemyAi(enemy.EnemyId)?.MarkDestroyed();
            World.Tick++;
            return;
        }
        LanceSystem.FirePlayer(World, command.FireLance, Settings, _damageRandom, _events);
        foreach (EnemyShipState enemy in enemies.Where(enemy => enemy.IsDestroyed))
        {
            World.CurrentEncounter.GetEnemyAi(enemy.EnemyId)?.MarkDestroyed();
            EnemyExplosionSystem.Apply(World, enemy, Settings, _damageRandom, _events);
        }
        if (World.GameState == GameState.GameOver)
        {
            World.Tick++;
            return;
        }
        foreach (EnemyShipState enemy in enemies.Where(enemy => !enemy.IsDestroyed))
        {
            LanceSystem.FireEnemy(World, enemy, enemyCommands[enemy.EnemyId].FireLance, Settings, _damageRandom, _events);
            if (World.GameState == GameState.GameOver) break;
        }
        if (World.GameState == GameState.GameOver)
        {
            World.Tick++;
            return;
        }
        WarpDriveSystem.Step(World, navigation, Settings, _events);
        World.Tick++;
    }

    private ShipState CreateShip(ShipInitialState initial, float? reactorOperatingLevelPercent = null) => new(Settings.ShipMassKg, Settings.YawMomentOfInertia,
        PowerDistributionSystem.CreateReactor(Settings.Power, reactorOperatingLevelPercent), PowerDistributionSystem.Create(Settings.Power), ShieldSystem.Create(Settings.Shield),
        HullSystem.Create(Settings.Hull), new SubsystemState())
    {
        Position = initial.Position,
        Velocity = initial.Velocity,
        Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, initial.YawRadians),
        AngularVelocity = Vector3.UnitY * initial.YawRateRadiansPerSecond
    };

    private void AddEnemy(EncounterState encounter, int enemyId, ShipInitialState initial, EnemyDifficulty difficulty, int seed)
    {
        ValidateInitial(initial);
        EnemyDifficultyProfile profile = EnemyDifficultyProfiles.Create(difficulty, Settings.EnemyAi);
        profile.Ai.Validate();
        ShipState ship = CreateShip(initial, profile.Ai.PatrolReactorOperatingLevelPercent);
        ship.Shield.CurrentShield = 0f;
        PowerDistributionSystem.ApplyEnemyPatrolDemand(ship, profile.Ai.PatrolPropulsionDraw);
        encounter.AddEnemy(new EnemyShipState(enemyId, ship, difficulty),
            new EnemyAiController(profile.Ai, difficulty, Settings.ReverseThrustNewtons / Settings.ShipMassKg));
    }

    private ShipInitialState CreatePatrolInitial(int seed)
    {
        var random = new Random(seed);
        float spawnAngle = (float)(random.NextDouble() * MathF.Tau);
        float distance = Settings.EnemyAi.PatrolSpawnMinimumDistanceMeters +
            (float)random.NextDouble() * (Settings.EnemyAi.PatrolSpawnMaximumDistanceMeters - Settings.EnemyAi.PatrolSpawnMinimumDistanceMeters);
        float course = (float)(random.NextDouble() * MathF.Tau);
        Vector3 forward = new(MathF.Sin(course), 0f, -MathF.Cos(course));
        return new ShipInitialState(new Vector3(MathF.Sin(spawnAngle) * distance, 0f, -MathF.Cos(spawnAngle) * distance),
            forward * Settings.EnemyAi.PatrolCruiseSpeedMetersPerSecond, -course);
    }

    private static void ValidateInitial(ShipInitialState initial)
    {
        static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        if (!Finite(initial.Position) || !Finite(initial.Velocity) ||
            !float.IsFinite(initial.YawRadians) || !float.IsFinite(initial.YawRateRadiansPerSecond) ||
            initial.Position.Y != 0f || initial.Velocity.Y != 0f)
            throw new ArgumentException("Planar flight requires finite initial state in the X/Z plane.", nameof(initial));
    }
}
