using System.Numerics;
using SpaceSim.Core.Combat;

namespace SpaceSim.Core.Simulation;

public abstract record SimulationEvent;
public enum WeaponOwner { Player, Enemy }
public enum WeaponHitKind { None, Target, Player, Enemy }
public sealed record WeaponFired(Vector3 Origin, Vector3 End, int? TargetId,
    WeaponOwner Owner = WeaponOwner.Player, WeaponHitKind HitKind = WeaponHitKind.None,
    Vector3? VisualFadeStart = null, Vector3? VisualEnd = null) : SimulationEvent;
public sealed record TargetHit(int TargetId, Vector3 Position) : SimulationEvent;
public sealed record TargetSpawned(int TargetId, Vector3 Position, int EncounterId) : SimulationEvent;
public sealed record EncounterChanged(int FromEncounterId, int ToEncounterId) : SimulationEvent;
public sealed record EnteredHyperspace(int FromEncounterId) : SimulationEvent;
public sealed record EnemyDestroyed(int EnemyId, Vector3 Position) : SimulationEvent;
public sealed record PlayerDestroyed(int EnemyId, Vector3 Position) : SimulationEvent;
public sealed record ShipCollision(int EnemyId, Vector3 Position) : SimulationEvent;
public sealed record EnemyExplosion(int EnemyId, Vector3 Position, float DistanceToPlayer) : SimulationEvent;
public sealed record ShieldHit(WeaponOwner TargetOwner, int? TargetEnemyId, float Damage,
    float ShieldBefore, float ShieldAfter, Vector3 Position) : SimulationEvent;
public sealed record ShieldDepleted(WeaponOwner TargetOwner, int? TargetEnemyId, Vector3 Position) : SimulationEvent;
public sealed record HullDamaged(WeaponOwner TargetOwner, int? TargetEnemyId, float HullBefore, float HullAfter, Vector3 Position) : SimulationEvent;
public sealed record SubsystemDamaged(WeaponOwner TargetOwner, int? TargetEnemyId, ShipSubsystem Subsystem,
    float ConditionBefore, float ConditionAfter, Vector3 Position) : SimulationEvent;
public sealed record SubsystemDisabled(WeaponOwner TargetOwner, int? TargetEnemyId, ShipSubsystem Subsystem, Vector3 Position) : SimulationEvent;
public sealed record SystemsRepaired(WeaponOwner TargetOwner, int? TargetEnemyId, Vector3 Position) : SimulationEvent;
