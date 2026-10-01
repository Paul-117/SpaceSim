using System.Numerics;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Debug;

/// <summary>
/// Read-only telemetry for the active enemy. This deliberately exposes a focused debug view,
/// rather than serialising the authoritative world state to a browser.
/// </summary>
public sealed record EnemyDebugState(
    bool EnemyAvailable,
    int EnemyId,
    string Difficulty,
    bool PlayerDetected,
    string AiState,
    float DistanceToPlayer,
    float ClosingSpeed,
    float RelativeSpeed,
    float EnemySpeed,
    int Hull,
    int MaximumHull,
    float Shield,
    float MaximumShield,
    float PropulsionCondition,
    float WeaponsCondition,
    float ShieldsCondition,
    float LanceCharge,
    bool LanceReady,
    float ReactorTargetOperatingLevelPercent,
    float ReactorOperatingLevelPercent,
    float ReactorAvailablePower,
    float ReactorCurrentDraw,
    float ReactorFuel,
    float ReactorFuelCapacity,
    float PropulsionRequested,
    float WeaponsRequested,
    float ShieldsRequested,
    float PropulsionDraw,
    float WeaponsDraw,
    float ShieldsDraw,
    long SimulationTick,
    FireControlDebug? EnemyFireControl = null,
    FireControlDebug? PlayerFireControl = null);

/// <summary>
/// Focused, read-only explanation of the live lance gates. It deliberately distinguishes
/// the AI's fire-command gates from the final ray/sphere hit check.
/// </summary>
public sealed record FireControlDebug(
    bool HasTarget,
    bool CombatActive,
    bool AttackState,
    bool LanceReady,
    bool TargetInRange,
    bool TargetInFront,
    float AimErrorDegrees,
    float AimToleranceDegrees,
    bool AimWithinTolerance,
    bool RayWouldHit,
    bool FireCommandWouldBeIssued)
{
    public static FireControlDebug Unavailable { get; } = new(
        false, false, false, false, false, false, 0f, 0f, false, false, false);
}

public static class EnemyDebugStateBuilder
{
    public static EnemyDebugState Build(WorldState world, SimulationSettings? settings = null)
    {
        var enemy = world.CurrentEnemy;
        if (enemy is null) return Empty(world.Tick);

        EnemyAiController? ai = world.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
        var ship = enemy.Ship;
        var context = ai?.LastContext ?? default;
        var power = ship.Power;
        var reactor = ship.Reactor;
        return new EnemyDebugState(
            true,
            enemy.EnemyId,
            enemy.Difficulty.ToString().ToUpperInvariant(),
            ai?.IsPlayerDetected ?? false,
            ai?.CurrentState.ToString().ToUpperInvariant() ?? "UNAVAILABLE",
            context.DistanceToPlayer,
            context.ClosingSpeed,
            context.RelativeVelocity.Length(),
            ship.Velocity.Length(),
            ship.Hull.CurrentHull,
            ship.Hull.MaximumHull,
            ship.Shield.CurrentShield,
            ship.Shield.MaximumShield,
            ship.Systems.PropulsionCondition,
            ship.Systems.WeaponsCondition,
            ship.Systems.ShieldsCondition,
            enemy.Lance.ChargeFraction,
            enemy.Lance.IsReady,
            reactor.TargetOperatingLevelPercent,
            reactor.OperatingLevelPercent,
            reactor.AvailablePower,
            reactor.CurrentDraw,
            reactor.Fuel,
            reactor.FuelCapacity,
            power.PropulsionRequested,
            power.WeaponsRequested,
            power.ShieldsRequested,
            power.PropulsionDraw,
            power.WeaponsDraw,
            power.ShieldsDraw,
            world.Tick,
            BuildEnemyFireControl(world, enemy, ai, settings),
            BuildPlayerFireControl(world, enemy, settings));
    }

