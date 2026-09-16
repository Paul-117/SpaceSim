using System.Numerics;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;

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
    ("There is no artificial speed cap", () =>
    {
        var sim = New();
        Step(sim, 3600, new ShipCommand(MainThrust: true));
        Near(sim.World.Ship.Velocity.Length(), 720f, 0.05f);
    }),
    ("Lance needs exactly three seconds to charge", () =>
    {
        var sim = New();
        Check(!sim.World.Lance.IsReady, "Lance starts empty.");
        Step(sim, 179);
        Check(!sim.World.Lance.IsReady, "Lance must not charge early.");
        Step(sim, 1);
        Check(sim.World.Lance.IsReady, "Lance must be ready after 180 ticks.");
    }),
    ("Early fire is ignored and never queued", () =>
    {
        var sim = New();
        sim.Step(new ShipCommand(FireLance: true));
        Check(!sim.Events.OfType<WeaponFired>().Any(), "Early shot must be rejected.");
        Step(sim, 179);
        Check(sim.World.Lance.IsReady, "Rejected request must not fire later.");
        Check(!sim.Events.OfType<WeaponFired>().Any(), "No delayed shot.");
    }),
    ("Shot resets charge, emits once, and recharges", () =>
    {
        var sim = New();
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
        var sim = new Simulation(new SimulationSettings { TargetCount = 1 },
            new ShipInitialState(YawRadians: MathF.PI / 2),
            initialTargets: new[] { new Vector3(-300, 0, 0) });
        Fire(sim);
        Check(sim.World.HitCount == 1, "Rotated weapon ray must hit.");
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
    ("Two encounters start with ten and fifteen distinct targets", () =>
    {
        var sim = new Simulation();
        Check(sim.World.CurrentEncounter.Id == 1, "Start in encounter 1.");
        Check(sim.World.Encounters.Count == 2, "There must be two destinations.");
        Check(sim.World.Targets.Count == 10 && sim.World.Encounters[1].Targets.Count == 15, "Wrong populations.");
        Check(sim.World.Encounters.SelectMany(e => e.Targets).Select(t => t.Id).Distinct().Count() == 25,
            "Target IDs must be unique across encounters.");
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
        var sim = New(new ShipInitialState(Position: new Vector3(100, 0, 50), Velocity: new Vector3(10, 0, -3),
            YawRadians: 1, YawRateRadiansPerSecond: 0.4f));
        Step(sim, 600);
        sim.Step(default, new NavigationCommand(2));
        Check(sim.World.CurrentEncounter.Id == 2 && sim.World.Targets.Count == 15, "Wrong destination.");
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
        var sim = New(new ShipInitialState(Velocity: new Vector3(12, 0, 0), YawRateRadiansPerSecond: 0.2f));
        Step(sim, 600);
        NearVector(sim.World.Ship.Position, new Vector3(120, 0, 0), 0.002f);
        Check(sim.World.Lance.IsReady && sim.World.WarpDrive.IsReady, "Both systems must charge while moving.");
        Near(sim.World.Ship.AngularVelocity.Y, 0.2f);
        Check(sim.World.Tick == 600, "World time must advance.");
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
    new(new SimulationSettings { TargetCount = 0 }, initial);
static Simulation WithTargets(params Vector3[] targets) =>
    new(new SimulationSettings { TargetCount = targets.Length }, initialTargets: targets);
static void Fire(Simulation sim)
{
    Step(sim, 180);
    sim.Step(new ShipCommand(FireLance: true));
}
static void Step(Simulation simulation, int count, ShipCommand command = default)
{
    for (int i = 0; i < count; i++) simulation.Step(command);
}
static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
static void Near(float actual, float expected, float epsilon = 0.0001f) =>
    Check(MathF.Abs(actual - expected) <= epsilon, $"Expected {expected}, got {actual}.");
static void NearVector(Vector3 actual, Vector3 expected, float epsilon = 0.0001f) =>
    Check(Vector3.Distance(actual, expected) <= epsilon, $"Expected {expected}, got {actual}.");
