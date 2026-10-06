using SpaceSim.Core.Combat;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Ships;

/// <summary>Per-ship combat tuning. Normal encounters create this from shared simulation defaults;
/// laboratory duels can assign different values to both combatants.</summary>
public sealed record ShipTuning(
    float MainThrustNewtons,
    float ReverseThrustNewtons,
    float YawTorqueNewtonMeters,
    float MaximumForwardSpeedMetersPerSecond,
    float MaximumReverseSpeedMetersPerSecond,
    float MaximumYawAngularVelocityRadiansPerSecond,
    float LanceRangeMeters,
    float LanceVisualRangeMeters,
    float LanceChargeSeconds,
    float ShieldRechargePerSecond)
{
    public static ShipTuning From(SimulationSettings settings) => new(
        settings.MainThrustNewtons, settings.ReverseThrustNewtons, settings.YawTorqueNewtonMeters,
        settings.MaximumNominalSpeedMetersPerSecond, settings.MaximumReverseSpeedMetersPerSecond,
        settings.MaximumYawAngularVelocityRadiansPerSecond, settings.LanceRangeMeters,
        settings.LanceVisualRangeMeters, settings.LanceChargeSeconds, settings.Shield.RechargePerSecond);

    internal void Validate()
    {
        foreach (float value in new[] { MainThrustNewtons, ReverseThrustNewtons, YawTorqueNewtonMeters,
                     MaximumForwardSpeedMetersPerSecond, MaximumReverseSpeedMetersPerSecond,
                     MaximumYawAngularVelocityRadiansPerSecond, LanceRangeMeters, LanceVisualRangeMeters,
                     LanceChargeSeconds, ShieldRechargePerSecond })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(ShipTuning));
        if (LanceVisualRangeMeters < LanceRangeMeters)
            throw new ArgumentOutOfRangeException(nameof(LanceVisualRangeMeters));
    }
}
