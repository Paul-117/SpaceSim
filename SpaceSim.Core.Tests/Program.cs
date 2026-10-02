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
using SpaceSim.Stations.Debug;
using SpaceSim.Stations.Reactorium;
using SpaceSim.Stations.Sensorium;

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
        WeaponFired shot = sim.Events.OfType<WeaponFired>().Single();
        Near(shot.End.Length(), sim.Settings.LanceRangeMeters);
        Near(shot.VisualFadeStart!.Value.Length(), sim.Settings.LanceRangeMeters);
        Near(shot.VisualEnd!.Value.Length(), sim.Settings.LanceVisualRangeMeters);
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
              noTarget.TargetDistanceMeters == 0f && noTarget.LanceTurretAngleDegrees == 0f &&
              noTarget.MaximumPower == 40f && noTarget.LanceSystemCondition == 1f,
            "An empty encounter must not expose a target or world state.");
        Step(empty, 180);
        Check(ArmariumStateBuilder.Build(empty.World).LanceReady,
            "Armarium must expose the ordinary lance readiness state.");
        Step(empty, 30, new ShipCommand(AimLanceRight: true));
        Near(ArmariumStateBuilder.Build(empty.World).LanceTurretAngleDegrees, 5f);

        var combat = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -900)));
        JumpToCombat(combat);
        ArmariumState target = ArmariumStateBuilder.Build(combat.World);
        Check(target.TargetAvailable && MathF.Abs(target.TargetBearingDegrees) < 0.01f &&
              MathF.Abs(target.TargetDistanceMeters - 900f) < 5f && target.SimulationTick == combat.World.Tick,
            "Armarium must receive its compact target map distance, bearing and lance state.");
    }),
    ("Sensorium state exposes compact contact telemetry without world coordinates", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -900)));
        JumpToCombat(sim);
        SensoriumState state = SensoriumStateBuilder.Build(sim.World);
        SensoriumContact contact = state.Contacts.Single();
        Check(contact.SignatureCode == "ARGUS" && contact.ShipClass == "FRIGATE" && contact.Name.StartsWith("Argus", StringComparison.Ordinal) &&
              MathF.Abs(contact.BearingDegrees) < .01f && MathF.Abs(contact.DistanceMeters - 900f) < .1f &&
              contact.ReactorOutputFraction is >= 0f and <= 1f && contact.ShieldFraction is >= 0f and <= 1f,
            "Sensorium must receive compact class, bearing, range and system telemetry for its own contact view.");
    }),
    ("Sensorium controls bridge contact visibility and active sonar alerts enemies", () =>
    {
        var bearingSim = CombatSimulation(player: new ShipInitialState(YawRadians: MathF.PI / 2),
            enemy: new ShipInitialState(new Vector3(0, 0, -900)));
        JumpToCombat(bearingSim);
        Near(SensoriumStateBuilder.Build(bearingSim.World).Contacts.Single().BearingDegrees, 0f);

        var alertSim = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -3_000)));
        JumpToCombat(alertSim);
        EnemyAiController ai = alertSim.World.CurrentEncounter.EnemyAi!;
        Check(!ai.IsPlayerDetected, "Distant enemy must still be unaware before active sonar.");
        Check(!alertSim.World.VisibleEnemies.Any(), "The bridge must start a combat encounter without an enemy contact.");
        int enemyId = alertSim.World.CurrentEnemies.Single().EnemyId;
        alertSim.World.RequireSensoriumConfirmationForBridgeContacts = false;
        Check(alertSim.World.VisibleEnemies.Single().EnemyId == enemyId,
            "Disabling Sensorium confirmation must expose every active contact to the bridge.");
        alertSim.World.RequireSensoriumConfirmationForBridgeContacts = true;
        alertSim.Step(default, sensorCommand: new SensorCommand(ConfirmedEnemyId: enemyId));
        Check(alertSim.World.VisibleEnemies.Single().EnemyId == enemyId,
            "A confirmed spectrometer contact must become visible to bridge map and contact controls.");
        alertSim.Step(default, sensorCommand: new SensorCommand(ActiveSonarPing: true));
        Check(ai.IsPlayerDetected && ai.DesiredReactorOperatingLevelPercent == 100f && ai.CurrentState == EnemyAiState.Approach,
            "An active sonar emission must alarm the enemy through the ordinary combat detection path.");
        Check(alertSim.World.VisibleEnemies.Count() == alertSim.World.CurrentEnemies.Count(),
            "An active sonar emission must reveal every local enemy to the bridge.");
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
    ("Enemy AI debug state is focused and exposes patrol telemetry", () =>
    {
        var sim = new Simulation();
        EnemyDebugState empty = EnemyDebugStateBuilder.Build(sim.World);
        Check(!empty.EnemyAvailable && empty.SimulationTick == sim.World.Tick,
            "Enemy debug station must report the absence of an active enemy without exposing a WorldState.");
        Step(sim, 600);
        EnterEncounter(sim, 2);
        sim.Step(default);
        EnemyDebugState state = EnemyDebugStateBuilder.Build(sim.World);
        Check(state.EnemyAvailable && state.EnemyId == 1 && !state.PlayerDetected &&
              state.Difficulty == "EASY" && state.AiState == "ACQUIRE" && state.ReactorOperatingLevelPercent == 50f &&
              state.PropulsionRequested == 50f && state.PropulsionDraw > 0f && state.WeaponsDraw == 0f && state.ShieldsDraw == 0f &&
              state.EnemyFireControl is { CombatActive: false, FireCommandWouldBeIssued: false } &&
              state.PlayerFireControl is { HasTarget: true, AimToleranceDegrees: 2f },
            "Enemy debug state must expose only the active patrol enemy's meaningful AI and power telemetry.");
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
        EnemyShipState[] enemies = sim.World.Encounters.Skip(1).SelectMany(encounter => encounter.Enemies).ToArray();
        Check(enemies.Select(enemy => enemy.Name).All(name => !string.IsNullOrWhiteSpace(name)) &&
              enemies.Select(enemy => enemy.Name).Distinct().Count() == enemies.Length,
            "Every configured enemy must have a distinct contact name.");
        Check(enemies.Select(enemy => enemy.ShipClass).Distinct().Count() == enemies.Length,
            "Each configured enemy must expose its contact class.");
        Check(enemies.Select(enemy => enemy.ShipClass).SequenceEqual(
                [EnemyShipClass.Corvette, EnemyShipClass.Frigate, EnemyShipClass.Cruiser]),
            "Easy, Medium and Hard encounters must use the CETUS, ARGUS and ATLAS contact classes.");
        Check(sim.World.Encounters.SelectMany(e => e.Targets).Select(t => t.Id).Distinct().Count() == 10,
            "Target IDs must be unique across encounters.");
    }),
    ("Distant enemies begin on deterministic random patrol courses with patrol reactor power", () =>
    {
        var sim = new Simulation();
        var enemies = sim.World.Encounters.Skip(1).SelectMany(encounter => encounter.Enemies).ToArray();
        Check(enemies.Length == 3, "Each hostile encounter must contain its patrol enemy.");
        foreach (EnemyShipState enemy in enemies)
        {
            float distance = enemy.Ship.Position.Length();
            Check(distance >= sim.Settings.EnemyAi.PatrolSpawnMinimumDistanceMeters &&
                  distance <= sim.Settings.EnemyAi.PatrolSpawnMaximumDistanceMeters,
                "Patrol enemy must spawn two to three kilometres from the local player origin.");
            Near(enemy.Ship.Velocity.Length(), sim.Settings.EnemyAi.PatrolCruiseSpeedMetersPerSecond);
            Near(enemy.Ship.Reactor.OperatingLevelPercent, sim.Settings.EnemyAi.PatrolReactorOperatingLevelPercent);
            Near(enemy.Ship.Power.PropulsionRequested, sim.Settings.EnemyAi.PatrolPropulsionDraw);
            Near(enemy.Ship.Power.PropulsionDraw, sim.Settings.EnemyAi.PatrolPropulsionDraw);
            Near(enemy.Ship.Power.WeaponsDraw, 0f);
            Near(enemy.Ship.Power.ShieldsDraw, 0f);
            Near(enemy.Ship.Shield.CurrentShield, 0f);
            Check(Vector3.Dot(Vector3.Normalize(enemy.Ship.Velocity), enemy.Ship.Forward) > .999f,
                "Patrol velocity must point along the enemy nose.");
            Check(!sim.World.Encounters.First(encounter => encounter.Enemies.Contains(enemy)).GetEnemyAi(enemy.EnemyId)!.IsPlayerDetected,
                "A distant patrol must not know about the player.");
        }
    }),
    ("Enemy detection at one point five kilometres starts reactor ramp and combat station charging", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 0, EncounterThreeTargetCount = 0 },
            enemyInitial: new ShipInitialState(new Vector3(0, 0, -1_500), YawRadians: MathF.PI), spawnEnemy: true);
        Step(sim, 600);
        EnterEncounter(sim, 3);
        sim.Step(default);
        EnemyShipState enemy = sim.World.CurrentEnemy!;
        EnemyAiController ai = sim.World.CurrentEncounter.EnemyAi!;
        Check(ai.IsPlayerDetected && ai.CurrentState == EnemyAiState.Approach,
            "Enemy inside the detection range must enter the existing combat FSM.");
        Near(enemy.Ship.Reactor.TargetOperatingLevelPercent, 100f);
        Check(enemy.Ship.Reactor.OperatingLevelPercent > sim.Settings.EnemyAi.PatrolReactorOperatingLevelPercent,
            "Detection must begin the reactor ramp instead of jumping directly to full output.");
        Check(enemy.Ship.Power.PropulsionRequested == enemy.Ship.Power.MaximumPropulsionDraw &&
              enemy.Ship.Power.WeaponsRequested == enemy.Ship.Power.MaximumWeaponsDraw &&
              enemy.Ship.Power.ShieldsRequested == enemy.Ship.Power.MaximumShieldsDraw,
            "Detected enemy must request all three stations at their normal maximum.");
    }),
    ("Enemy detection range scales linearly with the player's physical reactor output", () =>
    {
        var halfOutside = DetectionSimulation(50f, 1_001f);
        JumpToCombat(halfOutside);
        halfOutside.Step(default);
        Check(!halfOutside.World.CurrentEncounter.EnemyAi!.IsPlayerDetected,
            "A 50 percent reactor must not be detected beyond one kilometre.");

        var halfAtRange = DetectionSimulation(50f, 1_000f);
        JumpToCombat(halfAtRange);
        halfAtRange.Step(default);
        Check(halfAtRange.World.CurrentEncounter.EnemyAi!.IsPlayerDetected,
            "A 50 percent reactor must be detected at one kilometre.");

        var fullAtRange = DetectionSimulation(100f, 2_000f);
        JumpToCombat(fullAtRange);
        fullAtRange.Step(default);
        Check(fullAtRange.World.CurrentEncounter.EnemyAi!.IsPlayerDetected,
            "A 100 percent reactor must be detected at two kilometres.");
    }),
    ("Encounter 4 activates its Hard enemy through shared physics", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 0 });
        Step(sim, 600);
        EnterEncounter(sim, 4);
        var enemies = sim.World.CurrentEnemies.OrderBy(enemy => enemy.EnemyId).ToArray();
        Check(enemies.Length == 1 && enemies.Single().EnemyId == 3 && enemies.Single().Difficulty == EnemyDifficulty.Hard,
            "Encounter 4 must activate its Hard enemy.");
        Check(sim.World.CurrentEncounter.EnemyAi?.CurrentState == EnemyAiState.Acquire,
            "The enemy requires its own initial controller state.");
        sim.Step(default);
        Check(sim.World.CurrentEncounter.EnemyAi is { IsPlayerDetected: false, CurrentState: EnemyAiState.Acquire },
            "A distant enemy must remain on its undetected patrol instead of entering combat.");
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
    ("Configured gameplay start can open with a ready warp drive", () =>
    {
        var sim = new Simulation(new SimulationSettings { StartWarpReady = true }, spawnEnemy: false);
        Check(sim.World.WarpDrive.IsReady && sim.World.WarpDrive.ChargeFraction == 1f && sim.World.WarpDrive.RemainingSeconds == 0,
            "A ready-start configuration must initialize the warp state before the first simulation tick.");
    }),
    ("Configured gameplay start begins in hyperspace and can enter Encounter 1", () =>
    {
        var sim = new Simulation(new SimulationSettings { StartInHyperspace = true }, spawnEnemy: false);
        Check(sim.World.HyperspacePhase == HyperspacePhase.SelectingDestination && sim.World.IsPlayerInHyperspace &&
              sim.World.HyperspaceOriginEncounterId is null,
            "A hyperspace-start configuration must open the destination selection without an origin encounter.");
        sim.Step(default, new NavigationCommand(1));
        Check(sim.World.HyperspacePhase == HyperspacePhase.PlanningEntry,
            "Encounter 1 must be selectable when the game starts in hyperspace.");
        sim.Step(default, new NavigationCommand(EntryPosition: new Vector3(125, 0, -75)));
        Check(sim.World.HyperspacePhase == HyperspacePhase.RealSpace && sim.World.CurrentEncounter.Id == 1,
            "Confirming the first entry point must begin the encounter in real space.");
        NearVector(sim.World.Ship.Position, new Vector3(125, 0, -75));
    }),
    ("Hyperspace entry planning uses a bounded last known enemy position", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -1_000)));
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(EnterHyperspace: true));
        sim.Step(default, new NavigationCommand(3));
        Check(sim.World.HyperspacePhase == HyperspacePhase.PlanningEntry,
            "Selecting a destination must enter the hyperspace entry-planning phase.");
        Vector3 known = sim.World.CurrentEncounter.LastKnownEnemyPosition ?? throw new Exception(
            "An encounter with an active enemy must supply a last known position.");
        Vector3 actual = sim.World.CurrentEnemy!.Ship.Position;
        Check(Vector3.Distance(known, actual) <= 300.001f,
            "Last known enemy position must stay within the configured three-hundred-metre uncertainty radius.");
        sim.Step(default);
        NearVector(sim.World.CurrentEncounter.LastKnownEnemyPosition!.Value, known, .0001f);
    }),
    ("Early jump is rejected without being queued", () =>
    {
        var sim = New();
        sim.Step(default, new NavigationCommand(EnterHyperspace: true));
        Check(sim.World.CurrentEncounter.Id == 1, "Uncharged warp must not jump.");
        Step(sim, 599);
        Check(sim.World.CurrentEncounter.Id == 1 && sim.World.WarpDrive.IsReady, "No delayed jump allowed.");
    }),
    ("Jump changes encounter, stops the ship, and consumes warp charge", () =>
    {
        var sim = NewWeapons(new ShipInitialState(Position: new Vector3(100, 0, 50), Velocity: new Vector3(10, 0, -3),
            YawRadians: 1, YawRateRadiansPerSecond: 0.4f));
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(EnterHyperspace: true));
        Check(sim.World.IsPlayerInHyperspace && sim.World.HyperspacePhase == HyperspacePhase.SelectingDestination,
            "Ready warp must first leave the encounter for hyperspace.");
        sim.Step(default, new NavigationCommand(2));
        Check(sim.World.HyperspacePhase == HyperspacePhase.PlanningEntry,
            "Selecting a destination must open entry planning without spawning the ship.");
        sim.Step(default, new NavigationCommand(EntryPosition: new Vector3(250, 0, -120)));
        Check(sim.World.CurrentEncounter.Id == 2 && sim.World.Targets.Count == 0 &&
              sim.World.CurrentEnemies.Count() == 0, "Wrong destination.");
        NearVector(sim.World.Ship.Position, new Vector3(250, 0, -120));
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
        EnterEncounter(sim, 2);
        Check(sim.World.Targets.SequenceEqual(secondTargets), "Encounter 2 must retain its initial layout.");
        Step(sim, 600);
        EnterEncounter(sim, 1);
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
    ("Autopilot uses the shared flight rules and never creates a fire command", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0), YawRadians: MathF.PI / 2));
        JumpToCombat(sim);
        var autopilot = new AutopilotController(sim.Settings.EnemyAi,
            sim.Settings.ReverseThrustNewtons / sim.Settings.ShipMassKg);
        ShipCommand command = autopilot.Tick(sim.World.Ship, sim.World.CurrentEnemy!.Ship);
        Check(!command.FireLance && !command.AimLanceLeft && !command.AimLanceRight,
            "Autopilot must leave every weapon intent to the bridge and Armarium.");
        Check(autopilot.CurrentState is AutopilotState.Approach or AutopilotState.Attack or AutopilotState.Reposition,
            "Autopilot must use a normal shared flight state.");
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
        NearVector(context.RelativeVelocity, new Vector3(15.2f, 0, 0), 0.25f);
        Near(context.ClosingSpeed, 15.2f, 0.25f);
        Near(context.LineOfSightAngularVelocity, 0f, 0.001f);
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
    ("APPROACH plans a tangential fly-by instead of reversing into a collision risk", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(0, 0, -900),
            new Vector3(0, 0, 100), MathF.PI));
        JumpToCombat(sim);
        sim.Step(default);
        var command = sim.World.CurrentEncounter.EnemyAi!.LastCommand;
        Check(!command.MainThrust && !command.ReverseThrust && (command.YawLeft || command.YawRight),
            "A fast close-range collision course must begin a normal yaw-and-thrust fly-by, not turn around for main-engine braking.");
    }),
    ("Combat AI uses one shared range and difficulty changes only aim tolerance", () =>
    {
        var sim = CombatSimulation(enemy: new ShipInitialState(new Vector3(900, 0, 0), YawRadians: MathF.PI / 2));
        JumpToCombat(sim);
        Step(sim, 30);
        var ai = sim.World.CurrentEncounter.EnemyAi!;
        Check(ai.CurrentState is EnemyAiState.Approach or EnemyAiState.Attack,
            "A detected opponent must directly approach or attack without an EVADE state.");
        Check(sim.Settings.EnemyAi.MinimumCombatDistance == 250f && sim.Settings.EnemyAi.MaximumCombatDistance == 900f,
            "All enemies must share the configured 250-900 metre combat range.");
        Check(sim.Settings.EnemyAi.NoMainEngineTurnDistanceMeters == 2_000f,
            "Enemy and bridge autopilot must protect against main-engine turnarounds within two kilometres.");
        Check(MathF.Abs(ai.FireAimToleranceRadians - MathF.PI / 180f * 3f) < .001f,
            "Medium must retain the central three-degree firing tolerance.");
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
    ("Reactorium allocations create independent player station budgets", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = new PowerSettings { DefaultReactorOperatingLevelPercent = 40f }
        }, spawnEnemy: false);
        var power = sim.World.Ship.Power;
        sim.Step(new ShipCommand(MainThrust: true), default, new ReactorCommand(Allocation: new PowerAllocation(100f, 0f, 0f)));
        Near(power.MaximumPropulsionDraw, 50f);
        Near(power.MaximumWeaponsDraw, 40f);
        Near(power.MaximumShieldsDraw, 35f);
        Near(power.PropulsionAllocationPercent, 100f);
        Near(power.WeaponsAllocationPercent, 0f);
        Near(power.ShieldsAllocationPercent, 0f);
        Near(power.PropulsionAvailable, 50f);
        Near(power.RequestedPower, 90f);
        Near(power.DemandScale, 1f);
        Near(power.CurrentDraw, 50f);
        Near(sim.World.Ship.Reactor.CurrentDraw, 50f);
    }),
    ("Reactorium rejects allocations above one hundred percent and honors station PU limits", () =>
    {
        var sim = new Simulation(new SimulationSettings { TargetCount = 0 }, spawnEnemy: false);
        sim.Step(default, default, new ReactorCommand(Allocation: new PowerAllocation(100f, 0f, 0f)));
        var power = sim.World.Ship.Power;
        Near(power.PropulsionAvailable, 50f);
        Near(power.PropulsionAllocationPercent, 40f);
        sim.Step(default, default, new ReactorCommand(Allocation: new PowerAllocation(70f, 20f, 20f)));
        Near(power.PropulsionAllocationPercent, 40f);
        Near(power.ShieldsAllocationPercent, 0f);
        Near(power.WeaponsAllocationPercent, 0f);
    }),
    ("Thirty auxiliary PU are reserved before the main thruster receives power", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = PropulsionOnlyPower() with { ReactorRampSeconds = .001f }
        }, spawnEnemy: false);
        Step(sim, 180);
        sim.Step(new ShipCommand(MainThrust: true), default, new ReactorCommand(28f, new PowerAllocation(100f, 0f, 0f)));
        var power = sim.World.Ship.Power;
        Near(power.PropulsionAvailable, 35f);
        Near(power.PropulsionDraw, 35f);
        Near(power.AuxiliaryThrusterDraw, 30f);
        Near(power.MainThrusterDraw, 5f);
        Near(power.MainThrusterAvailable, 5f);
        Near(power.MainThrusterPowerFactor, .25f);
    }),
    ("Below thirty PU reverse and yaw thrusters scale linearly while main thrust is unavailable", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = PropulsionOnlyPower() with { ReactorRampSeconds = .001f }
        }, spawnEnemy: false);
        Step(sim, 180);
        sim.Step(new ShipCommand(MainThrust: true), default, new ReactorCommand(20f, new PowerAllocation(100f, 0f, 0f)));
        Near(sim.World.Ship.Power.AuxiliaryThrusterPowerFactor, 5f / 6f, .001f);
        Near(sim.World.Ship.Power.MainThrusterPowerFactor, 0f);
        Step(sim, 60, new ShipCommand(ReverseThrust: true));
        NearVector(sim.World.Ship.Velocity, new Vector3(0f, 0f, 5f), .01f);
        Step(sim, 60, new ShipCommand(YawLeft: true));
        Near(sim.World.Ship.AngularVelocity.Y, .5f, .01f);
    }),
    ("Auxiliary thrusters retain priority over the main thruster while inertia persists at zero", () =>
    {
        var half = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = PropulsionOnlyPower() with { DefaultReactorOperatingLevelPercent = 20f }
        }, spawnEnemy: false);
        half.Step(new ShipCommand(MainThrust: true), default, new ReactorCommand(Allocation: new PowerAllocation(100f, 0f, 0f)));
        Step(half, 689, new ShipCommand(MainThrust: true));
        Near(half.World.Ship.Velocity.Length(), 0f, 0.01f);
        Near(half.World.Ship.Power.AuxiliaryThrusterDraw, 25f, 0.01f);
        Near(half.World.Ship.Power.MainThrusterDraw, 0f, 0.01f);
        var zero = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = PropulsionOnlyPower() with { DefaultReactorOperatingLevelPercent = 0f }
        }, new ShipInitialState(Velocity: new Vector3(7f, 0f, 0f), YawRateRadiansPerSecond: .3f), spawnEnemy: false);
        Vector3 velocity = zero.World.Ship.Velocity;
        float angular = zero.World.Ship.AngularVelocity.Y;
        Step(zero, 60, new ShipCommand(MainThrust: true, YawLeft: true));
        NearVector(zero.World.Ship.Velocity, velocity, 0.001f);
        Near(zero.World.Ship.AngularVelocity.Y, angular, 0.001f);
    }),
    ("Armarium allocation controls weapon charging without changing station limits", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = new PowerSettings { DefaultReactorOperatingLevelPercent = 16f }
        }, spawnEnemy: false);
        sim.Step(default, default, new ReactorCommand(Allocation: new PowerAllocation(0f, 0f, 100f)));
        Step(sim, 358);
        Check(!sim.World.Lance.IsReady, "Half Armarium power must not charge a lance in under six seconds.");
        Step(sim, 1);
        Check(sim.World.Lance.IsReady, "Twenty allocated PU must charge the lance in six seconds.");
        var stopped = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = WeaponsOnlyPower() with { ReactorRampSeconds = .001f }
        }, spawnEnemy: false);
        Step(stopped, 90);
        float charge = stopped.World.Lance.ChargeFraction;
        stopped.Step(default, default, new ReactorCommand(0f));
        Step(stopped, 300);
        Near(stopped.World.Lance.ChargeFraction, charge, 0.0001f);
        stopped.Step(default, default, new ReactorCommand(100f));
        Step(stopped, 90);
        Check(stopped.World.Lance.IsReady, "Restored weapons power must continue from retained charge.");
    }),
    ("Reactor ramps from zero to full output in sixty simulation seconds", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = new PowerSettings { DefaultReactorOperatingLevelPercent = 0f }
        }, spawnEnemy: false);
        sim.Step(default, default, new ReactorCommand(100f));
        Near(sim.World.Ship.Reactor.OperatingLevelPercent, 100f / 3600f, .0001f);
        Step(sim, 3598);
        Check(sim.World.Ship.Reactor.OperatingLevelPercent < 100f, "Reactor must not reach full output before sixty seconds.");
        Step(sim, 1);
        Near(sim.World.Ship.Reactor.OperatingLevelPercent, 100f, .001f);
        Near(sim.World.Ship.Reactor.AvailablePower, 125f, .01f);
    }),
    ("Fuel depletion stops the authoritative reactor and all delivered power", () =>
    {
        var sim = new Simulation(new SimulationSettings
        {
            TargetCount = 0,
            Power = new PowerSettings { ReactorFuelCapacity = .01f }
        }, spawnEnemy: false);
        Step(sim, 60, new ShipCommand(MainThrust: true));
        Check(sim.World.Ship.Reactor.IsFuelDepleted, "The configured fuel reserve must deplete.");
        Near(sim.World.Ship.Reactor.AvailablePower, 0f);
        Near(sim.World.Ship.Reactor.OperatingLevelPercent, 0f);
        Near(sim.World.Ship.Power.CurrentDraw, 0f);
        Near(sim.World.Ship.Power.PropulsionDraw, 0f);
    }),
    ("A ready lance fires after weapons power is removed and still resets charge", () =>
    {
        var sim = WithTargets(new Vector3(0, 0, -300));
        Step(sim, 180);
        sim.Step(default, default, new ReactorCommand(0f));
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
            $"First enemy lance hit must deplete the player shield without GameOver (state {sim.World.CurrentEncounter.EnemyAi!.CurrentState}, distance {sim.World.CurrentEncounter.EnemyAi.LastContext.DistanceToPlayer:0}, closing {sim.World.CurrentEncounter.EnemyAi.LastContext.ClosingSpeed:0}, velocity {sim.World.CurrentEnemy!.Ship.Velocity.Length():0}, power {sim.World.CurrentEnemy!.Ship.Power.PropulsionDraw:0}, aim {MathF.Abs(sim.World.CurrentEncounter.EnemyAi.LastContext.EnemyAimError) * 180 / MathF.PI:0}).");
        Check(sim.Events.OfType<ShieldHit>().Any(hit => hit.TargetOwner == WeaponOwner.Player),
            "Player shield hit event is required.");
    }),
    ("Detected enemy requests every station maximum regardless of damage", () =>
    {
        var sim = CombatSimulation();
        JumpToCombat(sim);
        sim.Step(default);
        var enemy = sim.World.CurrentEnemy!;
        Check(enemy.Ship.Power.PropulsionRequested == enemy.Ship.Power.MaximumPropulsionDraw &&
              enemy.Ship.Power.WeaponsRequested == enemy.Ship.Power.MaximumWeaponsDraw &&
              enemy.Ship.Power.ShieldsRequested == enemy.Ship.Power.MaximumShieldsDraw,
            "Detected enemies must request full propulsion, weapons and shields without health-aware profiles.");
        Check(enemy.Ship.Shield.CurrentShield > 0f && enemy.Ship.Shield.CurrentShield <= enemy.Ship.Shield.MaximumShield,
            "The initially empty shield must begin normal recharge only after detection.");
    }),
    ("Residual damage removes hull and damages exactly one reproducible subsystem", () =>
    {
        var sim = CombatSimulation(power: CombatPower());
        JumpToCombat(sim);
        sim.Step(new ShipCommand(FireLance: true)); // enemy shield starts empty; first hit reaches hull
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
        EnterEncounter(sim, 1);
        Check(sim.World.Ship.Systems.PropulsionCondition == 1f && sim.World.Ship.Systems.WeaponsCondition == 1f &&
              sim.World.Ship.Systems.ShieldsCondition == 1f && sim.World.Ship.Shield.CurrentShield == sim.World.Ship.Shield.MaximumShield,
            "Successful warp must repair player systems and refill the shield.");
        Check(sim.World.CurrentEncounter.Id == 1 && hull == 1, "Warp must not repair stored enemy hull or alter encounter progress.");
    }),
    ("Power and condition define the speed limit without removing existing momentum", () =>
    {
        var sim = New();
        Step(sim, 3000, new ShipCommand(MainThrust: true));
        Near(sim.World.Ship.Velocity.Length(), 500f, 0.1f);
        sim.Step(default, default, new ReactorCommand(20f));
        Step(sim, 60, new ShipCommand(MainThrust: true));
        Check(sim.World.Ship.Velocity.Length() >= 499f, "Reducing power must not clamp existing velocity.");
        Step(sim, 60, new ShipCommand(ReverseThrust: true));
        Check(sim.World.Ship.Velocity.Length() < 499f, "Reverse thrust must brake while overspeed.");
    }),
    ("Bridge main throttle timings use the configured five-second rise and three-second fall", () =>
    {
        var power = new PowerSettings();
        Near(power.BridgeMainThrottleRiseSeconds, 5f);
        Near(power.BridgeMainThrottleFallSeconds, 3f);
    }),
    ("Difficulty profiles differ only by lance aim tolerance", () =>
    {
        var sim = new Simulation();
        var easy = sim.World.Encounters[1].EnemyAi!;
        var medium = sim.World.Encounters[2].EnemyAi!;
        var hard = sim.World.Encounters[3].EnemyAi!;
        Near(easy.FireAimToleranceRadians * 180f / MathF.PI, 2f);
        Near(medium.FireAimToleranceRadians * 180f / MathF.PI, 3f);
        Near(hard.FireAimToleranceRadians * 180f / MathF.PI, 5f);
    }),
    ("Invalid AI settings are rejected", () =>
    {
        bool rejected = false;
        try
        {
            _ = new Simulation(new SimulationSettings
            {
                EnemyAi = new EnemyAiSettings { MaximumCombatDistance = 500, MinimumCombatDistance = 600 }
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
        randomSeed: 42, enemyInitial: enemy ?? new ShipInitialState(new Vector3(0, 0, -1_000), YawRadians: MathF.PI), spawnEnemy: true);
static Simulation DetectionSimulation(float playerReactorPercent, float enemyDistance) =>
    new(new SimulationSettings
    {
        TargetCount = 0,
        Power = new PowerSettings { DefaultReactorOperatingLevelPercent = playerReactorPercent }
    }, enemyInitial: new ShipInitialState(new Vector3(0, 0, -enemyDistance), YawRadians: MathF.PI), spawnEnemy: true);
static Simulation ExplosionScenario(float distance) => CombatSimulation(
    enemy: new ShipInitialState(new Vector3(0, 0, -distance)),
    shield: new ShieldSettings { LanceDamage = 200f }, hull: new HullSettings { MaximumHull = 1 });
static PowerSettings PropulsionOnlyPower() => new();
static PowerSettings WeaponsOnlyPower() => new();
static PowerSettings CombatPower() => new();
static void JumpToCombat(Simulation sim)
{
    Step(sim, 600);
    EnterEncounter(sim, 3);
}
static void EnterEncounter(Simulation sim, int encounterId, Vector3? entryPosition = null)
{
    sim.Step(default, new NavigationCommand(EnterHyperspace: true));
    sim.Step(default, new NavigationCommand(encounterId));
    sim.Step(default, new NavigationCommand(EntryPosition: entryPosition ?? Vector3.Zero));
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
    var reactorCommands = new ReactoriumCommandBuffer();
    var sensoriumCommands = new SensoriumCommandBuffer();
    var assets = new Dictionary<string, string>
    {
        ["index.html"] = "<main>ARMARIUM</main>",
        ["armarium.css"] = "body{}",
        ["armarium.js"] = "",
        ["reactorium/index.html"] = "<main>REACTORIUM</main>",
        ["sensorium/index.html"] = "<main>SENSORIUM</main>",
        ["debug/index.html"] = "<main>ENEMY AI DEBUG</main>"
    };
    using var server = new StationServer(new StationServerOptions { Port = 0, StateUpdatesPerSecond = 30 }, assets, commands, reactorCommands, sensoriumCommands);
    server.UpdateState(new ArmariumState(true, -12.4f, 640f, 0.72f, false, 2.5f, 3, -12.4f, 32f, 40f, 1f, 42));
    server.UpdateReactoriumState(new ReactoriumState(75f, 70f, 87.5f, 125f, 50f, 100f, 100f, 3.5f,
        40f, 28f, 32f, 35f, 24.5f, 28f, 50f, 35f, 40f, 42));
    server.UpdateSensoriumState(new SensoriumState(
        [new SensoriumContact(2, "Argus-02", "ARGUS", "FRIGATE", -12.4f, 640f, .7f, .3f, .5f, .8f, 3, 3)], 42));
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
          MathF.Abs(state.RootElement.GetProperty("targetBearingDegrees").GetSingle() + 12.4f) < 0.001f &&
          MathF.Abs(state.RootElement.GetProperty("availablePower").GetSingle() - 32f) < 0.001f &&
          MathF.Abs(state.RootElement.GetProperty("maximumPower").GetSingle() - 40f) < 0.001f,
        "Station server must transmit only the current Armarium snapshot.");
    await SendWebSocketJsonAsync(socket, new { type = "fire_lance" });
    bool received = SpinWait.SpinUntil(() => commands.ReadCommand().FireLance, TimeSpan.FromSeconds(1));
    Check(received, "Station fire_lance must reach the thread-safe command buffer.");
    await SendWebSocketJsonAsync(socket, new { type = "turret", direction = "left", active = true });
    bool turretReceived = SpinWait.SpinUntil(() => commands.ReadCommand().AimLanceLeft, TimeSpan.FromSeconds(1));
    Check(turretReceived, "Station turret input must reach the thread-safe command buffer.");

    string reactorPage = await http.GetStringAsync(server.ReactoriumUrl);
    Check(reactorPage.Contains("REACTORIUM"), "Station server must serve the Reactorium page.");
    using var reactorSocket = new ClientWebSocket();
    await reactorSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/station"), CancellationToken.None);
    await SendWebSocketJsonAsync(reactorSocket, new { type = "hello", station = "reactorium", protocolVersion = StationProtocol.Version });
    using JsonDocument reactorWelcome = JsonDocument.Parse(await ReceiveWebSocketTextAsync(reactorSocket));
    Check(reactorWelcome.RootElement.GetProperty("station").GetString() == "reactorium" && server.IsReactoriumOnline,
        "Station server must accept a Reactorium handshake.");
    using JsonDocument reactorState = JsonDocument.Parse(await ReceiveWebSocketTextAsync(reactorSocket));
    Check(reactorState.RootElement.GetProperty("type").GetString() == "reactorium_state" &&
          MathF.Abs(reactorState.RootElement.GetProperty("outputPower").GetSingle() - 87.5f) < 0.001f &&
          MathF.Abs(reactorState.RootElement.GetProperty("targetOperatingLevelPercent").GetSingle() - 75f) < 0.001f,
        "Reactorium must receive only its own reactor snapshot.");
    await SendWebSocketJsonAsync(reactorSocket, new { type = "reactor_level", levelPercent = 40f });
    Check(SpinWait.SpinUntil(() => reactorCommands.TryReadOperatingLevel(out float level) && MathF.Abs(level - 40f) < 0.001f,
        TimeSpan.FromSeconds(1)), "Reactorium level commands must reach the simulation buffer.");
    await SendWebSocketJsonAsync(reactorSocket, new { type = "power_allocation", bridgePercent = 40f, shieldsPercent = 28f, armariumPercent = 32f });
    Check(SpinWait.SpinUntil(() => reactorCommands.TryReadAllocation(out PowerAllocation allocation) &&
        MathF.Abs(allocation.BridgePercent - 40f) < 0.001f && MathF.Abs(allocation.ShieldsPercent - 28f) < 0.001f &&
        MathF.Abs(allocation.ArmariumPercent - 32f) < 0.001f, TimeSpan.FromSeconds(1)),
        "Reactorium allocation commands must reach the simulation buffer atomically.");

    string sensoriumPage = await http.GetStringAsync(server.SensoriumUrl);
    Check(sensoriumPage.Contains("SENSORIUM"), "Station server must serve the Sensorium page.");
    using var sensoriumSocket = new ClientWebSocket();
    await sensoriumSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/station"), CancellationToken.None);
    await SendWebSocketJsonAsync(sensoriumSocket, new { type = "hello", station = "sensorium", protocolVersion = StationProtocol.Version });
    using JsonDocument sensoriumWelcome = JsonDocument.Parse(await ReceiveWebSocketTextAsync(sensoriumSocket));
    Check(sensoriumWelcome.RootElement.GetProperty("station").GetString() == "sensorium" && server.IsSensoriumOnline,
        "Station server must accept a read-only Sensorium handshake.");
    using JsonDocument sensoriumState = JsonDocument.Parse(await ReceiveWebSocketTextAsync(sensoriumSocket));
    Check(sensoriumState.RootElement.GetProperty("type").GetString() == "sensorium_state" &&
          sensoriumState.RootElement.GetProperty("contacts")[0].GetProperty("signatureCode").GetString() == "ARGUS" &&
          MathF.Abs(sensoriumState.RootElement.GetProperty("contacts")[0].GetProperty("distanceMeters").GetSingle() - 640f) < .001f &&
          !sensoriumState.RootElement.TryGetProperty("worldState", out _),
        "Sensorium must receive its compact contact telemetry without a complete world state.");
    await SendWebSocketJsonAsync(sensoriumSocket, new { type = "active_sonar" });
    Check(SpinWait.SpinUntil(() => sensoriumCommands.ReadCommand().ActiveSonarPing, TimeSpan.FromSeconds(1)),
        "Sensorium active-sonar commands must reach the simulation buffer as one-shot intents.");
    await SendWebSocketJsonAsync(sensoriumSocket, new { type = "identify_contact", enemyId = 2 });
    Check(SpinWait.SpinUntil(() => sensoriumCommands.ReadCommand().ConfirmedEnemyId == 2, TimeSpan.FromSeconds(1)),
        "A confirmed Sensorium signature must reach the simulation buffer with its contact identity.");

    server.UpdateEnemyDebugState(new EnemyDebugState(true, 2, "MEDIUM", true, "ATTACK",
        480f, 18f, 36f, 155f, 2, 3, 70f, 100f, 1f, .5f, 1f, .8f, false,
        100f, 75f, 93.75f, 50f, 98f, 100f, 40f, 35f, 0f, 40f, 32f, 0f, 42));
    string debugPage = await http.GetStringAsync(server.DebugUrl);
    Check(debugPage.Contains("ENEMY AI DEBUG"), "Station server must serve the enemy AI debug page.");
    using var debugSocket = new ClientWebSocket();
    await debugSocket.ConnectAsync(new Uri($"ws://127.0.0.1:{server.Port}/station"), CancellationToken.None);
    await SendWebSocketJsonAsync(debugSocket, new { type = "hello", station = "debug", protocolVersion = StationProtocol.Version });
    using JsonDocument debugWelcome = JsonDocument.Parse(await ReceiveWebSocketTextAsync(debugSocket));
    Check(debugWelcome.RootElement.GetProperty("station").GetString() == "debug" && server.IsDebugOnline,
        "Station server must accept the read-only debug station handshake.");
    using JsonDocument debugState = JsonDocument.Parse(await ReceiveWebSocketTextAsync(debugSocket));
    Check(debugState.RootElement.GetProperty("type").GetString() == "enemy_debug_state" &&
          debugState.RootElement.GetProperty("enemyAvailable").GetBoolean() &&
          debugState.RootElement.GetProperty("aiState").GetString() == "ATTACK" &&
          MathF.Abs(debugState.RootElement.GetProperty("distanceToPlayer").GetSingle() - 480f) < .001f &&
          debugState.RootElement.TryGetProperty("enemyFireControl", out JsonElement enemyFireControl) &&
          enemyFireControl.TryGetProperty("rayWouldHit", out _) &&
          debugState.RootElement.TryGetProperty("playerFireControl", out JsonElement playerFireControl) &&
          playerFireControl.TryGetProperty("aimToleranceDegrees", out _) &&
          !debugState.RootElement.TryGetProperty("worldState", out _),
        "Debug station must receive its focused AI telemetry rather than a complete world snapshot.");
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
