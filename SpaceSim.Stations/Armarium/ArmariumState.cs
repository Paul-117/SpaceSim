using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Armarium;

/// <summary>Small, station-specific snapshot. It deliberately does not expose the world state.</summary>
public sealed record ArmariumState(
    bool TargetAvailable,
    float TargetBearingDegrees,
    float TargetDistanceMeters,
    float LanceCharge,
    bool LanceReady,
    float LanceTurretAngleDegrees,
    long TargetHitSequence,
    float LastTargetHitBearingDegrees,
    long SimulationTick);

public static class ArmariumStateBuilder
{
    public static ArmariumState Build(WorldState world, long targetHitSequence = 0,
        float lastTargetHitBearingDegrees = 0f)
    {
        var enemy = world.CurrentEnemy;
        bool hasTarget = enemy is not null;
        float bearing = hasTarget ? CalculateTargetBearingDegrees(world.Ship.Position, world.Ship.Forward,
            enemy!.Ship.Position) : 0f;
        float distance = hasTarget ? CalculateHorizontalDistanceMeters(world.Ship.Position, enemy!.Ship.Position) : 0f;
        return new ArmariumState(hasTarget, bearing, distance, world.Lance.ChargeFraction, world.Lance.IsReady,
            world.LanceAim.YawOffsetDegrees, targetHitSequence, lastTargetHitBearingDegrees, world.Tick);
    }

    /// <summary>Horizontal distance for the tactical map; vertical separation is not represented there.</summary>
    public static float CalculateHorizontalDistanceMeters(Vector3 shipPosition, Vector3 targetPosition)
    {
        Vector3 delta = targetPosition - shipPosition;
        delta.Y = 0f;
        return delta.Length();
    }

    /// <summary>Returns a normalized horizontal bearing: negative is port/left, positive is starboard/right.</summary>
    public static float CalculateTargetBearingDegrees(Vector3 shipPosition, Vector3 shipForward, Vector3 targetPosition)
    {
        Vector3 forward = new(shipForward.X, 0f, shipForward.Z);
        Vector3 target = targetPosition - shipPosition;
        target.Y = 0f;
        if (forward.LengthSquared() < 0.000001f || target.LengthSquared() < 0.000001f) return 0f;
        forward = Vector3.Normalize(forward);
        target = Vector3.Normalize(target);
        float radians = MathF.Atan2(-Vector3.Cross(forward, target).Y, Vector3.Dot(forward, target));
        // Behind is represented consistently as +180 degrees instead of a signed-zero-dependent result.
        if (MathF.Abs(MathF.Abs(radians) - MathF.PI) < 0.00001f) radians = MathF.PI;
        return radians * 180f / MathF.PI;
    }
}
