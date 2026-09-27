using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Armarium;

/// <summary>Small, station-specific snapshot. It deliberately does not expose the world state.</summary>
public sealed record ArmariumState(
    bool TargetAvailable,
    float TargetBearingDegrees,
    float LanceCharge,
    bool LanceReady,
    bool ArmariumControlsActive,
    long SimulationTick);

public static class ArmariumStateBuilder
{
    public static ArmariumState Build(WorldState world, bool armariumControlsActive = false)
    {
        var enemy = world.CurrentEnemy;
        bool hasTarget = enemy is not null;
        float bearing = hasTarget ? CalculateTargetBearingDegrees(world.Ship.Position, world.Ship.Forward,
            enemy!.Ship.Position) : 0f;
        return new ArmariumState(hasTarget, bearing, world.Lance.ChargeFraction, world.Lance.IsReady,
            armariumControlsActive, world.Tick);
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
