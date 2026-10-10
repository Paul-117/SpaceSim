namespace SpaceSim.Core.Simulation;

using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Power;

/// <summary>All simulation values are SI units; presentation uses its own scale.</summary>
public sealed record SimulationSettings
{
    public const int TickRate = 60;
    public const float FixedDeltaSeconds = 1f / TickRate;

    public float ShipMassKg { get; init; } = 12_000f;
    public float YawMomentOfInertia { get; init; } = 90_000f;
    public float MainThrustNewtons { get; set; } = 100_000f;
    public float ReverseThrustNewtons { get; set; } = 30_000f;
    public float YawTorqueNewtonMeters { get; set; } = 11_530f;
    /// <summary>Maximum active yaw rate. Existing angular inertia above this value is not clamped.</summary>
    public float MaximumYawAngularVelocityRadiansPerSecond { get; set; } = 0.17453293f;
    public float LanceChargeSeconds { get; init; } = 5f;
    /// <summary>Maximum range at which a lance can affect gameplay targets.</summary>
    public float LanceRangeMeters { get; init; } = 1_000f;
    /// <summary>Purely visual beam length; intensity fades after LanceRangeMeters.</summary>
    public float LanceVisualRangeMeters { get; init; } = 3_000f;
    /// <summary>Maximum horizontal player lance mount deflection from the ship nose.</summary>
    public float LanceTurretMaximumAngleDegrees { get; init; } = 5f;
    public float LanceTurretDegreesPerSecond { get; init; } = 3.33f;
    /// <summary>Centre-to-centre distance below which a player and enemy ship are both destroyed.</summary>
    public float ShipCollisionDistanceMeters { get; init; } = 50f;
    public float WarpChargeSeconds { get; init; } = 10f;
    /// <summary>Gameplay start option used by the bridge: begins with a ready warp drive.</summary>
    public bool StartWarpReady { get; init; }
    /// <summary>Gameplay start option: begin outside encounters on the destination star map.</summary>
    public bool StartInHyperspace { get; init; }
    public int TargetCount { get; init; } = 10;
    public int EncounterTwoTargetCount { get; init; }
    public int EncounterThreeTargetCount { get; init; }
    public int EncounterFourTargetCount { get; init; }
    public float TargetRadiusMeters { get; init; } = 14f;
    public float SpawnMinDistanceMeters { get; init; } = 180f;
    public float SpawnMaxDistanceMeters { get; init; } = 750f;
    public EnemyAiSettings EnemyAi { get; init; } = new();
    public BoardComputerSettings BoardComputers { get; init; } = new();
    /// <summary>Procedurally generated ships use their loadout-selected board computer unless a scenario supplies one explicitly.</summary>
    public bool UseGeneratedBoardComputers { get; init; } = true;
    /// <summary>Enemy hulls use class-specific mass and structural-integrity profiles unless a controlled scenario disables them.</summary>
    public bool UseClassHullProfiles { get; init; } = true;
    public PowerSettings Power { get; init; } = new();
    public ShieldSettings Shield { get; init; } = new();
    public HullSettings Hull { get; init; } = new();
    public EnemyExplosionSettings EnemyExplosion { get; init; } = new();
    public float MaximumNominalSpeedMetersPerSecond { get; set; } = 100f;
    /// <summary>Maximum speed a reverse-thrust acceleration may add to. Existing inertia is never clamped.</summary>
    public float MaximumReverseSpeedMetersPerSecond { get; set; } = 50f;

    internal void Validate()
    {
        Positive(ShipMassKg, nameof(ShipMassKg));
        Positive(YawMomentOfInertia, nameof(YawMomentOfInertia));
        Positive(MainThrustNewtons, nameof(MainThrustNewtons));
        Positive(ReverseThrustNewtons, nameof(ReverseThrustNewtons));
        Positive(YawTorqueNewtonMeters, nameof(YawTorqueNewtonMeters));
        Positive(MaximumYawAngularVelocityRadiansPerSecond, nameof(MaximumYawAngularVelocityRadiansPerSecond));
        Positive(LanceChargeSeconds, nameof(LanceChargeSeconds));
        Positive(LanceRangeMeters, nameof(LanceRangeMeters));
        Positive(LanceVisualRangeMeters, nameof(LanceVisualRangeMeters));
        if (LanceVisualRangeMeters < LanceRangeMeters)
            throw new ArgumentException("Visual lance range must not be shorter than gameplay range.");
        Positive(LanceTurretMaximumAngleDegrees, nameof(LanceTurretMaximumAngleDegrees));
        Positive(LanceTurretDegreesPerSecond, nameof(LanceTurretDegreesPerSecond));
        Positive(ShipCollisionDistanceMeters, nameof(ShipCollisionDistanceMeters));
        Positive(WarpChargeSeconds, nameof(WarpChargeSeconds));
        Positive(TargetRadiusMeters, nameof(TargetRadiusMeters));
        Positive(SpawnMinDistanceMeters, nameof(SpawnMinDistanceMeters));
        Positive(SpawnMaxDistanceMeters, nameof(SpawnMaxDistanceMeters));
        ArgumentNullException.ThrowIfNull(EnemyAi);
        ArgumentNullException.ThrowIfNull(BoardComputers);
        ArgumentNullException.ThrowIfNull(Power);
        ArgumentNullException.ThrowIfNull(Shield);
        ArgumentNullException.ThrowIfNull(Hull);
        ArgumentNullException.ThrowIfNull(EnemyExplosion);
        EnemyAi.Validate();
        BoardComputers.Validate();
        Power.Validate();
        if (EnemyAi.PatrolPropulsionDraw > Power.MaximumPropulsionDraw)
            throw new ArgumentException("Patrol propulsion draw must not exceed the propulsion station maximum.");
        Shield.Validate();
        Hull.Validate();
        EnemyExplosion.Validate();
        Positive(MaximumNominalSpeedMetersPerSecond, nameof(MaximumNominalSpeedMetersPerSecond));
        Positive(MaximumReverseSpeedMetersPerSecond, nameof(MaximumReverseSpeedMetersPerSecond));
        if (TargetCount < 0 || TargetCount > 256)
            throw new ArgumentOutOfRangeException(nameof(TargetCount));
        if (EncounterTwoTargetCount < 0 || EncounterTwoTargetCount > 256)
            throw new ArgumentOutOfRangeException(nameof(EncounterTwoTargetCount));
        if (EncounterThreeTargetCount < 0 || EncounterThreeTargetCount > 256)
            throw new ArgumentOutOfRangeException(nameof(EncounterThreeTargetCount));
        if (EncounterFourTargetCount < 0 || EncounterFourTargetCount > 256)
            throw new ArgumentOutOfRangeException(nameof(EncounterFourTargetCount));
        if (SpawnMinDistanceMeters <= TargetRadiusMeters ||
            SpawnMaxDistanceMeters <= SpawnMinDistanceMeters)
            throw new ArgumentException("Target spawn distances must describe a safe annulus.");
    }

    private static void Positive(float value, string name)
    {
        if (!float.IsFinite(value) || value <= 0f)
            throw new ArgumentOutOfRangeException(name, "Must be finite and positive.");
    }
}
