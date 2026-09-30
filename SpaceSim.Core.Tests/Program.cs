using System.Numerics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Power;
using SpaceSim.Stations;
using SpaceSim.Stations.Armarium;

var tests = new (string Name, Action Run)[]
{
    ("No thrust preserves velocity and integrates position", () =>
    {
        var sim = New(new ShipInitialState(Velocity: new Vector3(12, 0, -8)));
        Step(sim, 600);
        NearVector(sim.World.Ship.Velocity, new Vector3(12, 0, -8));
        NearVector(sim.World.Ship.Position, new Vector3(120, 0, -80), 0.002f);
    }),
    ("Main engine accelerates along the rotated ship nose", () =>
    {
        var sim = New(new ShipInitialState(YawRadians: MathF.PI / 2));
        Step(sim, 120, new ShipCommand(MainThrust: true));
        NearVector(sim.World.Ship.Velocity, new Vector3(-24, 0, 0), 0.001f);
    }),
    ("Velocity persists after releasing thrust", () =>
    {
        var sim = New();
        Step(sim, 120, new ShipCommand(MainThrust: true));
        var velocity = sim.World.Ship.Velocity;
        Step(sim, 600);
        NearVector(sim.World.Ship.Velocity, velocity);
    }),
    ("Rotation has inertia after releasing torque", () =>
    {
        var sim = New();
        Step(sim, 60, new ShipCommand(YawLeft: true));
        float rate = sim.World.Ship.AngularVelocity.Y;
        var nose = sim.World.Ship.Forward;
        Step(sim, 60);
        Near(rate, 0.6f);
        Near(sim.World.Ship.AngularVelocity.Y, rate);
        Check(Vector3.Distance(nose, sim.World.Ship.Forward) > 0.5f, "Orientation must keep changing.");
    }),
    ("Opposing torque brakes and reverses angular velocity", () =>
    {
        var sim = New();
        Step(sim, 60, new ShipCommand(YawLeft: true));
        Step(sim, 30, new ShipCommand(YawRight: true));
        Near(sim.World.Ship.AngularVelocity.Y, 0.3f);
        Step(sim, 60, new ShipCommand(YawRight: true));
        Near(sim.World.Ship.AngularVelocity.Y, -0.3f);
    }),
    ("Reduced yaw intensity applies proportional normal thruster torque", () =>
    {
        var bridge = New();
        var armarium = New();
        Step(bridge, 60, new ShipCommand(YawLeft: true));
        Step(armarium, 60, new ShipCommand(YawLeft: true, YawIntensity: 0.5f));
        Near(armarium.World.Ship.AngularVelocity.Y, bridge.World.Ship.AngularVelocity.Y * 0.5f);
    }),
    ("Reverse engine thrust is relative to orientation", () =>
    {
        var sim = New();
        Step(sim, 60, new ShipCommand(ReverseThrust: true));
        NearVector(sim.World.Ship.Velocity, new Vector3(0, 0, 6));
    }),
    ("Planar rule and normalized 3D orientation survive long flight", () =>
    {
        var sim = New();
        Step(sim, 36000, new ShipCommand(MainThrust: true, YawLeft: true));
        Near(sim.World.Ship.Position.Y, 0);
        Near(sim.World.Ship.Velocity.Y, 0);
        Near(sim.World.Ship.Rotation.Length(), 1);
    }),
    ("Full propulsion reaches the configured speed limit", () =>
    {
        var sim = New();
        Step(sim, 3600, new ShipCommand(MainThrust: true));
        Near(sim.World.Ship.Velocity.Length(), 500f, 0.05f);
    }),
    ("Lance needs exactly three seconds to charge", () =>
    {
        var sim = NewWeapons();
        Check(!sim.World.Lance.IsReady, "Lance starts empty.");
        Step(sim, 179);
        Check(!sim.World.Lance.IsReady, "Lance must not charge early.");
        Step(sim, 1);
        Check(sim.World.Lance.IsReady, "Lance must be ready after 180 ticks.");
    }),
    ("Early fire is ignored and never queued", () =>
    {
        var sim = NewWeapons();
        sim.Step(new ShipCommand(FireLance: true));
        Check(!sim.Events.OfType<WeaponFired>().Any(), "Early shot must be rejected.");
        Step(sim, 179);
        Check(sim.World.Lance.IsReady, "Rejected request must not fire later.");
        Check(!sim.Events.OfType<WeaponFired>().Any(), "No delayed shot.");
    }),
    ("Shot resets charge, emits once, and recharges", () =>
    {
        var sim = NewWeapons();
        Step(sim, 180);
        sim.Step(new ShipCommand(FireLance: true));
        Near(sim.World.Lance.ChargeFraction, 0);
        Check(sim.Events.OfType<WeaponFired>().Count() == 1, "Exactly one shot event expected.");
        Step(sim, 1);
        Check(sim.Events.Count == 0, "Old events must not repeat.");
        Step(sim, 178);
        Check(!sim.World.Lance.IsReady, "Cooldown must last full duration.");
        Step(sim, 1);
        Check(sim.World.Lance.IsReady, "Must recharge automatically.");
    }),
    ("Ray hits target immediately and never respawns it", () =>
    {
        var sim = WithTargets(new Vector3(0, 0, -300));
        int id = sim.World.Targets[0].Id;
        Fire(sim);
        Check(sim.World.HitCount == 1, "Expected immediate hit.");
        Check(sim.World.Targets.Count == 0, "Destroyed target must remain removed.");
        Check(sim.Events.OfType<TargetHit>().Single().TargetId == id, "Missing hit event.");
        Check(!sim.Events.OfType<TargetSpawned>().Any(), "No replacement may spawn.");
        NearVector(sim.Events.OfType<WeaponFired>().Single().End, new Vector3(0, 0, -286));
        Step(sim, 1200);
        Check(sim.World.Targets.Count == 0, "Cleared encounter must stay empty.");
    }),
    ("Off-axis target is missed", () =>
    {
        var sim = WithTargets(new Vector3(30, 0, -300));
        Fire(sim);
        Check(sim.World.HitCount == 0, "Off-axis shot must miss.");
        Near(sim.Events.OfType<WeaponFired>().Single().End.Length(), sim.Settings.LanceRangeMeters);
    }),
    ("Nearest target blocks targets behind it", () =>
    {
        var sim = WithTargets(new Vector3(0, 0, -500), new Vector3(0, 0, -200));
        int nearId = sim.World.Targets[1].Id;
        Fire(sim);
        Check(sim.World.HitCount == 1, "Only one target may be hit.");
        Check(sim.Events.OfType<TargetHit>().Single().TargetId == nearId, "Nearest target must win.");
    }),
    ("Targets behind the ship or beyond range are missed", () =>
    {
        var sim = WithTargets(new Vector3(0, 0, 300), new Vector3(0, 0, -1800));
        Fire(sim);
        Check(sim.World.HitCount == 0, "Ray must be forward and range limited.");
    }),
    ("Lance follows the ship's 3D orientation", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 1, Power = WeaponsOnlyPower() },
            new ShipInitialState(YawRadians: MathF.PI / 2),
            initialTargets: new[] { new Vector3(-300, 0, 0) });
        Fire(sim);
        Check(sim.World.HitCount == 1, "Rotated weapon ray must hit.");
    }),
    ("Lance turret is limited and does not apply ship yaw", () =>
    {
        var sim = NewWeapons();
        Step(sim, 30, new ShipCommand(AimLanceRight: true));
        Near(sim.World.LanceAim.YawOffsetDegrees, 5f);
        Near(sim.World.Ship.AngularVelocity.Y, 0f);
        Check(sim.World.LanceDirection.X > 0f && sim.World.LanceDirection.Z < 0f,
            "Starboard turret aim must rotate only the lance ray to starboard.");
        Step(sim, 60, new ShipCommand(AimLanceLeft: true));
        Near(sim.World.LanceAim.YawOffsetDegrees, -5f);
        Near(sim.World.Ship.AngularVelocity.Y, 0f);
    }),
    ("Turret deflection changes the player lance hit ray", () =>
    {
        float radians = 5f * MathF.PI / 180f;
        var target = new Vector3(MathF.Sin(radians) * 300f, 0f, -MathF.Cos(radians) * 300f);
        var sim = WithTargets(target);
        Step(sim, 30, new ShipCommand(AimLanceRight: true));
        Step(sim, 150);
        sim.Step(new ShipCommand(FireLance: true));
        Check(sim.World.HitCount == 1, "A target on the mounted lance axis must be hit.");
    }),
    ("Spawn positions are safe and repeatable with a fixed seed", () =>
    {
        var sim = new Simulation();
        var other = new Simulation();
        Check(sim.World.Targets.Count == sim.Settings.TargetCount, "Population must be full.");
        for (int i = 0; i < sim.World.Targets.Count; i++)
        {
            var target = sim.World.Targets[i];
            float distance = target.Position.Length();
            Check(distance >= sim.Settings.SpawnMinDistanceMeters && distance <= sim.Settings.SpawnMaxDistanceMeters,
                "Target must be in spawn annulus.");
            NearVector(target.Position, other.World.Targets[i].Position);
        }
    }),
    ("Targets remain static even when flying far away", () =>
    {
        var sim = new Simulation(initialShip: new ShipInitialState(Velocity: new Vector3(1000, 0, 0)));
        var original = sim.World.Targets.ToArray();
        Step(sim, 1);
        Check(sim.World.Targets.SequenceEqual(original), "Targets must not follow the ship.");
        Step(sim, 600);
        Check(sim.World.Targets.Count == sim.Settings.TargetCount, "Population must remain bounded.");
        Check(sim.World.HitCount == 0, "Travel must not score hits.");
        Check(sim.World.Targets.SequenceEqual(original), "Targets must not be recycled or relocated.");
    }),
    ("Invalid physical settings are rejected", () =>
    {
        bool rejected = false;
        try { _ = new Simulation(new SimulationSettings { ShipMassKg = 0 }); }
        catch (ArgumentOutOfRangeException) { rejected = true; }
        Check(rejected, "Zero mass must not enter the simulation.");
    }),
    ("Armarium target bearing is normalized relative to the ship nose", () =>
    {
        Vector3 ship = Vector3.Zero;
        Near(ArmariumStateBuilder.CalculateTargetBearingDegrees(ship, -Vector3.UnitZ, new Vector3(0, 0, -100)), 0f);
        Check(ArmariumStateBuilder.CalculateTargetBearingDegrees(ship, -Vector3.UnitZ, new Vector3(-100, 0, -100)) < 0f,
            "A target to port must have a negative bearing.");
        Check(ArmariumStateBuilder.CalculateTargetBearingDegrees(ship, -Vector3.UnitZ, new Vector3(100, 0, -100)) > 0f,
            "A target to starboard must have a positive bearing.");
        Near(ArmariumStateBuilder.CalculateTargetBearingDegrees(ship, Vector3.UnitX, new Vector3(0, 0, -100)), -90f);
        Near(ArmariumStateBuilder.CalculateTargetBearingDegrees(ship, -Vector3.UnitZ, new Vector3(0, 0, 100)), 180f);
    }),
    ("Armarium state is station-specific and tracks target and lance availability", () =>
    {
        var empty = NewWeapons();
        ArmariumState noTarget = ArmariumStateBuilder.Build(empty.World);
        Check(!noTarget.TargetAvailable && noTarget.TargetBearingDegrees == 0f && noTarget.LanceCharge == 0f &&
              noTarget.TargetDistanceMeters == 0f && noTarget.LanceTurretAngleDegrees == 0f,
            "An empty encounter must not expose a target or world state.");
        Step(empty, 180);
        Check(ArmariumStateBuilder.Build(empty.World).LanceReady,
            "Armarium must expose the ordinary lance readiness state.");
        Step(empty, 30, new ShipCommand(AimLanceRight: true));
        Near(ArmariumStateBuilder.Build(empty.World).LanceTurretAngleDegrees, 5f);

        var combat = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -900)));
        JumpToCombat(combat);
        ArmariumState target = ArmariumStateBuilder.Build(combat.World);
        Check(target.TargetAvailable && MathF.Abs(target.TargetBearingDegrees) < 0.001f &&
              MathF.Abs(target.TargetDistanceMeters - 900f) < 0.001f && target.SimulationTick == combat.World.Tick,
            "Armarium must receive its compact target map distance, bearing and lance state.");
    }),
    ("Armarium command buffer creates ordinary fire and turret intents", () =>
    {
        var buffer = new ArmariumCommandBuffer();
        var early = NewWeapons();
        buffer.RequestFire();
        early.Step(new ShipCommand(FireLance: buffer.ReadCommand().FireLance));
        Check(!early.Events.OfType<WeaponFired>().Any(), "Early Armarium fire must obey the ordinary lance readiness rule.");

        var sim = WithTargets(new Vector3(0, 0, -300));
        Step(sim, 180);
        buffer.RequestFire(); buffer.RequestFire();
        buffer.SetTurretDirection(-1);
        ArmariumCommand command = buffer.ReadCommand();
        sim.Step(new ShipCommand(AimLanceLeft: command.AimLanceLeft, AimLanceRight: command.AimLanceRight,
            FireLance: command.FireLance));
        Check(sim.Events.OfType<WeaponFired>().Count() == 1 && !buffer.ReadCommand().FireLance && command.AimLanceLeft,
            "Several network requests before one tick must produce one fire impulse.");
        buffer.Clear();
        Check(!buffer.ReadCommand().AimLanceLeft && !buffer.ReadCommand().AimLanceRight,
            "Clearing the station buffer must release held Armarium turret input.");
    }),
    ("Armarium turret input never changes the ship thrusters", () =>
    {
        var sim = NewWeapons();
        Step(sim, 60, new ShipCommand(AimLanceRight: true));
        Near(sim.World.Ship.AngularVelocity.Y, 0f);
        Near(sim.World.LanceAim.YawOffsetDegrees, 5f);
    }),
    ("Station server serves Armarium and relays hello state and fire", () =>
        StationServerSmokeAsync().GetAwaiter().GetResult()),
    ("Four encounters assign their configured content and enemy difficulty", () =>
    {
        var sim = new Simulation();
        Check(sim.World.CurrentEncounter.Id == 1, "Start in encounter 1.");
        Check(sim.World.Encounters.Count == 4, "There must be four destinations.");
        Check(sim.World.Encounters[0].Targets.Count == 10 && sim.World.Encounters[1].Targets.Count == 0 &&
              sim.World.Encounters[2].Targets.Count == 0 && sim.World.Encounters[3].Targets.Count == 0,
            "Targets belong only to encounter 1.");
        Check(sim.World.Encounters[0].Enemies.Count == 0 && sim.World.Encounters[1].Enemies.Count == 1 &&
              sim.World.Encounters[2].Enemies.Count == 1 && sim.World.Encounters[3].Enemies.Count == 1,
            "Wrong enemy populations.");
        Check(sim.World.Encounters[1].Enemies.Single().Difficulty == EnemyDifficulty.Easy &&
              sim.World.Encounters[2].Enemies.Single().Difficulty == EnemyDifficulty.Medium &&
              sim.World.Encounters[3].Enemies.Single().Difficulty == EnemyDifficulty.Hard,
            "Each jump point must use its configured enemy difficulty.");
        Check(sim.World.Encounters.SelectMany(e => e.Targets).Select(t => t.Id).Distinct().Count() == 10,
            "Target IDs must be unique across encounters.");
    }),
    ("Encounter 4 activates its Hard enemy through shared physics", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 0 });
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(4));
        var enemies = sim.World.CurrentEnemies.OrderBy(enemy => enemy.EnemyId).ToArray();
        Check(enemies.Length == 1 && enemies.Single().EnemyId == 3 && enemies.Single().Difficulty == EnemyDifficulty.Hard,
            "Encounter 4 must activate its Hard enemy.");
        Check(sim.World.CurrentEncounter.EnemyAi?.CurrentState == EnemyAiState.Acquire,
            "The enemy requires its own initial controller state.");
        sim.Step(default);
        Check(sim.World.CurrentEncounter.EnemyAi?.CurrentState == EnemyAiState.Approach,
            "The controller must progress from ACQUIRE.");
        Check(enemies.Single().Ship.Position.Y == 0 && enemies.Single().Ship.Velocity.Y == 0,
            "The enemy must remain inside the shared planar flight physics.");
    }),
    ("Warp starts empty and takes exactly ten seconds", () =>
    {
        var sim = New();
        Check(!sim.World.WarpDrive.IsReady, "Warp must start empty.");
        Near((float)sim.World.WarpDrive.RemainingSeconds, 10);
        Step(sim, 599);
        Check(!sim.World.WarpDrive.IsReady, "Warp must not be ready early.");
        Step(sim, 1);
        Check(sim.World.WarpDrive.IsReady, "Warp must be ready at tick 600.");
        Near(sim.World.WarpDrive.ChargeFraction, 1);
        Near((float)sim.World.WarpDrive.RemainingSeconds, 0);
    }),
    ("Early jump is rejected without being queued", () =>
    {
        var sim = New();
        sim.Step(default, new NavigationCommand(2));
        Check(sim.World.CurrentEncounter.Id == 1, "Uncharged warp must not jump.");
        Step(sim, 599);
        Check(sim.World.CurrentEncounter.Id == 1 && sim.World.WarpDrive.IsReady, "No delayed jump allowed.");
    }),
    ("Jump changes encounter, stops the ship, and consumes warp charge", () =>
    {
        var sim = NewWeapons(new ShipInitialState(Position: new Vector3(100, 0, 50), Velocity: new Vector3(10, 0, -3),
            YawRadians: 1, YawRateRadiansPerSecond: 0.4f));
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(2));
        Check(sim.World.CurrentEncounter.Id == 2 && sim.World.Targets.Count == 0 &&
              sim.World.CurrentEnemies.Count() == 0, "Wrong destination.");
        NearVector(sim.World.Ship.Position, Vector3.Zero);
        NearVector(sim.World.Ship.Velocity, Vector3.Zero);
        NearVector(sim.World.Ship.AngularVelocity, Vector3.Zero);
        NearVector(sim.World.Ship.Forward, -Vector3.UnitZ);
        Near(sim.World.WarpDrive.ChargeFraction, 0);
        Check(!sim.World.WarpDrive.IsReady, "Jump must consume charge.");
        Check(sim.Events.OfType<EncounterChanged>().Single() == new EncounterChanged(1, 2), "Wrong jump event.");
        Check(sim.World.Lance.IsReady, "Warp must not drain the independent lance.");
        Step(sim, 599);
        Check(!sim.World.WarpDrive.IsReady, "Recharge must last full ten seconds.");
        Step(sim, 1);
        Check(sim.World.WarpDrive.IsReady, "Warp must automatically recharge.");
    }),
    ("Unknown and current destinations do not consume warp charge", () =>
    {
        var sim = New();
        Step(sim, 600);
        foreach (int id in new[] { 1, -1, 999 })
        {
            sim.Step(default, new NavigationCommand(id));
            Check(sim.World.CurrentEncounter.Id == 1 && sim.World.WarpDrive.IsReady, "Invalid jump changed state.");
            Check(!sim.Events.OfType<EncounterChanged>().Any(), "Invalid jump emitted an event.");
        }
    }),
    ("Encounter progress and remaining targets survive a round trip", () =>
    {
        var sim = WithTargets(new Vector3(0, 0, -300), new Vector3(200, 0, 200));
        var survivor = sim.World.Targets[1];
        var secondTargets = sim.World.Encounters[1].Targets.ToArray();
        Fire(sim);
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(2));
        Check(sim.World.Targets.SequenceEqual(secondTargets), "Encounter 2 must retain its initial layout.");
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(1));
        Check(sim.World.Targets.Count == 1 && sim.World.Targets[0] == survivor, "Destroyed target respawned.");
        Check(sim.World.CurrentEncounter.HitCount == 1 && sim.World.HitCount == 1, "Progress must survive travel.");
        Check(!sim.Events.OfType<TargetSpawned>().Any(), "Revisiting must not spawn targets.");
    }),
    ("Warp charging does not freeze flight or lance charging", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 0 },
            new ShipInitialState(Velocity: new Vector3(12, 0, 0), YawRateRadiansPerSecond: 0.2f), spawnEnemy: false);
        Step(sim, 600);
        NearVector(sim.World.Ship.Position, new Vector3(120, 0, 0), 0.002f);
        Check(sim.World.Lance.IsReady && sim.World.WarpDrive.IsReady, "Both systems must charge while moving.");
        Near(sim.World.Ship.AngularVelocity.Y, 0.2f);
        Check(sim.World.Tick == 600, "World time must advance.");
    }),
    ("Each jump point starts its configured enemy difficulty in ACQUIRE", () =>
    {
        var sim = CombatSimulation();
        Check(sim.World.Encounters[0].Enemy is null, "Encounter 1 contains targets only.");
        Check(sim.World.Encounters[1].Enemy is { EnemyId: 1, Difficulty: EnemyDifficulty.Easy, IsDestroyed: false },
            "Encounter 2 needs one Easy enemy.");
        var encounter = sim.World.Encounters[2];
        Check(encounter.Enemy is { EnemyId: 2, Difficulty: EnemyDifficulty.Medium, IsDestroyed: false },
            "Encounter 3 needs one Medium enemy.");
        Check(sim.World.Encounters[3].Enemy is { EnemyId: 3, Difficulty: EnemyDifficulty.Hard, IsDestroyed: false },
            "Encounter 4 needs one Hard enemy.");
        Check(encounter.EnemyAi?.CurrentState == EnemyAiState.Acquire, "Enemy must start in ACQUIRE.");
        Check(encounter.Enemy!.Ship.MassKg == sim.World.Ship.MassKg &&
              encounter.Enemy.Ship.YawMomentOfInertia == sim.World.Ship.YawMomentOfInertia,
            "Player and enemy must use identical physical parameters.");
    }),
    ("ACQUIRE immediately transitions to APPROACH and AI only outputs ShipCommand", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0),
            YawRadians: MathF.PI / 2));
        JumpToCombat(sim);
        var enemyBefore = sim.World.CurrentEnemy!.Ship;
        var positionBefore = enemyBefore.Position;
        var velocityBefore = enemyBefore.Velocity;
        var angularBefore = enemyBefore.AngularVelocity.Y;
        sim.Step(default);
        var ai = sim.World.CurrentEncounter.EnemyAi!;
        Check(ai.CurrentState == EnemyAiState.Approach, "ACQUIRE must normally last one tick.");
        Check(ai.LastContext.DistanceToPlayer > 899 && ai.LastContext.DistanceToPlayer < 901,
            "AI context must contain exact player geometry.");
        Check(!ai.LastCommand.FireLance, "APPROACH must use the regular non-firing command fields.");
        Check((enemyBefore.Velocity - velocityBefore).Length() <= 12f / 60f + 0.0001f &&
              MathF.Abs(enemyBefore.AngularVelocity.Y - angularBefore) <= 0.6f / 60f + 0.0001f,
            "AI motion must remain bounded by the shared thruster physics.");
        NearVector(enemyBefore.Position, positionBefore + enemyBefore.Velocity / 60f, 0.0001f);
    }),
    ("AI context computes distance, relative velocity, closing speed and both aim errors", () =>
    {
        var sim = CombatSimulation(player: new ShipInitialState(Velocity: new Vector3(5, 0, 0)),
            enemy: new ShipInitialState(new Vector3(900, 0, 0), new Vector3(-15, 0, 0), MathF.PI / 2));
        JumpToCombat(sim);
        sim.Step(default);
        var context = sim.World.CurrentEncounter.EnemyAi!.LastContext;
        Near(context.DistanceToPlayer, 900, 0.5f);
        NearVector(context.DirectionToPlayer, -Vector3.UnitX, 0.001f);
        NearVector(context.RelativeVelocity, new Vector3(15, 0, 0), 0.001f);
        Near(context.ClosingSpeed, 15, 0.01f);
        Near(context.EnemyAimError, 0, 0.01f);
        Near(MathF.Abs(context.PlayerAimError), MathF.PI / 2, 0.01f);
    }),
    ("PD rotation controller countersteers existing angular velocity", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0),
            YawRadians: MathF.PI / 2, YawRateRadiansPerSecond: 1f));
        JumpToCombat(sim);
        sim.Step(default);
        var command = sim.World.CurrentEncounter.EnemyAi!.LastCommand;
        Check(command.YawRight && !command.YawLeft, "Positive yaw rate must receive opposing torque near aim.");
    }),
    ("APPROACH brakes instead of adding thrust at excessive closing speed", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -900),
            new Vector3(0, 0, 100), MathF.PI));
        JumpToCombat(sim);
        sim.Step(default);
        var command = sim.World.CurrentEncounter.EnemyAi!.LastCommand;
        Check(!command.MainThrust && command.ReverseThrust,
            "Fast approach must command braking through normal reverse thrust.");
    }),
    ("Threat model requires charge, range and player aim", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0),
            YawRadians: MathF.PI / 2));
        JumpToCombat(sim);
        sim.Step(default);
        var ai = sim.World.CurrentEncounter.EnemyAi!;
        var context = ai.LastContext;
        Check(!ai.IsThreatenedByPlayerLance(context, sim.Settings.LanceRangeMeters),
            "Charged lance aimed ninety degrees away is not an immediate threat.");
        var aimed = context with { PlayerAimError = 0 };
        Check(ai.IsThreatenedByPlayerLance(aimed, sim.Settings.LanceRangeMeters),
            "Charged and aimed lance in range must be a threat.");
        Check(!ai.IsThreatenedByPlayerLance(aimed with { PlayerLanceCharge = 0.79f }, sim.Settings.LanceRangeMeters),
            "Charge below threshold must not trigger evade.");
        Check(!ai.IsThreatenedByPlayerLance(aimed with { DistanceToPlayer = 1700 }, sim.Settings.LanceRangeMeters),
            "Out-of-range lance must not trigger evade.");
    }),
    ("Health-aware EVADE selects one reproducible direction and respects minimum duration", () =>
    {
        var sim = CombatSimulation(power: CombatPower());
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true)); // Full shield is removed; the enemy is now defensive.
        Step(sim, 144); // Recharge the player lance until the defensive threat threshold is reached.
        var ai = sim.World.CurrentEncounter.EnemyAi!;
        Check(ai.CurrentState == EnemyAiState.Evade && ai.CurrentEvadeDirection is not null,
            "Threat must enter EVADE.");
        var direction = ai.CurrentEvadeDirection;
        for (int i = 0; i < 25; i++)
        {
            sim.Step(default);
            Check(ai.CurrentState == EnemyAiState.Evade, "EVADE must last for its configured minimum duration.");
            Check(ai.CurrentEvadeDirection == direction, "Evade direction must not be rerolled each tick.");
        }
        var other = CombatSimulation(power: CombatPower());
        JumpToCombat(other);
        other.Step(new ShipCommand(FireLance: true)); Step(other, 144);
        Check(other.World.CurrentEncounter.EnemyAi!.CurrentEvadeDirection == direction,
            "Identical seeds must produce reproducible evade choices.");
        bool leftEvade = false;
        for (int i = 0; i < 90 && !leftEvade; i++)
        {
            sim.Step(default);
            leftEvade = ai.CurrentState != EnemyAiState.Evade;
        }
        Check(leftEvade && ai.CurrentEvadeDirection is null && ai.EvadeCooldownRemaining > 0f,
            "EVADE must end quickly and enable its cooldown.");
        Step(sim, 60);
        Check(ai.CurrentState != EnemyAiState.Evade, "Evade cooldown must prevent immediate re-entry.");
    }),
    ("Enemy lance overload causes frozen GameOver", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0),
            YawRadians: MathF.PI / 2), shield: new ShieldSettings { MaximumShield = 1f }, hull: new HullSettings { MaximumHull = 1 });
        JumpToCombat(sim);
        bool sawAttack = false;
        for (int i = 0; i < 2_400 && sim.World.GameState == GameState.Running; i++)
        {
            sim.Step(default);
            sawAttack |= sim.World.CurrentEncounter.EnemyAi!.CurrentState == EnemyAiState.Attack;
        }
        Check(sawAttack, "A healthy enemy must reach ATTACK and fire through the shared weapon rules.");
        Check(sim.World.GameState == GameState.GameOver, $"A clean enemy lance hit must destroy the player (state {sim.World.CurrentEncounter.EnemyAi!.CurrentState}, distance {sim.World.CurrentEncounter.EnemyAi.LastContext.DistanceToPlayer:0}, aim {MathF.Abs(sim.World.CurrentEncounter.EnemyAi.LastContext.EnemyAimError) * 180 / MathF.PI:0}).");
        Check(sim.Events.OfType<PlayerDestroyed>().Count() == 1, "PlayerDestroyed must be emitted once.");
        Check(sim.Events.OfType<WeaponFired>().Single(e => e.Owner == WeaponOwner.Enemy).HitKind == WeaponHitKind.Player,
            "Enemy shot must identify its owner and player hit.");
        long tick = sim.World.Tick;
        var playerPosition = sim.World.Ship.Position;
        var enemyPosition = sim.World.CurrentEncounter.Enemy!.Ship.Position;
        sim.Step(new ShipCommand(MainThrust: true, FireLance: true), new NavigationCommand(1));
        Check(sim.World.Tick == tick && sim.World.Ship.Position == playerPosition &&
              sim.World.CurrentEncounter.Enemy!.Ship.Position == enemyPosition && sim.Events.Count == 0,
            "GameOver must freeze physics, weapons, AI, navigation and simulation time.");
    }),
    ("Player lance with overload destroys enemy and disables its AI", () =>
    {
        var sim = CombatSimulation(shield: new ShieldSettings { LanceDamage = 200f }, hull: new HullSettings { MaximumHull = 1 });
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true));
        var enemy = sim.World.CurrentEncounter.Enemy!;
        Check(enemy.IsDestroyed && sim.World.CurrentEnemy is null, "One lance hit must destroy the enemy.");
        Check(sim.World.CurrentEncounter.EnemyAi!.CurrentState == EnemyAiState.Destroyed,
            "Destroyed enemy AI must enter DESTROYED.");
        Check(sim.World.CurrentEncounter.EnemyAi.LastCommand == default, "Destroyed AI must stop commanding.");
        Check(sim.Events.OfType<EnemyDestroyed>().Single().EnemyId == enemy.EnemyId,
            "EnemyDestroyed must identify the enemy.");
        Check(sim.World.GameState == GameState.Running, "Destroying enemy must not end the player game.");
        var position = enemy.Ship.Position;
        Step(sim, 300);
        Check(enemy.Ship.Position == position, "Destroyed enemy physics must stop.");
    }),
    ("Ship collision below one hundred meters destroys both ships", () =>
    {
        var atThreshold = CombatSimulation(enemy: new ShipInitialState(new Vector3(100, 0, 0)));
        JumpToCombat(atThreshold);
        atThreshold.Step(default);
        Check(atThreshold.World.GameState == GameState.Running && !atThreshold.World.CurrentEnemy!.IsDestroyed,
            "Exactly one hundred meters must not count as a collision.");

        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(99, 0, 0)));
        JumpToCombat(sim);
        var enemy = sim.World.CurrentEnemy!;
        sim.Step(new ShipCommand(FireLance: true));
        Check(sim.World.GameState == GameState.GameOver && enemy.IsDestroyed && sim.World.CurrentEnemy is null,
            "A collision below one hundred meters must destroy player and enemy together.");
        Check(sim.World.CurrentEncounter.EnemyAi!.CurrentState == EnemyAiState.Destroyed,
            "The destroyed enemy AI must stop after a collision.");
        Check(sim.Events.OfType<ShipCollision>().Single().EnemyId == enemy.EnemyId &&
              sim.Events.OfType<EnemyDestroyed>().Single().EnemyId == enemy.EnemyId &&
              sim.Events.OfType<PlayerDestroyed>().Single().EnemyId == enemy.EnemyId,
            "Collision destruction events must identify the same enemy.");
        Check(!sim.Events.OfType<WeaponFired>().Any(), "Collision must resolve before weapon commands in the same tick.");
        long tick = sim.World.Tick;
        sim.Step(default);
        Check(sim.World.Tick == tick && sim.Events.Count == 0, "Collision GameOver must freeze the simulation.");
    }),
    ("Destroyed enemies damage the player by explosion distance", () =>
    {
        var shieldOnly = ExplosionScenario(349f);
        JumpToCombat(shieldOnly);
        shieldOnly.Step(new ShipCommand(FireLance: true));
        Check(shieldOnly.World.GameState == GameState.Running && shieldOnly.World.Ship.Shield.CurrentShield == 0f,
            $"An enemy destroyed within 350 meters must deplete the player shield only (shield {shieldOnly.World.Ship.Shield.CurrentShield}, enemy {shieldOnly.World.CurrentEnemy?.IsDestroyed}, events {string.Join(',', shieldOnly.Events.Select(item => item.GetType().Name))}).");
        Check(shieldOnly.Events.OfType<ShieldDepleted>().Any(hit => hit.TargetOwner == WeaponOwner.Player),
            "Shield depletion must be reported for the player.");

        var oneSubsystem = ExplosionScenario(249f);
        JumpToCombat(oneSubsystem);
        oneSubsystem.Step(new ShipCommand(FireLance: true));
        float[] oneConditions = [oneSubsystem.World.Ship.Systems.PropulsionCondition,
            oneSubsystem.World.Ship.Systems.WeaponsCondition, oneSubsystem.World.Ship.Systems.ShieldsCondition];
        Check(oneSubsystem.World.GameState == GameState.Running && oneConditions.Count(value => value == 0f) == 1,
            "An enemy destroyed within 250 meters must disable exactly one player subsystem.");

        var twoSubsystems = ExplosionScenario(199f);
        JumpToCombat(twoSubsystems);
        twoSubsystems.Step(new ShipCommand(FireLance: true));
        float[] twoConditions = [twoSubsystems.World.Ship.Systems.PropulsionCondition,
            twoSubsystems.World.Ship.Systems.WeaponsCondition, twoSubsystems.World.Ship.Systems.ShieldsCondition];
        Check(twoSubsystems.World.GameState == GameState.Running && twoConditions.Count(value => value == 0f) == 2,
            "An enemy destroyed within 200 meters must disable two distinct player subsystems.");

        var fatal = ExplosionScenario(149f);
        JumpToCombat(fatal);
        fatal.Step(new ShipCommand(FireLance: true));
        Check(fatal.World.GameState == GameState.GameOver && fatal.World.Ship.Hull.CurrentHull == 0,
            "An enemy destroyed within 150 meters must destroy the player ship.");
        Check(fatal.Events.OfType<EnemyExplosion>().Single().DistanceToPlayer <= 150f &&
              fatal.Events.OfType<PlayerDestroyed>().Single().EnemyId == 2,
            "Fatal explosion events must preserve the source enemy and distance.");
    }),
    ("Power allocation rejects overflow and negative allocations but accepts an atomic valid request", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 0 }, spawnEnemy: false);
        var power = sim.World.Ship.Power;
        sim.Step(default, default, new PowerAllocationCommand(5));
        Check(!sim.Events.OfType<PowerAllocationChanged>().Any(), "Overflow allocation must be rejected.");
        Near(power.PropulsionAllocation, 35);
        sim.Step(default, default, new PowerAllocationCommand(-40));
        Check(!sim.Events.OfType<PowerAllocationChanged>().Any(), "Negative allocation must be rejected.");
        sim.Step(default, default, new PowerAllocationCommand(-5, 5));
        Check(sim.Events.OfType<PowerAllocationChanged>().Single() is { Propulsion: 30, Weapons: 40, Shields: 30 },
            "Valid allocation must be applied atomically.");
        Near(power.AllocatedPower, 100);
    }),
    ("Propulsion power linearly scales thrust and torque while inertia persists at zero", () =>
    {
        var half = New();
        half.Step(default, default, new PowerAllocationCommand(-50));
        Step(half, 60, new ShipCommand(MainThrust: true, YawLeft: true));
        Near(half.World.Ship.Velocity.Length(), 6f, 0.01f);
        Near(half.World.Ship.AngularVelocity.Y, 0.3f, 0.01f);
        half.Step(default, default, new PowerAllocationCommand(-50));
        var velocity = half.World.Ship.Velocity;
        var angular = half.World.Ship.AngularVelocity.Y;
        Step(half, 60, new ShipCommand(MainThrust: true, YawLeft: true));
        NearVector(half.World.Ship.Velocity, velocity, 0.001f);
        Near(half.World.Ship.AngularVelocity.Y, angular, 0.001f);
    }),
    ("Weapons power controls charge rate and preserves existing lance charge", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = new PowerSettings { DefaultPropulsionPower = 0, DefaultWeaponsPower = 50, DefaultShieldsPower = 0 }
        }, spawnEnemy: false);
        Step(sim, 359);
        Check(!sim.World.Lance.IsReady, "Fifty percent weapons power must need six seconds.");
        Step(sim, 1);
        Check(sim.World.Lance.IsReady, "Fifty percent weapons power must finish after six seconds.");
        var stopped = NewWeapons();
        Step(stopped, 90);
        float charge = stopped.World.Lance.ChargeFraction;
        stopped.Step(default, default, new PowerAllocationCommand(WeaponsDelta: -100));
        Step(stopped, 300);
        Near(stopped.World.Lance.ChargeFraction, charge, 0.0001f);
        stopped.Step(default, default, new PowerAllocationCommand(WeaponsDelta: 100));
        Step(stopped, 90);
        Check(stopped.World.Lance.IsReady, "Restored weapons power must continue from retained charge.");
    }),
    ("A ready lance fires after weapons power is removed and still resets charge", () =>
    {
        var sim = WithTargets(new Vector3(0, 0, -300));
        Step(sim, 180);
        sim.Step(default, default, new PowerAllocationCommand(WeaponsDelta: -100));
        sim.Step(new ShipCommand(FireLance: true));
        Check(sim.Events.OfType<WeaponFired>().Any() && sim.World.Targets.Count == 0, "Ready lance must fire without weapons power.");
        Near(sim.World.Lance.ChargeFraction, 0);
    }),
    ("Full enemy shield absorbs one lance hit, emits shield events and delays recharge", () =>
    {
        var sim = CombatSimulation(power: CombatPower());
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true));
        var enemy = sim.World.CurrentEnemy!;
        Check(!enemy.IsDestroyed && enemy.Ship.Shield.CurrentShield == 0f, "Full shield must absorb the first hit.");
        Check(sim.Events.OfType<ShieldHit>().Single().TargetEnemyId == enemy.EnemyId, "Shield hit must identify its enemy.");
        Check(sim.Events.OfType<ShieldDepleted>().Any(), "Shield depletion event is required.");
        Step(sim, 180);
        Near(enemy.Ship.Shield.CurrentShield, 0);
        Step(sim, 3);
        Check(enemy.Ship.Shield.CurrentShield > 0f, "Shield must recharge after its delay.");
    }),
    ("Partial enemy shield passes residual lance damage through and destroys the ship", () =>
    {
        var sim = CombatSimulation(power: CombatPower(), shield: new ShieldSettings { MaximumShield = 40f }, hull: new HullSettings { MaximumHull = 1 });
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true));
        var enemy = sim.World.CurrentEncounter.Enemy!;
        Check(enemy.IsDestroyed && enemy.Ship.Shield.CurrentShield == 0f, "Residual lance damage must destroy a partially shielded enemy.");
    }),
    ("Enemy lance damages the player shield before GameOver", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0), YawRadians: MathF.PI / 2),
            power: CombatPower());
        JumpToCombat(sim);
        for (int i = 0; i < 3_600 && sim.World.Ship.Shield.CurrentShield > 0f; i++) sim.Step(default);
        Check(sim.World.Ship.Shield.CurrentShield == 0f && sim.World.GameState == GameState.Running,
            $"First enemy lance hit must deplete the player shield without GameOver (state {sim.World.CurrentEncounter.EnemyAi!.CurrentState}, distance {sim.World.CurrentEncounter.EnemyAi.LastContext.DistanceToPlayer:0}, closing {sim.World.CurrentEncounter.EnemyAi.LastContext.ClosingSpeed:0}, velocity {sim.World.CurrentEnemy!.Ship.Velocity.Length():0}, power {sim.World.CurrentEnemy!.Ship.Power.PropulsionAllocation:0}, aim {MathF.Abs(sim.World.CurrentEncounter.EnemyAi.LastContext.EnemyAimError) * 180 / MathF.PI:0}).");
        Check(sim.Events.OfType<ShieldHit>().Any(hit => hit.TargetOwner == WeaponOwner.Player),
            "Player shield hit event is required.");
    }),
    ("Healthy enemy remains aggressive against a merely ready player lance", () =>
    {
        var sim = CombatSimulation(power: CombatPower());
        JumpToCombat(sim);
        Step(sim, 180);
        var enemy = sim.World.CurrentEnemy!;
        var ai = sim.World.CurrentEncounter.EnemyAi!;
        Check(ai.CurrentRiskLevel == EnemyRiskLevel.Aggressive && ai.CurrentState != EnemyAiState.Evade,
            "A full shield and full hull must not evade just because the player lance is ready.");
        Check(enemy.Ship.Power.WeaponsAllocation == sim.Settings.Power.AttackProfile.Weapons ||
              enemy.Ship.Power.WeaponsAllocation == sim.Settings.Power.RepositionProfile.Weapons,
            "The aggressive AI must still use an ordinary configured power profile.");
    }),
    ("Zero shield allocation prevents recharge and recharge never exceeds maximum", () =>
    {
        var noRecharge = CombatPower() with { DefendProfile = new PowerProfile(0f, 0f, 0f) };
        var stopped = CombatSimulation(power: noRecharge);
        JumpToCombat(stopped);
        stopped.Step(new ShipCommand(FireLance: true));
        var stoppedEnemy = stopped.World.CurrentEnemy!;
        Step(stopped, 240);
        Near(stoppedEnemy.Ship.Shield.CurrentShield, 0f);

        var charging = CombatSimulation(power: CombatPower());
        JumpToCombat(charging);
        charging.Step(new ShipCommand(FireLance: true));
        var chargingEnemy = charging.World.CurrentEnemy!;
        Step(charging, 900);
        Check(chargingEnemy.Ship.Shield.CurrentShield <= chargingEnemy.Ship.Shield.MaximumShield,
            "Shield recharge must never exceed its configured maximum.");
    }),
    ("Residual damage removes hull and damages exactly one reproducible subsystem", () =>
    {
        var sim = CombatSimulation(power: CombatPower());
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true)); // shield absorbs
        Step(sim, 180);
        sim.Step(new ShipCommand(FireLance: true)); // hull hit
        var enemy = sim.World.CurrentEnemy!;
        Check(enemy.Ship.Hull.CurrentHull == 2, "Residual lance damage must remove one hull point.");
        var conditions = new[] { enemy.Ship.Systems.PropulsionCondition, enemy.Ship.Systems.WeaponsCondition, enemy.Ship.Systems.ShieldsCondition };
        Check(conditions.Count(value => value == 0.5f) == 1 && conditions.Count(value => value == 1f) == 2,
            "Each hull hit must damage exactly one subsystem by fifty percent.");
        Check(sim.Events.OfType<HullDamaged>().Any() && sim.Events.OfType<SubsystemDamaged>().Any(), "Hull events are required.");
    }),
    ("Warp repairs systems and shield but preserves hull", () =>
    {
        var sim = CombatSimulation(power: CombatPower());
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true)); Step(sim, 180); sim.Step(new ShipCommand(FireLance: true));
        int hull = sim.World.CurrentEnemy!.Ship.Hull.CurrentHull;
        // Damage the player through the same seeded combat rules is not required: warp repair applies to player state.
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(1));
        Check(sim.World.Ship.Systems.PropulsionCondition == 1f && sim.World.Ship.Systems.WeaponsCondition == 1f &&
              sim.World.Ship.Systems.ShieldsCondition == 1f && sim.World.Ship.Shield.CurrentShield == sim.World.Ship.Shield.MaximumShield,
            "Successful warp must repair player systems and refill the shield.");
        Check(sim.World.CurrentEncounter.Id == 1 && hull == 2, "Warp must not repair stored enemy hull or alter encounter progress.");
    }),
    ("Power and condition define the speed limit without removing existing momentum", () =>
    {
        var sim = New();
        Step(sim, 3000, new ShipCommand(MainThrust: true));
        Near(sim.World.Ship.Velocity.Length(), 500f, 0.1f);
        sim.Step(default, default, new PowerAllocationCommand(-50));
        Step(sim, 60, new ShipCommand(MainThrust: true));
        Check(sim.World.Ship.Velocity.Length() >= 499f, "Reducing power must not clamp existing velocity.");
        Step(sim, 60, new ShipCommand(ReverseThrust: true));
        Check(sim.World.Ship.Velocity.Length() < 499f, "Reverse thrust must brake while overspeed.");
    }),
    ("Enemy risk assessment is deterministic and health aware", () =>
    {
        var settings = new EnemyAiSettings();
        Check(EnemyRiskAssessment.Calculate(3, 3, 1f, 1f, 1f, 1f, settings) == EnemyRiskLevel.Aggressive,
            "Full hull, shield and systems must be aggressive.");
        Check(EnemyRiskAssessment.Calculate(3, 3, 0.5f, 1f, 1f, 1f, settings) == EnemyRiskLevel.Normal,
            "A partially depleted shield must produce NORMAL caution.");
        Check(EnemyRiskAssessment.Calculate(3, 3, 0.1f, 1f, 1f, 1f, settings) == EnemyRiskLevel.Defensive,
            "A nearly depleted shield must produce DEFENSIVE caution.");
        Check(EnemyRiskAssessment.Calculate(2, 3, 1f, 1f, 1f, 1f, settings) == EnemyRiskLevel.Defensive,
            "Hull damage must increase caution.");
        Check(EnemyRiskAssessment.Calculate(1, 3, 1f, 1f, 1f, 1f, settings) == EnemyRiskLevel.Critical &&
              EnemyRiskAssessment.Calculate(3, 3, 1f, 0.2f, 1f, 1f, settings) == EnemyRiskLevel.Critical,
            "Critical hull or subsystem damage must produce CRITICAL caution.");
    }),
    ("Invalid AI settings are rejected", () =>
    {
        bool rejected = false;
        try
        {
            _ = new Simulation(new SimulationSettings
            {
                EnemyAi = new EnemyAiSettings { AttackEnterDistance = 500, MinimumCombatDistance = 600 }
            });
        }
        catch (ArgumentException) { rejected = true; }
        Check(rejected, "Invalid hysteresis distances must fail fast.");
    })
};

