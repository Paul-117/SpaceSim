using System.Numerics;

namespace SpaceSim.Core.Simulation;

public abstract record SimulationEvent;
public enum WeaponOwner { Player, Enemy }
public enum WeaponHitKind { None, Target, Player, Enemy }
public sealed record WeaponFired(Vector3 Origin, Vector3 End, int? TargetId,
    WeaponOwner Owner = WeaponOwner.Player, WeaponHitKind HitKind = WeaponHitKind.None) : SimulationEvent;
public sealed record TargetHit(int TargetId, Vector3 Position) : SimulationEvent;
public sealed record TargetSpawned(int TargetId, Vector3 Position, int EncounterId) : SimulationEvent;
public sealed record EncounterChanged(int FromEncounterId, int ToEncounterId) : SimulationEvent;
public sealed record EnemyDestroyed(int EnemyId, Vector3 Position) : SimulationEvent;
public sealed record PlayerDestroyed(int EnemyId, Vector3 Position) : SimulationEvent;
