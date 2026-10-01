namespace SpaceSim.Core.AI;

/// <summary>All enemy decision thresholds live here; angles are stored as radians.</summary>
public sealed record EnemyAiSettings
{
    /// <summary>Detection distance when the player's physical reactor output is 100 percent.</summary>
    public float DetectionRangeAtFullReactorMeters { get; init; } = 2_000f;
    public float PatrolSpawnMinimumDistanceMeters { get; init; } = 2_000f;
    public float PatrolSpawnMaximumDistanceMeters { get; init; } = 3_000f;
    public float PatrolCruiseSpeedMetersPerSecond { get; init; } = 100f;
    /// <summary>Patrol reactor starts at 50%; detection ramps it to full output at the normal reactor rate.</summary>
    public float PatrolReactorOperatingLevelPercent { get; init; } = 50f;
    public float PatrolPropulsionDraw { get; init; } = 50f;

    /// <summary>Stable target distance within the shared 250-900 metre combat range.</summary>
    public float PreferredCombatDistance { get; init; } = 600f;
    public float MinimumCombatDistance { get; init; } = 250f;
    public float MaximumCombatDistance { get; init; } = 900f;
    public float FireAimTolerance { get; init; } = Degrees(3f);
    public float MinimumStateDuration { get; init; } = 0.35f;
    /// <summary>Maximum intentional approach speed while still outside weapon range.</summary>
    public float MaximumApproachClosingSpeed { get; init; } = 55f;
    /// <summary>Largest accepted relative speed when entering or holding weapon range.</summary>
    public float MaximumAttackRelativeSpeed { get; init; } = 35f;
    /// <summary>Separation targeted by a high-speed fly-by; must exceed the 100 metre collision distance.</summary>
    public float FlybySafetyDistanceMeters { get; init; } = 180f;
    /// <summary>Inside this distance braking must not turn the ship around for main-engine thrust.</summary>
    public float NoMainEngineTurnDistanceMeters { get; init; } = 2_000f;
    /// <summary>Minimum lead time used to estimate the player's near-future course.</summary>
    public float MinimumInterceptLeadSeconds { get; init; } = 4f;
    public float RotationKp { get; init; } = 2.4f;
    public float RotationKd { get; init; } = 2.8f;
    /// <summary>Small deadband so the controller still closes the final few degrees of aim error.</summary>
    public float TurnCommandThreshold { get; init; } = 0.03f;
    public float ThrustAlignmentAngle { get; init; } = Degrees(18f);
    public float ShipHitRadiusMeters { get; init; } = 16f;

    internal void Validate()
    {
        Positive(DetectionRangeAtFullReactorMeters, nameof(DetectionRangeAtFullReactorMeters));
        Positive(PatrolSpawnMinimumDistanceMeters, nameof(PatrolSpawnMinimumDistanceMeters));
        Positive(PatrolSpawnMaximumDistanceMeters, nameof(PatrolSpawnMaximumDistanceMeters));
        Positive(PatrolCruiseSpeedMetersPerSecond, nameof(PatrolCruiseSpeedMetersPerSecond));
        Positive(PatrolPropulsionDraw, nameof(PatrolPropulsionDraw));
        Positive(PreferredCombatDistance, nameof(PreferredCombatDistance));
        Positive(MinimumCombatDistance, nameof(MinimumCombatDistance));
        Positive(MaximumCombatDistance, nameof(MaximumCombatDistance));
        Positive(FireAimTolerance, nameof(FireAimTolerance));
        Positive(MinimumStateDuration, nameof(MinimumStateDuration));
        Positive(MaximumApproachClosingSpeed, nameof(MaximumApproachClosingSpeed));
        Positive(MaximumAttackRelativeSpeed, nameof(MaximumAttackRelativeSpeed));
        Positive(FlybySafetyDistanceMeters, nameof(FlybySafetyDistanceMeters));
        Positive(NoMainEngineTurnDistanceMeters, nameof(NoMainEngineTurnDistanceMeters));
        Positive(MinimumInterceptLeadSeconds, nameof(MinimumInterceptLeadSeconds));
        Positive(RotationKp, nameof(RotationKp));
        Positive(RotationKd, nameof(RotationKd));
        Positive(TurnCommandThreshold, nameof(TurnCommandThreshold));
        Positive(ThrustAlignmentAngle, nameof(ThrustAlignmentAngle));
        Positive(ShipHitRadiusMeters, nameof(ShipHitRadiusMeters));
        if (MinimumCombatDistance >= PreferredCombatDistance || PreferredCombatDistance >= MaximumCombatDistance)
            throw new ArgumentException("Combat distances must increase from minimum through preferred to maximum.");
        if (FlybySafetyDistanceMeters <= 100f)
            throw new ArgumentOutOfRangeException(nameof(FlybySafetyDistanceMeters), "Must exceed collision distance.");
        if (PatrolSpawnMaximumDistanceMeters <= PatrolSpawnMinimumDistanceMeters ||
            DetectionRangeAtFullReactorMeters > PatrolSpawnMinimumDistanceMeters)
            throw new ArgumentException("Patrol spawn distances must stay outside the detection range.");
        if (PatrolReactorOperatingLevelPercent is <= 0f or > 100f || !float.IsFinite(PatrolReactorOperatingLevelPercent))
            throw new ArgumentOutOfRangeException(nameof(PatrolReactorOperatingLevelPercent));
    }

    private static float Degrees(float value) => value * MathF.PI / 180f;
    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }
}
