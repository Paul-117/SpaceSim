namespace SpaceSim.Core.Combat;

public sealed record ShieldSettings
{
    /// <summary>Delay after a non-depleting hit. A depleted shield uses its installed generator's reboot time instead.</summary>
    public float RechargeDelaySeconds { get; init; } = 3f;

    internal void Validate()
    {
        if (!float.IsFinite(RechargeDelaySeconds) || RechargeDelaySeconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(RechargeDelaySeconds));
    }
}