int failed = 0;
foreach (var (name, run) in tests)
{
    try { run(); Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failed++; Console.Error.WriteLine($"FAIL {name}: {e.Message}"); }
}
Console.WriteLine($"{tests.Length - failed}/{tests.Length} tests passed.");
return failed == 0 ? 0 : 1;

static Simulation New(ShipInitialState initial = default) =>
    new(new SimulationSettings { TargetCount = 0, Power = PropulsionOnlyPower() }, initial, spawnEnemy: false);
static Simulation NewWeapons(ShipInitialState initial = default) =>
    new(new SimulationSettings { TargetCount = 0, Power = WeaponsOnlyPower() }, initial, spawnEnemy: false);
static Simulation WithTargets(params Vector3[] targets) =>
    new(new SimulationSettings { TargetCount = targets.Length, Power = WeaponsOnlyPower() }, initialTargets: targets, spawnEnemy: false);
static Simulation CombatSimulation(ShipInitialState player = default, ShipInitialState? enemy = null,
    ShieldSettings? shield = null, PowerSettings? power = null, HullSettings? hull = null) =>
    new(new SimulationSettings
    {
        TargetCount = 0, EncounterTwoTargetCount = 0,
        Shield = shield ?? new ShieldSettings(), Power = power ?? new PowerSettings(), Hull = hull ?? new HullSettings()
    }, player,
        randomSeed: 42, enemyInitial: enemy, spawnEnemy: true);
