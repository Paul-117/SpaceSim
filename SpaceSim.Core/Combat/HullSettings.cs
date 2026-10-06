namespace SpaceSim.Core.Combat;

public sealed record HullSettings
{
    public int MaximumHull { get; init; } = 3;
    public float SubsystemDamagePerHullHit { get; init; } = 0.5f;
    /// <summary>Can be disabled by modes that use hull hits only, without subsystem failure.</summary>
    public bool EnableSubsystemDamage { get; init; } = true;
    internal void Validate()
    {
        if (MaximumHull <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumHull));
        if (!float.IsFinite(SubsystemDamagePerHullHit) || SubsystemDamagePerHullHit <= 0f || SubsystemDamagePerHullHit > 1f)
            throw new ArgumentOutOfRangeException(nameof(SubsystemDamagePerHullHit));
    }
}
