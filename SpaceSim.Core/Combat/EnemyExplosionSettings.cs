namespace SpaceSim.Core.Combat;

/// <summary>Distance thresholds for the damage caused by a newly destroyed enemy ship.</summary>
public sealed record EnemyExplosionSettings
{
    public float ShieldDepletionDistanceMeters { get; init; } = 350f;
    public float OneSubsystemDisabledDistanceMeters { get; init; } = 250f;
    public float TwoSubsystemsDisabledDistanceMeters { get; init; } = 200f;
    public float PlayerDestructionDistanceMeters { get; init; } = 150f;

    internal void Validate()
    {
        float[] values = [ShieldDepletionDistanceMeters, OneSubsystemDisabledDistanceMeters,
            TwoSubsystemsDisabledDistanceMeters, PlayerDestructionDistanceMeters];
        if (values.Any(value => !float.IsFinite(value) || value <= 0f) ||
            ShieldDepletionDistanceMeters < OneSubsystemDisabledDistanceMeters ||
            OneSubsystemDisabledDistanceMeters < TwoSubsystemsDisabledDistanceMeters ||
            TwoSubsystemsDisabledDistanceMeters < PlayerDestructionDistanceMeters)
            throw new ArgumentOutOfRangeException(nameof(ShieldDepletionDistanceMeters),
                "Explosion distances must be positive and descend from shield damage to ship destruction.");
    }
}
