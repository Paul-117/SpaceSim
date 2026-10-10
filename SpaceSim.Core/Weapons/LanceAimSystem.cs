using System.Numerics;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Weapons;

/// <summary>Applies normal command intent to the limited player lance mount.</summary>
public static class LanceAimSystem
{
    public static void Step(LanceAimState aim, ShipCommand command, BowWeaponDefinition weapon)
    {
        float input = (command.AimLanceRight ? 1f : 0f) - (command.AimLanceLeft ? 1f : 0f);
        float next = aim.YawOffsetDegrees + input * weapon.TurretDegreesPerSecond * SimulationSettings.FixedDeltaSeconds;
        aim.YawOffsetDegrees = Math.Clamp(next, -weapon.TurretMaximumAngleDegrees,
            weapon.TurretMaximumAngleDegrees);
    }

    /// <summary>Returns the planar lance direction. Positive mount offset is starboard/right.</summary>
    public static Vector3 GetDirection(Vector3 shipForward, float yawOffsetDegrees)
    {
        float radians = -yawOffsetDegrees * MathF.PI / 180f;
        return Vector3.Normalize(Vector3.Transform(shipForward,
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, radians)));
    }
}
