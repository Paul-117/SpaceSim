namespace SpaceSim.Core.Power;

/// <summary>Central reactor output and independent station-demand settings.</summary>
public sealed record PowerSettings
{
    public float MaximumReactorOutputPower { get; init; } = 125f;
    /// <summary>False for a self-contained duel: all stations receive nominal power without ramp-up or fuel use.</summary>
    public bool ReactorSimulationEnabled { get; init; } = true;
    public float DefaultReactorOperatingLevelPercent { get; init; } = 100f;
    /// <summary>Seconds needed for the physical reactor output to move from 0 to 100 percent.</summary>
    public float ReactorRampSeconds { get; init; } = 60f;
    public float ReactorFuelCapacity { get; init; } = 100f;
    /// <summary>Fuel use per minute while a non-zero reactor is at the lowest possible output.</summary>
    public float ReactorIdleFuelUsagePerMinute { get; init; } = 0.2f;
    /// <summary>Fuel use per minute at 100 percent physical reactor output.</summary>
    public float ReactorMaximumFuelUsagePerMinute { get; init; } = 7f;
    /// <summary>Legacy aggregate bridge maximum. Standard boosters total 120 PU.</summary>
    public float MaximumPropulsionDraw { get; init; } = 120f;
    /// <summary>Legacy shared auxiliary draw; used when no individual booster loadout is supplied.</summary>
    public float AuxiliaryThrusterDraw { get; init; } = 30f;
    public float MaximumMainThrusterDraw => MaximumPropulsionDraw - AuxiliaryThrusterDraw * 3f;
    public float MaximumReverseThrusterDraw => AuxiliaryThrusterDraw;
    public float MaximumSideThrusterDrawPerAxis => AuxiliaryThrusterDraw;
    public float MaximumWeaponsDraw { get; init; } = 40f;
    /// <summary>Fallback for legacy callers; installed shield generators normally provide their own PU limit.</summary>
    public float MaximumShieldsDraw { get; init; } = 30f;
    /// <summary>Seconds for the bridge main thruster (W) to ramp from zero to full thrust.</summary>
    public float BridgeMainThrottleRiseSeconds { get; set; } = 10f;
    /// <summary>Seconds for the bridge main thruster (W) to decay from full thrust after release.</summary>
    public float BridgeMainThrottleFallSeconds { get; init; } = 3f;

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