    private static FireControlDebug BuildEnemyFireControl(WorldState world, EnemyShipState enemy,
        EnemyAiController? ai, SimulationSettings? settings)
    {
        Vector3 toPlayer = world.Ship.Position - enemy.Ship.Position;
        float distance = toPlayer.Length();
        Vector3 direction = distance > .0001f ? toPlayer / distance : enemy.Ship.Forward;
        float aimErrorDegrees = Degrees(SignedPlanarAngle(enemy.Ship.Forward, direction));
        float toleranceDegrees = Degrees(ai?.FireAimToleranceRadians ?? 0f);
        bool inRange = distance <= (settings?.LanceRangeMeters ?? 1000f);
        bool inFront = Vector3.Dot(enemy.Ship.Forward, direction) > 0f;
        bool aimed = MathF.Abs(aimErrorDegrees) <= toleranceDegrees + .0001f;
        bool attackState = ai?.CurrentState == EnemyAiState.Attack;
        bool combatActive = ai?.IsPlayerDetected ?? false;
        bool rayWouldHit = RayWouldHit(enemy.Ship.Position, enemy.Ship.Forward, world.Ship.Position,
            settings?.EnemyAi.ShipHitRadiusMeters ?? 16f, settings?.LanceRangeMeters ?? 1000f);
        bool command = combatActive && attackState && enemy.Lance.IsReady && inRange && inFront && aimed;
        return new FireControlDebug(true, combatActive, attackState, enemy.Lance.IsReady, inRange, inFront,
            aimErrorDegrees, toleranceDegrees, aimed, rayWouldHit, command);
    }

    private static FireControlDebug BuildPlayerFireControl(WorldState world, EnemyShipState enemy,
        SimulationSettings? settings)
    {
        Vector3 toEnemy = enemy.Ship.Position - world.Ship.Position;
        float distance = toEnemy.Length();
        Vector3 direction = distance > .0001f ? toEnemy / distance : world.LanceDirection;
        float aimErrorDegrees = Degrees(SignedPlanarAngle(world.LanceDirection, direction));
        const float autopilotToleranceDegrees = AutopilotController.AimToleranceDegrees;
        bool inRange = distance <= (settings?.LanceRangeMeters ?? 1000f);
        bool inFront = Vector3.Dot(world.LanceDirection, direction) > 0f;
        bool aimed = MathF.Abs(aimErrorDegrees) <= autopilotToleranceDegrees + .0001f;
        bool rayWouldHit = RayWouldHit(world.Ship.Position, world.LanceDirection, enemy.Ship.Position,
            settings?.EnemyAi.ShipHitRadiusMeters ?? 16f, settings?.LanceRangeMeters ?? 1000f);
        // The bridge can deliberately fire at any time. The other gates describe whether it will hit this contact.
        return new FireControlDebug(true, true, false, world.Lance.IsReady, inRange, inFront,
            aimErrorDegrees, autopilotToleranceDegrees, aimed, rayWouldHit, world.Lance.IsReady);
    }

    private static bool RayWouldHit(Vector3 origin, Vector3 direction, Vector3 center, float radius, float range)
    {
        Vector3 normalizedDirection = Vector3.Normalize(direction);
        Vector3 offset = center - origin;
        float alongRay = Vector3.Dot(offset, normalizedDirection);
        if (alongRay < 0f) return false;
        float perpendicularSquared = MathF.Max(0f, offset.LengthSquared() - alongRay * alongRay);
        float radiusSquared = radius * radius;
        if (perpendicularSquared > radiusSquared) return false;
        return alongRay - MathF.Sqrt(radiusSquared - perpendicularSquared) <= range;
    }

    private static float SignedPlanarAngle(Vector3 from, Vector3 to)
    {
        Vector3 a = Vector3.Normalize(new Vector3(from.X, 0f, from.Z));
        Vector3 b = Vector3.Normalize(new Vector3(to.X, 0f, to.Z));
        return MathF.Atan2(Vector3.Cross(a, b).Y, Vector3.Dot(a, b));
    }

    private static float Degrees(float radians) => radians * 180f / MathF.PI;

    private static EnemyDebugState Empty(long tick) => new(
        false, 0, "NONE", false, "NONE", 0f, 0f, 0f, 0f,
        0, 0, 0f, 0f, 0f, 0f, 0f, 0f, false,
        0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, tick,
        FireControlDebug.Unavailable, FireControlDebug.Unavailable);
}
