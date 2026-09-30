namespace SpaceSim.Core.Power;

/// <summary>Central reactor output and independent station-demand settings.</summary>
public sealed record PowerSettings
{
    public float MaximumReactorOutputPower { get; init; } = 125f;
    public float DefaultReactorOperatingLevelPercent { get; init; } = 100f;
    /// <summary>Seconds needed for the physical reactor output to move from 0 to 100 percent.</summary>
    public float ReactorRampSeconds { get; init; } = 60f;
    public float ReactorFuelCapacity { get; init; } = 100f;
    /// <summary>Fuel use per minute while a non-zero reactor is at the lowest possible output.</summary>
    public float ReactorIdleFuelUsagePerMinute { get; init; } = 0.2f;
    /// <summary>Fuel use per minute at 100 percent physical reactor output.</summary>
    public float ReactorMaximumFuelUsagePerMinute { get; init; } = 7f;
    public float MaximumPropulsionDraw { get; init; } = 50f;
    /// <summary>Reserved draw for each of reverse, left yaw and right yaw thrusters.</summary>
    public float AuxiliaryThrusterDraw { get; init; } = 10f;
    public float MaximumWeaponsDraw { get; init; } = 40f;
    public float MaximumShieldsDraw { get; init; } = 35f;
    /// <summary>Seconds for the bridge main thruster (W) to ramp from zero to full thrust.</summary>
    public float BridgeMainThrottleRiseSeconds { get; init; } = 5f;
    /// <summary>Seconds for the bridge main thruster (W) to decay from full thrust after release.</summary>
    public float BridgeMainThrottleFallSeconds { get; init; } = 3f;
    public PowerProfile AttackProfile { get; init; } = new(50f, 40f, 20f);
    public PowerProfile DefendProfile { get; init; } = new(30f, 10f, 35f);
    public PowerProfile EvadeProfile { get; init; } = new(50f, 5f, 20f);
    public PowerProfile RepositionProfile { get; init; } = new(50f, 15f, 20f);
    public float DefendShieldThresholdFraction { get; init; } = 0.35f;
    public float MinimumProfileDuration { get; init; } = 1f;
    public PowerProfile DefaultProfile => new(50f, 35f, 25f);

    internal void Validate()
    {
        Positive(MaximumReactorOutputPower, nameof(MaximumReactorOutputPower));
        Positive(ReactorRampSeconds, nameof(ReactorRampSeconds));
        Positive(ReactorFuelCapacity, nameof(ReactorFuelCapacity));
        NonNegativeFinite(ReactorIdleFuelUsagePerMinute, nameof(ReactorIdleFuelUsagePerMinute));
        Positive(ReactorMaximumFuelUsagePerMinute, nameof(ReactorMaximumFuelUsagePerMinute));
        if (ReactorMaximumFuelUsagePerMinute < ReactorIdleFuelUsagePerMinute)
            throw new ArgumentOutOfRangeException(nameof(ReactorMaximumFuelUsagePerMinute));
        Positive(MaximumPropulsionDraw, nameof(MaximumPropulsionDraw));
        Positive(AuxiliaryThrusterDraw, nameof(AuxiliaryThrusterDraw));
        if (MaximumPropulsionDraw <= AuxiliaryThrusterDraw * 3f)
            throw new ArgumentOutOfRangeException(nameof(MaximumPropulsionDraw), "Must leave power for the main thruster.");
        Positive(MaximumWeaponsDraw, nameof(MaximumWeaponsDraw));
        Positive(MaximumShieldsDraw, nameof(MaximumShieldsDraw));
        Positive(BridgeMainThrottleRiseSeconds, nameof(BridgeMainThrottleRiseSeconds));
        Positive(BridgeMainThrottleFallSeconds, nameof(BridgeMainThrottleFallSeconds));
        if (!float.IsFinite(DefaultReactorOperatingLevelPercent) || DefaultReactorOperatingLevelPercent is < 0f or > 100f)
            throw new ArgumentOutOfRangeException(nameof(DefaultReactorOperatingLevelPercent));
        ValidateProfile(AttackProfile, nameof(AttackProfile));
        ValidateProfile(DefendProfile, nameof(DefendProfile));
        ValidateProfile(EvadeProfile, nameof(EvadeProfile));
        ValidateProfile(RepositionProfile, nameof(RepositionProfile));
        if (DefendShieldThresholdFraction is < 0f or > 1f)
            throw new ArgumentOutOfRangeException(nameof(DefendShieldThresholdFraction));
        Positive(MinimumProfileDuration, nameof(MinimumProfileDuration));
    }

    private void ValidateProfile(PowerProfile profile, string name)
    {
        if (!float.IsFinite(profile.Propulsion) || !float.IsFinite(profile.Weapons) || !float.IsFinite(profile.Shields) ||
            profile.Propulsion < 0f || profile.Weapons < 0f || profile.Shields < 0f)
            throw new ArgumentException("Station demand must be finite and non-negative.", name);
    }

    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }

    private static void NonNegativeFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0f) throw new ArgumentOutOfRangeException(name);
    }
}
