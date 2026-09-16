using System.Numerics;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.Navigation;

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
        int randomSeed = 42, IEnumerable<Vector3>? initialTargets = null)
    {
        Settings = settings ?? new SimulationSettings();
        Settings.Validate();
        Events = _events.AsReadOnly();
        ValidateInitial(initialShip);
        var encounters = new[]
        {
            new EncounterState(1, "Encounter 1", Settings.TargetCount),
            new EncounterState(2, "Encounter 2", Settings.EncounterTwoTargetCount)
        };
        World = new WorldState(new ShipState(Settings.ShipMassKg, Settings.YawMomentOfInertia)
        {
            Position = initialShip.Position,
            Velocity = initialShip.Velocity,
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, initialShip.YawRadians),
            AngularVelocity = Vector3.UnitY * initialShip.YawRateRadiansPerSecond
        }, encounters);
        World.WarpDrive.RemainingSeconds = Settings.WarpChargeSeconds;
        var targets = new TargetSystem(Settings, randomSeed);
        targets.Initialize(encounters[0], initialShip.Position, _events, initialTargets);
        targets.Initialize(encounters[1], Vector3.Zero, _events);
    }

    public void Step(ShipCommand command, NavigationCommand navigation = default)
    {
        _events.Clear();
        ShipPhysics.Step(World.Ship, command, Settings);
        LanceSystem.Step(World, command.FireLance, Settings, _events);
        WarpDriveSystem.Step(World, navigation, Settings, _events);
        World.Tick++;
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
