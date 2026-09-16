namespace SpaceSim.Core.Simulation;

/// <summary>All simulation values are SI units; presentation uses its own scale.</summary>
public sealed record SimulationSettings
{
    public const int TickRate = 60;
    public const float FixedDeltaSeconds = 1f / TickRate;

    public float ShipMassKg { get; init; } = 12_000f;
    public float YawMomentOfInertia { get; init; } = 90_000f;
    public float MainThrustNewtons { get; init; } = 144_000f;
    public float ReverseThrustNewtons { get; init; } = 72_000f;
    public float YawTorqueNewtonMeters { get; init; } = 54_000f;
    public float LanceChargeSeconds { get; init; } = 3f;
    public float LanceRangeMeters { get; init; } = 1_600f;
    public float WarpChargeSeconds { get; init; } = 10f;
    public int TargetCount { get; init; } = 10;
    public int EncounterTwoTargetCount { get; init; } = 15;
    public float TargetRadiusMeters { get; init; } = 14f;
    public float SpawnMinDistanceMeters { get; init; } = 180f;
    public float SpawnMaxDistanceMeters { get; init; } = 750f;

    internal void Validate()
    {
        Positive(ShipMassKg, nameof(ShipMassKg));
        Positive(YawMomentOfInertia, nameof(YawMomentOfInertia));
        Positive(MainThrustNewtons, nameof(MainThrustNewtons));
        Positive(ReverseThrustNewtons, nameof(ReverseThrustNewtons));
        Positive(YawTorqueNewtonMeters, nameof(YawTorqueNewtonMeters));
        Positive(LanceChargeSeconds, nameof(LanceChargeSeconds));
        Positive(LanceRangeMeters, nameof(LanceRangeMeters));
        Positive(WarpChargeSeconds, nameof(WarpChargeSeconds));
        Positive(TargetRadiusMeters, nameof(TargetRadiusMeters));
        Positive(SpawnMinDistanceMeters, nameof(SpawnMinDistanceMeters));
        Positive(SpawnMaxDistanceMeters, nameof(SpawnMaxDistanceMeters));
        if (TargetCount < 0 || TargetCount > 256)
            throw new ArgumentOutOfRangeException(nameof(TargetCount));
        if (EncounterTwoTargetCount < 0 || EncounterTwoTargetCount > 256)
            throw new ArgumentOutOfRangeException(nameof(EncounterTwoTargetCount));
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
