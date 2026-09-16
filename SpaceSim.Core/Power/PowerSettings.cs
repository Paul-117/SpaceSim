namespace SpaceSim.Core.Power;

/// <summary>Central, deliberately simple constant-reactor configuration for V1.3.</summary>
public sealed record PowerSettings
{
    public float TotalAvailablePower { get; init; } = 100f;
    public float NominalSystemPower { get; init; } = 100f;
    public float DefaultPropulsionPower { get; init; } = 35f;
    public float DefaultWeaponsPower { get; init; } = 35f;
    public float DefaultShieldsPower { get; init; } = 30f;
    public float AdjustmentStep { get; init; } = 5f;
    public PowerProfile AttackProfile { get; init; } = new(20f, 60f, 20f);
    public PowerProfile DefendProfile { get; init; } = new(20f, 20f, 60f);
    public PowerProfile EvadeProfile { get; init; } = new(60f, 10f, 30f);
    public PowerProfile RepositionProfile { get; init; } = new(55f, 20f, 25f);
    public float DefendShieldThresholdFraction { get; init; } = 0.35f;
    public float MinimumProfileDuration { get; init; } = 1f;

    public PowerProfile DefaultProfile => new(DefaultPropulsionPower, DefaultWeaponsPower, DefaultShieldsPower);

    internal void Validate()
    {
        Positive(TotalAvailablePower, nameof(TotalAvailablePower));
        Positive(NominalSystemPower, nameof(NominalSystemPower));
        Positive(AdjustmentStep, nameof(AdjustmentStep));
        ValidateProfile(DefaultProfile, nameof(DefaultProfile));
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
            profile.Propulsion < 0f || profile.Weapons < 0f || profile.Shields < 0f ||
            profile.Propulsion + profile.Weapons + profile.Shields > TotalAvailablePower + 0.0001f)
            throw new ArgumentException("Power allocations must be finite, non-negative and within reactor output.", name);
    }

    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }
}