static Simulation ExplosionScenario(float distance) => CombatSimulation(
    enemy: new ShipInitialState(new Vector3(0, 0, -distance)),
    shield: new ShieldSettings { LanceDamage = 200f }, hull: new HullSettings { MaximumHull = 1 });
static PowerSettings PropulsionOnlyPower() => new()
{
    DefaultPropulsionPower = 100f, DefaultWeaponsPower = 0f, DefaultShieldsPower = 0f
};
static PowerSettings WeaponsOnlyPower() => new()
{
    DefaultPropulsionPower = 0f, DefaultWeaponsPower = 100f, DefaultShieldsPower = 0f
};
static PowerSettings CombatPower() => new()
{
    DefaultPropulsionPower = 0f, DefaultWeaponsPower = 100f, DefaultShieldsPower = 0f,
    AttackProfile = new PowerProfile(0f, 100f, 0f),
    DefendProfile = new PowerProfile(0f, 0f, 100f),
    EvadeProfile = new PowerProfile(60f, 10f, 30f),
    RepositionProfile = new PowerProfile(60f, 40f, 0f),
    MinimumProfileDuration = 1f
};
static void JumpToCombat(Simulation sim)
{
    Step(sim, 600);
    sim.Step(default, new NavigationCommand(3));
}
static void Fire(Simulation sim)
{
    Step(sim, 180);
    sim.Step(new ShipCommand(FireLance: true));
}
static void Step(Simulation simulation, int count, ShipCommand command = default)
{
    for (int i = 0; i < count; i++) simulation.Step(command);
}
static async Task StationServerSmokeAsync()
{
    var commands = new ArmariumCommandBuffer();
    var assets = new Dictionary<string, string>
    {
        ["index.html"] = "<main>ARMARIUM</main>",
        ["armarium.css"] = "body{}",
        ["armarium.js"] = ""
    };
    using var server = new StationServer(new StationServerOptions { Port = 0, StateUpdatesPerSecond = 30 }, assets, commands);
    server.UpdateState(new ArmariumState(true, -12.4f, 640f, 0.72f, false, 2.5f, 3, -12.4f, 42));
    server.Start();
    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
    string page = await http.GetStringAsync(server.ArmariumUrl);
    Check(page.Contains("ARMARIUM"), "Station server must serve the Armarium page.");

    using var socket = new ClientWebSocket();
    await socket.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/station"), CancellationToken.None);
    await SendWebSocketJsonAsync(socket, new { type = "hello", station = "armarium", protocolVersion = StationProtocol.Version });
    using JsonDocument welcome = JsonDocument.Parse(await ReceiveWebSocketTextAsync(socket));
    Check(welcome.RootElement.GetProperty("type").GetString() == "welcome" &&
          welcome.RootElement.GetProperty("protocolVersion").GetInt32() == StationProtocol.Version,
        "Station server must accept the matching Armarium protocol.");
    Check(server.IsArmariumOnline, "An accepted Armarium handshake must set the online indicator.");
    using JsonDocument state = JsonDocument.Parse(await ReceiveWebSocketTextAsync(socket));
    Check(state.RootElement.GetProperty("type").GetString() == "armarium_state" &&
          state.RootElement.GetProperty("targetAvailable").GetBoolean() &&
          !state.RootElement.TryGetProperty("armariumControlsActive", out _) &&
          MathF.Abs(state.RootElement.GetProperty("lanceTurretAngleDegrees").GetSingle() - 2.5f) < 0.001f &&
          state.RootElement.GetProperty("targetHitSequence").GetInt64() == 3 &&
          MathF.Abs(state.RootElement.GetProperty("lastTargetHitBearingDegrees").GetSingle() + 12.4f) < 0.001f &&
          MathF.Abs(state.RootElement.GetProperty("targetDistanceMeters").GetSingle() - 640f) < 0.001f &&
          MathF.Abs(state.RootElement.GetProperty("targetBearingDegrees").GetSingle() + 12.4f) < 0.001f,
        "Station server must transmit only the current Armarium snapshot.");
    await SendWebSocketJsonAsync(socket, new { type = "fire_lance" });
    bool received = SpinWait.SpinUntil(() => commands.ReadCommand().FireLance, TimeSpan.FromSeconds(1));
    Check(received, "Station fire_lance must reach the thread-safe command buffer.");
    await SendWebSocketJsonAsync(socket, new { type = "turret", direction = "left", active = true });
    bool turretReceived = SpinWait.SpinUntil(() => commands.ReadCommand().AimLanceLeft, TimeSpan.FromSeconds(1));
    Check(turretReceived, "Station turret input must reach the thread-safe command buffer.");
}
static async Task SendWebSocketJsonAsync(ClientWebSocket socket, object value)
{
    byte[] data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value));
    await socket.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
}
static async Task<string> ReceiveWebSocketTextAsync(ClientWebSocket socket)
{
    byte[] buffer = new byte[4096];
    WebSocketReceiveResult result = await socket.ReceiveAsync(buffer, CancellationToken.None);
    Check(result.MessageType == WebSocketMessageType.Text && result.EndOfMessage, "Expected one complete WebSocket text message.");
    return Encoding.UTF8.GetString(buffer, 0, result.Count);
}
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Near(float actual, float expected, float epsilon = 0.0001f) =>
    Check(MathF.Abs(actual - expected) <= epsilon, $"Expected {expected}, got {actual}.");
static void NearVector(Vector3 actual, Vector3 expected, float epsilon = 0.0001f) =>
    Check(Vector3.Distance(actual, expected) <= epsilon, $"Expected {expected}, got {actual}.");
