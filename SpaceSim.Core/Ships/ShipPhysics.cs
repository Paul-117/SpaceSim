using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Ships;

internal static class ShipPhysics
{
    public static void Step(ShipState ship, ShipCommand command, SimulationSettings settings)
    {
        const float dt = SimulationSettings.FixedDeltaSeconds;
        float powerFactor = ship.Power.PropulsionPowerFactor;
        float force = (command.MainThrust ? settings.MainThrustNewtons * powerFactor : 0f)
                    - (command.ReverseThrust ? settings.ReverseThrustNewtons * powerFactor : 0f);
        // Positive rotation about +Y turns the nose (-Z) left in the X/Z view.
        float torque = ((command.YawLeft ? 1f : 0f) - (command.YawRight ? 1f : 0f))
                       * settings.YawTorqueNewtonMeters * powerFactor;
        Vector3 acceleration = ship.Forward * (force / ship.MassKg);
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
}
