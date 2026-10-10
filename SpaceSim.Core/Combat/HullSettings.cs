namespace SpaceSim.Core.Combat;

public sealed record HullSettings
{
    /// <summary>Structural hit points shared by Nomad and the default enemy ships.</summary>
    public float MaximumHull { get; init; } = 30f;
    /// <summary>Chance that one previously intact subsystem receives a debuff after a hull hit.</summary>
    public float SubsystemDamageChancePerHullHit { get; init; } = 0.5f;
    /// <summary>Can be disabled by modes that use hull hits only, without subsystem failure.</summary>
    public bool EnableSubsystemDamage { get; init; } = true;
    internal void Validate()
    {
        if (MaximumHull <= 0) throw new ArgumentOutOfRangeException(nameof(MaximumHull));
        if (!float.IsFinite(SubsystemDamageChancePerHullHit) || SubsystemDamageChancePerHullHit < 0f || SubsystemDamageChancePerHullHit > 1f)
            throw new ArgumentOutOfRangeException(nameof(SubsystemDamageChancePerHullHit));
    }
}
