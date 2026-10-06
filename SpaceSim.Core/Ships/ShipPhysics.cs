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
        float force = (ship.Tuning.MainThrustNewtons * mainPowerFactor * mainInput)
                    - (ship.Tuning.ReverseThrustNewtons * auxiliaryPowerFactor * reverseInput);
        // Positive rotation about +Y turns the nose (-Z) left in the X/Z view.
        float angularAcceleration = (leftInput - rightInput) * ship.Tuning.YawTorqueNewtonMeters * auxiliaryPowerFactor /
            ship.YawMomentOfInertia;
        angularAcceleration = LimitAngularAcceleration(ship.AngularVelocity.Y, angularAcceleration,
            ship.Tuning.MaximumYawAngularVelocityRadiansPerSecond, dt);
        Vector3 acceleration = ship.Forward * (force / ship.MassKg);
        float speedLimit = ship.Tuning.MaximumForwardSpeedMetersPerSecond * mainPowerFactor;
        if (mainInput > 0f && Vector3.Dot(ship.Velocity, acceleration) > 0f)
            acceleration = LimitAcceleration(ship.Velocity, acceleration, speedLimit, dt);
        float reverseSpeedLimit = ship.Tuning.MaximumReverseSpeedMetersPerSecond * auxiliaryPowerFactor;
        if (reverseInput > 0f && Vector3.Dot(ship.Velocity, acceleration) > 0f)
            acceleration = LimitAcceleration(ship.Velocity, acceleration, reverseSpeedLimit, dt);
        ship.Velocity += acceleration * dt;
        ship.AngularVelocity += Vector3.UnitY * (angularAcceleration * dt);

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

    private static Vector3 LimitAcceleration(Vector3 velocity, Vector3 acceleration, float speedLimit, float dt)
    {
        if (velocity.Length() >= speedLimit) return Vector3.Zero;
        Vector3 nextVelocity = velocity + acceleration * dt;
        if (nextVelocity.Length() <= speedLimit) return acceleration;
        return (Vector3.Normalize(nextVelocity) * speedLimit - velocity) / dt;
    }

    private static float LimitAngularAcceleration(float angularVelocity, float angularAcceleration, float limit, float dt)
    {
        if (angularAcceleration == 0f) return 0f;
        float nextVelocity = angularVelocity + angularAcceleration * dt;
        bool increasesMagnitude = MathF.Abs(nextVelocity) > MathF.Abs(angularVelocity);
        if (!increasesMagnitude) return angularAcceleration;
        if (MathF.Abs(angularVelocity) >= limit) return 0f;
        if (MathF.Abs(nextVelocity) <= limit) return angularAcceleration;
        return (MathF.CopySign(limit, nextVelocity) - angularVelocity) / dt;
    }
}
