namespace SpaceSim.Core.Combat;

/// <summary>One ship-wide shield. A depleted generator must reboot before it can recharge.</summary>
public sealed class ShieldState
{
    public float CurrentShield { get; internal set; }
    public float MaximumShield { get; }
    public float RechargeDelayRemaining { get; internal set; }
    public float RebootRemaining { get; internal set; }
    public bool IsRechargeDelayed => RechargeDelayRemaining > 0f;
    public bool IsRebooting => RebootRemaining > 0f;
    public bool NeedsPower => IsRebooting || (!IsRechargeDelayed && CurrentShield < MaximumShield);

    internal ShieldState(float maximumShield)
    {
        MaximumShield = maximumShield;
        CurrentShield = maximumShield;
    }
}
