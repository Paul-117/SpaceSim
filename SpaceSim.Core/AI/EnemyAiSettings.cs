namespace SpaceSim.Core.AI;

/// <summary>All enemy decision thresholds live here; angles are stored as radians.</summary>
public sealed record EnemyAiSettings
{
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
    /// <summary>Preferred separation when a manoeuvre is required; the AI aims to pass at least this far away.</summary>
    public float FlybySafetyDistanceMeters { get; init; } = 250f;
    /// <summary>AI accepts no planned trajectory closer than this, while the physical collision threshold is 50 m.</summary>
    public float CollisionAvoidanceMinimumDistanceMeters { get; init; } = 100f;
    /// <summary>A direct collision course is only actively deflected once ships are this close.</summary>
    public float CollisionAvoidanceTriggerDistanceMeters { get; init; } = 350f;
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

    /// <summary>Vanguard only enters ATTACK once this much of a firing solution already exists.</summary>
    public float VanguardAttackEntryAimAngle { get; init; } = Degrees(28f);
    /// <summary>Vanguard keeps this separation from the target before it resumes a combat pass.</summary>
    public float VanguardSafetyDistanceMeters { get; init; } = 220f;
    /// <summary>Vanguard commits to a recovery arc long enough to prevent state flutter.</summary>
    public float VanguardMinimumRepositionDuration { get; init; } = 1.2f;
    /// <summary>Vanguard's planned lateral velocity component during an entry or safety arc.</summary>
    public float VanguardLateralSpeedMetersPerSecond { get; init; } = 28f;
    /// <summary>Kestrel only enters ATTACK after this target alignment has already been established.</summary>
    public float KestrelAttackEntryAimAngle { get; init; } = Degrees(24f);
    /// <summary>Kestrel performs a short deflection, then deliberately returns to pursuit.</summary>
    public float KestrelMinimumRepositionDuration { get; init; } = .75f;

    /// <summary>
    /// Ranged ships plan their arrival just inside their actual weapon range. The value is a
    /// fraction of the installed bow weapon's range, so LONGSPEAR and PEREGRINE ships do not
    /// inherit a fixed distance from the generic combat settings.
    /// </summary>
    public float RangedPreferredRangeFraction { get; init; } = .90f;
    /// <summary>
    /// Ranged ships start creating separation at this fraction of their weapon range. It is
    /// deliberately well inside maximum range, leaving reverse-thruster braking room before
    /// an opponent can force a close-range fight.
    /// </summary>
    public float RangedReverseStartRangeFraction { get; init; } = .80f;
    /// <summary>Maximum intentional closure for a ranged approach, limited to preserve its standoff.</summary>
    public float RangedMaximumApproachClosingSpeed { get; init; } = 28f;
    /// <summary>Desired opening speed once a ranged ship has crossed its reverse threshold.</summary>
    public float RangedWithdrawalSpeed { get; init; } = 18f;
    /// <summary>Extra distance retained beyond calculated reverse braking distance for a stable ranged firing corridor.</summary>
    public float RangedReverseBrakingMarginMeters { get; init; } = 35f;

    /// <summary>Assault ships aim their planned fly-by at this fraction of their own weapon range.</summary>
    public float AssaultPassDistanceRangeFraction { get; init; } = .55f;
    /// <summary>Absolute safety floor for an intentional Assault fly-by; collision handling remains stricter.</summary>
    public float AssaultMinimumPassDistanceMeters { get; init; } = 250f;
    /// <summary>Fraction of the available tracking rate used to keep an Assault firing solution stable during a pass.</summary>
    public float AssaultTrackingSafetyFactor { get; init; } = .70f;
    public float AssaultMinimumPassSpeedMetersPerSecond { get; init; } = 15f;
    public float AssaultMaximumPassSpeedMetersPerSecond { get; init; } = 55f;

    /// <summary>Patrol maintains a stable mid-range firing corridor derived from its installed weapon.</summary>
    public float PatrolMinimumRangeFraction { get; init; } = .40f;
    public float PatrolPreferredRangeFraction { get; init; } = .70f;
    public float PatrolMaximumRangeFraction { get; init; } = .90f;

