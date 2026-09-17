namespace SpaceSim.Core.AI;

/// <summary>All enemy decision thresholds live here; angles are stored as radians.</summary>
public sealed record EnemyAiSettings
{
    public float PreferredCombatDistanceAggressive { get; init; } = 450f;
    public float PreferredCombatDistanceNormal { get; init; } = 475f;
    public float PreferredCombatDistanceDefensive { get; init; } = 525f;
    public float PreferredCombatDistanceCritical { get; init; } = 550f;
    public float MinimumCombatDistance { get; init; } = 250f;
    public float AttackEnterDistance { get; init; } = 700f;
    public float AttackExitDistance { get; init; } = 850f;
    public float FireAimTolerance { get; init; } = Degrees(3f);
    public float ThreatAimAngle { get; init; } = Degrees(10f);
    public float ThreatExitAimAngle { get; init; } = Degrees(16f);
    public float ThreatChargeThreshold { get; init; } = 0.80f;
    public float AggressiveThreatChargeThreshold { get; init; } = 1.0f;
    public float NormalThreatChargeThreshold { get; init; } = 0.90f;
    public float DefensiveThreatChargeThreshold { get; init; } = 0.80f;
    public float CriticalThreatChargeThreshold { get; init; } = 0.75f;
    public float AggressiveThreatAimAngle { get; init; } = Degrees(2f);
    public float NormalThreatAimAngle { get; init; } = Degrees(4f);
    public float DefensiveThreatAimAngle { get; init; } = Degrees(6f);
    public float CriticalThreatAimAngle { get; init; } = Degrees(8f);
    public float AggressiveShieldThreshold { get; init; } = 0.70f;
    public float DefensiveShieldThreshold { get; init; } = 0.30f;
    public float DefensiveSystemConditionThreshold { get; init; } = 0.60f;
    public float CriticalSystemConditionThreshold { get; init; } = 0.25f;
    public float MinimumEvadeDuration { get; init; } = 0.45f;
    public float MaximumEvadeDuration { get; init; } = 0.9f;
    public float AggressiveEvadeCooldown { get; init; } = 3.5f;
    public float NormalEvadeCooldown { get; init; } = 3f;
    public float DefensiveEvadeCooldown { get; init; } = 2.5f;
    public float CriticalEvadeCooldown { get; init; } = 2f;
    public float MinimumStateDuration { get; init; } = 0.35f;
    public float MaximumDesiredClosingSpeed { get; init; } = 55f;
    public float MaximumDesiredRelativeSpeed { get; init; } = 70f;
    public float AttackRelativeSpeed { get; init; } = 38f;
    public float RepositionRelativeSpeed { get; init; } = 26f;
    public float RotationKp { get; init; } = 2.4f;
    public float RotationKd { get; init; } = 2.8f;
    public float TurnCommandThreshold { get; init; } = 0.08f;
    public float ThrustAlignmentAngle { get; init; } = Degrees(18f);
    public float EvadeThrustAngle { get; init; } = Degrees(28f);
    public float ShipHitRadiusMeters { get; init; } = 16f;

    internal void Validate()
    {
        Positive(PreferredCombatDistanceAggressive, nameof(PreferredCombatDistanceAggressive));
        Positive(PreferredCombatDistanceNormal, nameof(PreferredCombatDistanceNormal));
        Positive(PreferredCombatDistanceDefensive, nameof(PreferredCombatDistanceDefensive));
        Positive(PreferredCombatDistanceCritical, nameof(PreferredCombatDistanceCritical));
        Positive(MinimumCombatDistance, nameof(MinimumCombatDistance));
        Positive(AttackEnterDistance, nameof(AttackEnterDistance));
        Positive(AttackExitDistance, nameof(AttackExitDistance));
        Positive(FireAimTolerance, nameof(FireAimTolerance));
        Positive(ThreatAimAngle, nameof(ThreatAimAngle));
        Positive(ThreatExitAimAngle, nameof(ThreatExitAimAngle));
        Positive(MinimumEvadeDuration, nameof(MinimumEvadeDuration));
        Positive(MaximumEvadeDuration, nameof(MaximumEvadeDuration));
        Positive(MinimumStateDuration, nameof(MinimumStateDuration));
        Positive(MaximumDesiredClosingSpeed, nameof(MaximumDesiredClosingSpeed));
        Positive(MaximumDesiredRelativeSpeed, nameof(MaximumDesiredRelativeSpeed));
        Positive(AttackRelativeSpeed, nameof(AttackRelativeSpeed));
        Positive(RepositionRelativeSpeed, nameof(RepositionRelativeSpeed));
        Positive(RotationKp, nameof(RotationKp));
        Positive(RotationKd, nameof(RotationKd));
        Positive(TurnCommandThreshold, nameof(TurnCommandThreshold));
        Positive(ThrustAlignmentAngle, nameof(ThrustAlignmentAngle));
        Positive(EvadeThrustAngle, nameof(EvadeThrustAngle));
        Positive(ShipHitRadiusMeters, nameof(ShipHitRadiusMeters));
        Positive(AggressiveEvadeCooldown, nameof(AggressiveEvadeCooldown));
        Positive(NormalEvadeCooldown, nameof(NormalEvadeCooldown));
        Positive(DefensiveEvadeCooldown, nameof(DefensiveEvadeCooldown));
        Positive(CriticalEvadeCooldown, nameof(CriticalEvadeCooldown));
        if (MinimumCombatDistance >= PreferredCombatDistanceAggressive ||
            PreferredCombatDistanceAggressive >= AttackEnterDistance || AttackEnterDistance >= AttackExitDistance)
            throw new ArgumentException("Combat distances must increase from minimum through attack exit.");
        if (ThreatAimAngle >= ThreatExitAimAngle)
            throw new ArgumentException("Threat exit angle must be larger to provide hysteresis.");
        if (new[] { ThreatChargeThreshold, AggressiveThreatChargeThreshold, NormalThreatChargeThreshold,
                DefensiveThreatChargeThreshold, CriticalThreatChargeThreshold, AggressiveShieldThreshold,
                DefensiveShieldThreshold, DefensiveSystemConditionThreshold, CriticalSystemConditionThreshold }
            .Any(value => value is < 0f or > 1f))
            throw new ArgumentOutOfRangeException(nameof(ThreatChargeThreshold));
        if (MinimumEvadeDuration >= MaximumEvadeDuration)
            throw new ArgumentException("Maximum evade duration must exceed minimum duration.");
    }

    private static float Degrees(float value) => value * MathF.PI / 180f;
    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }
}
