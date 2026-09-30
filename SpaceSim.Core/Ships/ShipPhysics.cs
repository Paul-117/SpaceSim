using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Ships;

internal static class ShipPhysics
{
    public static void Step(ShipState ship, ShipCommand command, SimulationSettings settings)
    {
        const float dt = SimulationSettings.FixedDeltaSeconds;
        float mainPowerFactor = ship.Power.MainThrusterPowerFactor * ship.Systems.PropulsionCondition;
        float auxiliaryPowerFactor = ship.Power.AuxiliaryThrusterPowerFactor * ship.Systems.PropulsionCondition;
        float mainInput = command.MainThrust ? Intensity(command.MainThrustIntensity) : 0f;
        float reverseInput = command.ReverseThrust ? Intensity(command.ReverseThrustIntensity) : 0f;
        float leftInput = command.YawLeft ? Intensity(command.YawIntensity) : 0f;
        float rightInput = command.YawRight ? Intensity(command.YawIntensity) : 0f;
        float force = (settings.MainThrustNewtons * mainPowerFactor * mainInput)
                    - (settings.ReverseThrustNewtons * auxiliaryPowerFactor * reverseInput);
        // Positive rotation about +Y turns the nose (-Z) left in the X/Z view.
        float torque = (leftInput - rightInput) * settings.YawTorqueNewtonMeters * auxiliaryPowerFactor;
        Vector3 acceleration = ship.Forward * (force / ship.MassKg);
        float speedLimit = settings.MaximumNominalSpeedMetersPerSecond * mainPowerFactor;
        if (mainInput > 0f && Vector3.Dot(ship.Velocity, acceleration) > 0f && ship.Velocity.Length() >= speedLimit)
            acceleration = Vector3.Zero;
        ship.Velocity += acceleration * dt;
        ship.AngularVelocity += Vector3.UnitY * (torque / ship.YawMomentOfInertia * dt);

        // Semi-implicit Euler, deliberately without damping or a speed limit.
        ship.Position += ship.Velocity * dt;
        ship.Rotation = Quaternion.Normalize(
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, ship.AngularVelocity.Y * dt) * ship.Rotation);

        // Current planar rule, not a restriction of ShipState's data model.
        ship.Position = new Vector3(ship.Position.X, 0f, ship.Position.Z);
        ship.Velocity = new Vector3(ship.Velocity.X, 0f, ship.Velocity.Z);
        ship.AngularVelocity = new Vector3(0f, ship.AngularVelocity.Y, 0f);
    }

    private static float Intensity(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
}
