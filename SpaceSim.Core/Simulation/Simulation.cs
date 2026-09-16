using System.Numerics;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.Simulation;

/// <summary>Authoritative, engine-independent simulation, advanced exactly one fixed tick at a time.</summary>
public sealed class Simulation
{
    public SimulationSettings Settings { get; }
    public WorldState World { get; }
    private readonly List<SimulationEvent> _events = new();
    private readonly TargetSystem _targets;
    /// <summary>Events for the last completed tick (or initial spawns). Read before the next Step.</summary>
    public IReadOnlyList<SimulationEvent> Events { get; }

    public Simulation(SimulationSettings? settings = null, ShipInitialState initialShip = default,
        int randomSeed = 42, IEnumerable<Vector3>? initialTargets = null)
    {
        Settings = settings ?? new SimulationSettings();
        Settings.Validate();
        Events = _events.AsReadOnly();
        _targets = new TargetSystem(Settings, randomSeed);
        ValidateInitial(initialShip);
        World = new WorldState(new ShipState(Settings.ShipMassKg, Settings.YawMomentOfInertia)
        {
            Position = initialShip.Position,
            Velocity = initialShip.Velocity,
            Rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, initialShip.YawRadians),
            AngularVelocity = Vector3.UnitY * initialShip.YawRateRadiansPerSecond
        });
        if (initialTargets is not null)
        {
            var positions = initialTargets.ToArray();
            if (positions.Length != Settings.TargetCount)
                throw new ArgumentException("Initial target count must match TargetCount.", nameof(initialTargets));
            foreach (var position in positions) _targets.Add(World, position, _events);
        }
        _targets.MaintainPopulation(World, _events);
    }

    public void Step(ShipCommand command)
    {
        _events.Clear();
        ShipPhysics.Step(World.Ship, command, Settings);
        LanceSystem.Step(World, command.FireLance, Settings, _events);
        _targets.MaintainPopulation(World, _events);
        World.Tick++;
    }

    private static void ValidateInitial(ShipInitialState initial)
    {
        static bool Finite(Vector3 v) => float.IsFinite(v.X) && float.IsFinite(v.Y) && float.IsFinite(v.Z);
        if (!Finite(initial.Position) || !Finite(initial.Velocity) ||
            !float.IsFinite(initial.YawRadians) || !float.IsFinite(initial.YawRateRadiansPerSecond) ||
            initial.Position.Y != 0f || initial.Velocity.Y != 0f)
            throw new ArgumentException("Version 1.0 requires finite initial state in the X/Z plane.", nameof(initial));
    }
}
