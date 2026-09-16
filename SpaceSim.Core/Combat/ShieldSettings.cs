namespace SpaceSim.Core.Combat;

public sealed record ShieldSettings
{
    public float MaximumShield { get; init; } = 100f;
    public float LanceDamage { get; init; } = 100f;
    public float RechargePerSecond { get; init; } = 20f;
    public float RechargeDelaySeconds { get; init; } = 3f;

    internal void Validate()
    {
        foreach ((float value, string name) in new[]
                 { (MaximumShield, nameof(MaximumShield)), (LanceDamage, nameof(LanceDamage)),
                   (RechargePerSecond, nameof(RechargePerSecond)), (RechargeDelaySeconds, nameof(RechargeDelaySeconds)) })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(name);
    }
}
