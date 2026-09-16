using System.Numerics;

namespace SpaceSim.Core.Simulation;

public abstract record SimulationEvent;
public sealed record WeaponFired(Vector3 Origin, Vector3 End, int? TargetId) : SimulationEvent;
public sealed record TargetHit(int TargetId, Vector3 Position) : SimulationEvent;
public sealed record TargetSpawned(int TargetId, Vector3 Position) : SimulationEvent;
public sealed record TargetDespawned(int TargetId) : SimulationEvent;
