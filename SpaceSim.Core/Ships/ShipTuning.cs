using SpaceSim.Core.Combat;
using SpaceSim.Core.Power;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

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
    BowWeaponDefinition BowWeapon,
    ShieldDefinition Shield,
    ReactorDefinition Reactor,
    MainBoosterDefinition MainBooster,
    ReverseBoosterDefinition ReverseBooster,
    SideBoosterDefinition SideBooster)
{
    /// <summary>Current Nomad hull geometry; later ship hulls may expose their own arm.</summary>
    public const float SideThrusterLeverArmMeters = 11.53f;

    public static ShipTuning From(SimulationSettings settings, BowWeaponDefinition? weapon = null, ShieldDefinition? shield = null,
        ReactorDefinition? reactor = null, MainBoosterDefinition? mainBooster = null,
        ReverseBoosterDefinition? reverseBooster = null, SideBoosterDefinition? sideBooster = null)
    {
        BowWeaponDefinition selected = weapon ?? BowWeaponDefinitions.PeregrineL1000;
        ShieldDefinition selectedShield = shield ?? ShieldDefinitions.GuardianS20;
        ReactorDefinition selectedReactor = reactor ?? ReactorDefinitions.CoreX125 with
        {
            MaximumOutputPower = settings.Power.MaximumReactorOutputPower,
            MaximumFuelUsagePerMinute = settings.Power.ReactorMaximumFuelUsagePerMinute,
            RampUpSeconds = settings.Power.ReactorRampSeconds
        };
        MainBoosterDefinition selectedMain = mainBooster ?? BoosterDefinitions.AtlasM100 with
        {
            ThrustNewtons = settings.MainThrustNewtons,
            RampUpSeconds = settings.Power.BridgeMainThrottleRiseSeconds,
            MaximumSpeedMetersPerSecond = settings.MaximumNominalSpeedMetersPerSecond,
            PowerDraw = settings.Power.MaximumMainThrusterDraw
        };
        ReverseBoosterDefinition selectedReverse = reverseBooster ?? BoosterDefinitions.AnchorR30 with
        {
            ThrustNewtons = settings.ReverseThrustNewtons,
            MaximumSpeedMetersPerSecond = settings.MaximumReverseSpeedMetersPerSecond,
            PowerDraw = settings.Power.MaximumReverseThrusterDraw
        };
        SideBoosterDefinition selectedSide = sideBooster ?? BoosterDefinitions.VectorS1 with
        {
            ThrustNewtons = settings.YawTorqueNewtonMeters / SideThrusterLeverArmMeters,
            MaximumRotationDegreesPerSecond = settings.MaximumYawAngularVelocityRadiansPerSecond * 180f / MathF.PI,
            PowerDraw = settings.Power.MaximumSideThrusterDrawPerAxis
        };
        return new(
        selectedMain.ThrustNewtons, selectedReverse.ThrustNewtons, selectedSide.ThrustNewtons * SideThrusterLeverArmMeters,
        selectedMain.MaximumSpeedMetersPerSecond, selectedReverse.MaximumSpeedMetersPerSecond,
        selectedSide.MaximumAngularVelocityRadiansPerSecond, selected.RangeMeters,
        selected.VisualRangeMeters, selected.ChargeSeconds, selected, selectedShield, selectedReactor,
        selectedMain, selectedReverse, selectedSide);
    }

    internal void Validate()
    {
        foreach (float value in new[] { MainThrustNewtons, ReverseThrustNewtons, YawTorqueNewtonMeters,
                     MaximumForwardSpeedMetersPerSecond, MaximumReverseSpeedMetersPerSecond,
                     MaximumYawAngularVelocityRadiansPerSecond, LanceRangeMeters, LanceVisualRangeMeters,
                     LanceChargeSeconds })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(ShipTuning));
        if (LanceVisualRangeMeters < LanceRangeMeters)
            throw new ArgumentOutOfRangeException(nameof(LanceVisualRangeMeters));
        ArgumentNullException.ThrowIfNull(BowWeapon);
        BowWeapon.Validate();
        ArgumentNullException.ThrowIfNull(Shield);
        Shield.Validate();
        ArgumentNullException.ThrowIfNull(Reactor);
        Reactor.Validate();
        ArgumentNullException.ThrowIfNull(MainBooster);
        MainBooster.Validate();
        ArgumentNullException.ThrowIfNull(ReverseBooster);
        ReverseBooster.Validate();
        ArgumentNullException.ThrowIfNull(SideBooster);
        SideBooster.Validate();
    }
}
