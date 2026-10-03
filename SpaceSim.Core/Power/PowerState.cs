namespace SpaceSim.Core.Power;

/// <summary>
/// Demand, manually assigned station budgets and delivered power for one ship.
/// </summary>
public sealed class PowerState
{
    public float MaximumPropulsionDraw { get; }
    public float AuxiliaryThrusterDrawPerAxis { get; }
    public float AuxiliaryThrusterReserveDraw => AuxiliaryThrusterDrawPerAxis * 3f;
    public float MaximumMainThrusterDraw => MaximumPropulsionDraw - AuxiliaryThrusterReserveDraw;
    public float MaximumWeaponsDraw { get; }
    public float MaximumShieldsDraw { get; }
    /// <summary>Bridge allocation as a percentage of the reactor's current output.</summary>
    public float PropulsionAllocationPercent { get; internal set; }
    /// <summary>Armarium allocation as a percentage of the reactor's current output.</summary>
    public float WeaponsAllocationPercent { get; internal set; }
    /// <summary>Shield station allocation as a percentage of the reactor's current output.</summary>
    public float ShieldsAllocationPercent { get; internal set; }
    public float PropulsionRequested { get; internal set; }
    public float WeaponsRequested { get; internal set; }
    public float ShieldsRequested { get; internal set; }
    public float PropulsionDraw { get; internal set; }
    /// <summary>Aggregate reserved power for reverse, left yaw and right yaw thrusters.</summary>
    public float AuxiliaryThrusterDraw { get; internal set; }
    public float MainThrusterDraw { get; internal set; }
    public float WeaponsDraw { get; internal set; }
    public float ShieldsDraw { get; internal set; }
    public float RequestedPower => PropulsionRequested + WeaponsRequested + ShieldsRequested;
    public float CurrentDraw => PropulsionDraw + WeaponsDraw + ShieldsDraw;
    /// <summary>Legacy profile scaling for AI ships; player stations use explicit allocations.</summary>
    public float DemandScale { get; internal set; } = 1f;
    /// <summary>Bridge power budget currently allocated by the Voltarium.</summary>
    public float PropulsionAvailable { get; internal set; }
    public float WeaponsAvailable { get; internal set; }
    public float ShieldsAvailable { get; internal set; }
    public float PropulsionPowerFactor => MaximumPropulsionDraw <= 0f ? 0f : PropulsionDraw / MaximumPropulsionDraw;
    public float AuxiliaryThrusterPowerFactor => AuxiliaryThrusterReserveDraw <= 0f ? 0f : AuxiliaryThrusterDraw / AuxiliaryThrusterReserveDraw;
    public float MainThrusterPowerFactor => MaximumMainThrusterDraw <= 0f ? 0f : MainThrusterDraw / MaximumMainThrusterDraw;
    public float MainThrusterAvailable => Math.Max(0f, PropulsionAvailable - AuxiliaryThrusterReserveDraw);
    public float WeaponsPowerFactor => MaximumWeaponsDraw <= 0f ? 0f : WeaponsDraw / MaximumWeaponsDraw;
    public float ShieldsPowerFactor => MaximumShieldsDraw <= 0f ? 0f : ShieldsDraw / MaximumShieldsDraw;

    internal PowerState(float maximumPropulsionDraw, float auxiliaryThrusterDrawPerAxis, float maximumWeaponsDraw, float maximumShieldsDraw)
    {
        MaximumPropulsionDraw = maximumPropulsionDraw;
        AuxiliaryThrusterDrawPerAxis = auxiliaryThrusterDrawPerAxis;
        MaximumWeaponsDraw = maximumWeaponsDraw;
        MaximumShieldsDraw = maximumShieldsDraw;
        float totalStationCapacity = maximumPropulsionDraw + maximumWeaponsDraw + maximumShieldsDraw;
        PropulsionAllocationPercent = maximumPropulsionDraw / totalStationCapacity * 100f;
        WeaponsAllocationPercent = maximumWeaponsDraw / totalStationCapacity * 100f;
        ShieldsAllocationPercent = 100f - PropulsionAllocationPercent - WeaponsAllocationPercent;
    }
}

/// <summary>One Voltarium operating-level intent, applied only by the authoritative simulation.</summary>
public readonly record struct PowerAllocation(float BridgePercent, float ShieldsPercent, float ArmariumPercent)
{
    public float TotalPercent => BridgePercent + ShieldsPercent + ArmariumPercent;
    public bool IsValid => float.IsFinite(BridgePercent) && float.IsFinite(ShieldsPercent) && float.IsFinite(ArmariumPercent) &&
        BridgePercent >= 0f && ShieldsPercent >= 0f && ArmariumPercent >= 0f && TotalPercent <= 100.001f;
}

/// <summary>One Voltarium intent, applied only by the authoritative simulation.</summary>
public readonly record struct ReactorCommand(float? OperatingLevelPercent = null, PowerAllocation? Allocation = null);
