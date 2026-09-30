namespace SpaceSim.Core.Combat;

/// <summary>One ship-wide shield. Its station draw changes recharge speed, never its maximum.</summary>
public sealed class ShieldState
{
    public float CurrentShield { get; internal set; }
    public float MaximumShield { get; }
    public float RechargeDelayRemaining { get; internal set; }
    public bool IsRechargeDelayed => RechargeDelayRemaining > 0f;

    internal ShieldState(float maximumShield)
    {
        MaximumShield = maximumShield;
        CurrentShield = maximumShield;
    }
}
