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
    /// <summary>Events for the last completed tick (or initial spawns). Read before the next Step.</summary>
    public IReadOnlyList<SimulationEvent> Events { get; }

    public Simulation(SimulationSettings? settings = null, ShipInitialState initialShip = default,
        int randomSeed = 42, IEnumerable<Vector3>? initialTargets = null,
        ShipInitialState? enemyInitial = null, IEnumerable<ShipInitialState>? encounterThreeEnemies = null,
        bool spawnEnemy = true)
    {
        Settings = settings ?? new SimulationSettings();
        Settings.Validate();
        Events = _events.AsReadOnly();
        ValidateInitial(initialShip);
        var encounters = new[]
        {
            new EncounterState(1, "Encounter 1", Settings.TargetCount),
            new EncounterState(2, "Encounter 2", Settings.EncounterTwoTargetCount),
            new EncounterState(3, "Encounter 3", Settings.EncounterThreeTargetCount)
        };
        World = new WorldState(CreateShip(initialShip), encounters);
        World.WarpDrive.RemainingSeconds = Settings.WarpChargeSeconds;
        var targets = new TargetSystem(Settings, randomSeed);
        targets.Initialize(encounters[0], initialShip.Position, _events, initialTargets);
        targets.Initialize(encounters[1], Vector3.Zero, _events);
        targets.Initialize(encounters[2], Vector3.Zero, _events);
        if (spawnEnemy)
        {
            ShipInitialState enemyStart = enemyInitial ?? new ShipInitialState(
                Position: new Vector3(0, 0, -1_000), YawRadians: MathF.PI);
            AddEnemy(encounters[1], 1, enemyStart, randomSeed + 10_007);

            var thirdStarts = encounterThreeEnemies?.ToArray() ??
            [
                new ShipInitialState(Position: new Vector3(-480, 0, -950), YawRadians: -0.47f),
                new ShipInitialState(Position: new Vector3(480, 0, -950), YawRadians: 0.47f)
            ];
            if (thirdStarts.Length != 2)
                throw new ArgumentException("Encounter 3 requires exactly two enemies.", nameof(encounterThreeEnemies));
            for (int index = 0; index < thirdStarts.Length; index++)
                AddEnemy(encounters[2], index + 2, thirdStarts[index], randomSeed + 10_008 + index);
        }
    }

    public void Step(ShipCommand command, NavigationCommand navigation = default,
        PowerAllocationCommand powerAllocation = default)
    {
        _events.Clear();
        if (World.GameState == GameState.GameOver) return;

        if (PowerDistributionSystem.TryApply(World.Ship.Power, powerAllocation))
            _events.Add(new PowerAllocationChanged(World.Ship.Power.PropulsionAllocation,
                World.Ship.Power.WeaponsAllocation, World.Ship.Power.ShieldsAllocation));
        ShieldSystem.Recharge(World.Ship.Shield, World.Ship.Power.ShieldsPowerFactor, Settings.Shield);
        LanceSystem.Charge(World.Lance, Settings, World.Ship.Power.WeaponsPowerFactor);
        EnemyShipState[] enemies = World.CurrentEnemies.ToArray();
        var enemyCommands = new Dictionary<int, ShipCommand>(enemies.Length);
        foreach (EnemyShipState enemy in enemies)
        {
            EnemyAiController? ai = World.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
            enemyCommands[enemy.EnemyId] = ai?.Tick(enemy, World.Ship, World.Lance,
                Settings.LanceRangeMeters, World.GameState) ?? default;
            if (ai is not null) PowerDistributionSystem.ApplyProfile(enemy.Ship.Power, ai.CurrentPowerProfile);
            ShieldSystem.Recharge(enemy.Ship.Shield, enemy.Ship.Power.ShieldsPowerFactor, Settings.Shield);
            LanceSystem.Charge(enemy.Lance, Settings, enemy.Ship.Power.WeaponsPowerFactor);
        }
        ShipPhysics.Step(World.Ship, command, Settings);
        foreach (EnemyShipState enemy in enemies)
            ShipPhysics.Step(enemy.Ship, enemyCommands[enemy.EnemyId], Settings);
        LanceSystem.FirePlayer(World, command.FireLance, Settings, _events);
        foreach (EnemyShipState enemy in enemies.Where(enemy => enemy.IsDestroyed))
            World.CurrentEncounter.GetEnemyAi(enemy.EnemyId)?.MarkDestroyed();
        foreach (EnemyShipState enemy in enemies.Where(enemy => !enemy.IsDestroyed))
        {
            LanceSystem.FireEnemy(World, enemy, enemyCommands[enemy.EnemyId].FireLance, Settings, _events);
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

    private ShipState CreateShip(ShipInitialState initial) => new(Settings.ShipMassKg, Settings.YawMomentOfInertia,
        PowerDistributionSystem.Create(Settings.Power), ShieldSystem.Create(Settings.Shield))
    {
        Position = initial.Position,
        Velocity = initial.Velocity,
        Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, initial.YawRadians),
        AngularVelocity = Vector3.UnitY * initial.YawRateRadiansPerSecond
    };

    private void AddEnemy(EncounterState encounter, int enemyId, ShipInitialState initial, int seed)
    {
        ValidateInitial(initial);
        encounter.AddEnemy(new EnemyShipState(enemyId, CreateShip(initial)),
            new EnemyAiController(Settings.EnemyAi, Settings.Power, seed));
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