    internal void Validate()
    {
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
        Positive(CollisionAvoidanceMinimumDistanceMeters, nameof(CollisionAvoidanceMinimumDistanceMeters));
        Positive(CollisionAvoidanceTriggerDistanceMeters, nameof(CollisionAvoidanceTriggerDistanceMeters));
        Positive(NoMainEngineTurnDistanceMeters, nameof(NoMainEngineTurnDistanceMeters));
        Positive(MinimumInterceptLeadSeconds, nameof(MinimumInterceptLeadSeconds));
        Positive(RotationKp, nameof(RotationKp));
        Positive(RotationKd, nameof(RotationKd));
        Positive(TurnCommandThreshold, nameof(TurnCommandThreshold));
        Positive(ThrustAlignmentAngle, nameof(ThrustAlignmentAngle));
        Positive(ShipHitRadiusMeters, nameof(ShipHitRadiusMeters));
        Positive(VanguardAttackEntryAimAngle, nameof(VanguardAttackEntryAimAngle));
        Positive(VanguardSafetyDistanceMeters, nameof(VanguardSafetyDistanceMeters));
        Positive(VanguardMinimumRepositionDuration, nameof(VanguardMinimumRepositionDuration));
        Positive(VanguardLateralSpeedMetersPerSecond, nameof(VanguardLateralSpeedMetersPerSecond));
        Positive(KestrelAttackEntryAimAngle, nameof(KestrelAttackEntryAimAngle));
        Positive(KestrelMinimumRepositionDuration, nameof(KestrelMinimumRepositionDuration));
        Fraction(RangedPreferredRangeFraction, nameof(RangedPreferredRangeFraction));
        Fraction(RangedReverseStartRangeFraction, nameof(RangedReverseStartRangeFraction));
        Positive(RangedMaximumApproachClosingSpeed, nameof(RangedMaximumApproachClosingSpeed));
        Positive(RangedWithdrawalSpeed, nameof(RangedWithdrawalSpeed));
        Positive(RangedReverseBrakingMarginMeters, nameof(RangedReverseBrakingMarginMeters));
        Fraction(AssaultPassDistanceRangeFraction, nameof(AssaultPassDistanceRangeFraction));
        Positive(AssaultMinimumPassDistanceMeters, nameof(AssaultMinimumPassDistanceMeters));
        Fraction(AssaultTrackingSafetyFactor, nameof(AssaultTrackingSafetyFactor));
        Positive(AssaultMinimumPassSpeedMetersPerSecond, nameof(AssaultMinimumPassSpeedMetersPerSecond));
        Positive(AssaultMaximumPassSpeedMetersPerSecond, nameof(AssaultMaximumPassSpeedMetersPerSecond));
        Fraction(PatrolMinimumRangeFraction, nameof(PatrolMinimumRangeFraction));
        Fraction(PatrolPreferredRangeFraction, nameof(PatrolPreferredRangeFraction));
        Fraction(PatrolMaximumRangeFraction, nameof(PatrolMaximumRangeFraction));
        if (MinimumCombatDistance >= PreferredCombatDistance || PreferredCombatDistance >= MaximumCombatDistance)
            throw new ArgumentException("Combat distances must increase from minimum through preferred to maximum.");
        if (CollisionAvoidanceMinimumDistanceMeters <= 50f)
            throw new ArgumentOutOfRangeException(nameof(CollisionAvoidanceMinimumDistanceMeters), "Must exceed the physical collision threshold.");
        if (FlybySafetyDistanceMeters < CollisionAvoidanceMinimumDistanceMeters ||
            CollisionAvoidanceTriggerDistanceMeters <= FlybySafetyDistanceMeters)
            throw new ArgumentException("Collision avoidance distances must increase from minimum through manoeuvre to trigger.");
        if (VanguardSafetyDistanceMeters <= CollisionAvoidanceMinimumDistanceMeters)
            throw new ArgumentOutOfRangeException(nameof(VanguardSafetyDistanceMeters), "Must exceed the AI minimum distance.");
        if (PatrolSpawnMaximumDistanceMeters <= PatrolSpawnMinimumDistanceMeters)
            throw new ArgumentException("Patrol spawn distances must increase.");
        if (PatrolReactorOperatingLevelPercent is <= 0f or > 100f || !float.IsFinite(PatrolReactorOperatingLevelPercent))
            throw new ArgumentOutOfRangeException(nameof(PatrolReactorOperatingLevelPercent));
        if (RangedReverseStartRangeFraction >= RangedPreferredRangeFraction)
            throw new ArgumentException("Ranged reverse threshold must remain below the preferred weapon-range distance.");
        if (AssaultMinimumPassSpeedMetersPerSecond > AssaultMaximumPassSpeedMetersPerSecond)
            throw new ArgumentException("Assault pass speed limits must increase from minimum to maximum.");
        if (PatrolMinimumRangeFraction >= PatrolPreferredRangeFraction ||
            PatrolPreferredRangeFraction >= PatrolMaximumRangeFraction)
            throw new ArgumentException("Patrol weapon-range fractions must increase from minimum through preferred to maximum.");
    }

    private static float Degrees(float value) => value * MathF.PI / 180f;
    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }

    private static void Fraction(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f || value >= 1f) throw new ArgumentOutOfRangeException(name);
    }
}
