using System.Numerics;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.AI;

/// <summary>Derived once per AI tick; it is not authoritative persistent state.</summary>
public readonly record struct EnemyAiContext(
    Vector3 RelativePosition,
    Vector3 DirectionToPlayer,
    Vector3 RelativeVelocity,
    float DistanceToPlayer,
    float ClosingSpeed,
    float EnemyAimError,
    float PlayerAimError,
    float EnemyLanceCharge,
    float PlayerLanceCharge)
{
    internal static EnemyAiContext Create(ShipState player, LanceState playerLance, EnemyShipState enemy)
    {
        Vector3 relativePosition = player.Position - enemy.Ship.Position;
        float distance = relativePosition.Length();
        Vector3 direction = distance > 0.0001f ? relativePosition / distance : enemy.Ship.Forward;
        Vector3 relativeVelocity = player.Velocity - enemy.Ship.Velocity;
        float closingSpeed = -Vector3.Dot(relativeVelocity, direction);
        return new EnemyAiContext(relativePosition, direction, relativeVelocity, distance, closingSpeed,
            SignedPlanarAngle(enemy.Ship.Forward, direction),
            SignedPlanarAngle(player.Forward, -direction),
            enemy.Lance.ChargeFraction, playerLance.ChargeFraction);
    }

    internal static float SignedPlanarAngle(Vector3 from, Vector3 to)
    {
        Vector3 a = Vector3.Normalize(new Vector3(from.X, 0, from.Z));
        Vector3 b = Vector3.Normalize(new Vector3(to.X, 0, to.Z));
        return MathF.Atan2(Vector3.Cross(a, b).Y, Vector3.Dot(a, b));
    }
}

