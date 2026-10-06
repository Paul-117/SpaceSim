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
    float LineOfSightAngularVelocity,
    float EnemyAimError,
    float PlayerAimError,
    float EnemyLanceCharge,
    float PlayerLanceCharge)
{
    internal static EnemyAiContext Create(ShipState target, LanceState targetLance, EnemyShipState enemy) =>
        Create(target, targetLance, enemy.Ship, enemy.Lance);

    /// <summary>Builds the same relative combat context for any two ships.
    /// It is used by the symmetric 1VS1 simulator as well as enemy AI.</summary>
    internal static EnemyAiContext Create(ShipState target, LanceState targetLance, ShipState controlledShip, LanceState controlledLance)
    {
        Vector3 relativePosition = target.Position - controlledShip.Position;
        float distance = relativePosition.Length();
        Vector3 direction = distance > 0.0001f ? relativePosition / distance : controlledShip.Forward;
        Vector3 relativeVelocity = target.Velocity - controlledShip.Velocity;
        float closingSpeed = -Vector3.Dot(relativeVelocity, direction);
        float lineOfSightAngularVelocity = distance > 0.0001f
            ? Vector3.Cross(relativePosition, relativeVelocity).Y / (distance * distance)
            : 0f;
        return new EnemyAiContext(relativePosition, direction, relativeVelocity, distance, closingSpeed,
            lineOfSightAngularVelocity,
            SignedPlanarAngle(controlledShip.Forward, direction),
            SignedPlanarAngle(target.Forward, -direction),
            controlledLance.ChargeFraction, targetLance.ChargeFraction);
    }

    internal static float SignedPlanarAngle(Vector3 from, Vector3 to)
    {
        Vector3 a = Vector3.Normalize(new Vector3(from.X, 0, from.Z));
        Vector3 b = Vector3.Normalize(new Vector3(to.X, 0, to.Z));
        return MathF.Atan2(Vector3.Cross(a, b).Y, Vector3.Dot(a, b));
    }
}
