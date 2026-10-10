namespace SpaceSim.Core.Power;

/// <summary>
/// Demand, manually assigned station budgets and delivered power for one ship.
/// </summary>
public sealed class PowerState
{
    public float MaximumPropulsionDraw { get; }
    public float MaximumMainThrusterDraw { get; }
    public float MaximumReverseThrusterDraw { get; }
    public float MaximumSideThrusterDrawPerAxis { get; }
    /// <summary>Compatibility view of the side-thruster draw per axis.</summary>
    public float AuxiliaryThrusterDrawPerAxis => MaximumSideThrusterDrawPerAxis;
    /// <summary>
    /// Reverse and yaw use the shared auxiliary-thruster station. Its capacity is the larger
    /// installed auxiliary module, rather than three permanently additive loads.
    /// </summary>
    public float AuxiliaryThrusterReserveDraw => Math.Max(MaximumReverseThrusterDraw, MaximumSideThrusterDrawPerAxis);
    public float MaximumWeaponsDraw { get; }
    public float MaximumShieldsDraw { get; }
    /// <summary>Enemy-only passive sensor suite draw. Player ships currently have no such station draw.</summary>
    public float MaximumSensorsDraw { get; }
    /// <summary>Bridge allocation as a percentage of the reactor's current output.</summary>
    public float PropulsionAllocationPercent { get; internal set; }
    /// <summary>Armarium allocation as a percentage of the reactor's current output.</summary>
    public float WeaponsAllocationPercent { get; internal set; }
    /// <summary>Shield station allocation as a percentage of the reactor's current output.</summary>
    public float ShieldsAllocationPercent { get; internal set; }
    public float PropulsionRequested { get; internal set; }
    public float WeaponsRequested { get; internal set; }
    public float ShieldsRequested { get; internal set; }
    public float SensorsRequested { get; internal set; }
    public float PropulsionDraw { get; internal set; }
    /// <summary>Aggregate reserved power for reverse, left yaw and right yaw thrusters.</summary>
    public float AuxiliaryThrusterDraw { get; internal set; }
    public float MainThrusterDraw { get; internal set; }
    public float WeaponsDraw { get; internal set; }
    public float ShieldsDraw { get; internal set; }
    public float SensorsDraw { get; internal set; }
    /// <summary>Reactor output actually available after Voltarium condition is applied.</summary>
    public float EffectiveReactorOutput { get; internal set; }
    public float RequestedPower => PropulsionRequested + WeaponsRequested + ShieldsRequested + SensorsRequested;
    public float CurrentDraw => PropulsionDraw + WeaponsDraw + ShieldsDraw + SensorsDraw;
    /// <summary>Legacy profile scaling for AI ships; player stations use explicit allocations.</summary>
    public float DemandScale { get; internal set; } = 1f;
    /// <summary>Bridge power budget currently allocated by the Voltarium.</summary>
    public float PropulsionAvailable { get; internal set; }
    public float WeaponsAvailable { get; internal set; }
    public float ShieldsAvailable { get; internal set; }
    public float SensorsAvailable { get; internal set; }
    public float PropulsionPowerFactor => MaximumPropulsionDraw <= 0f ? 0f : PropulsionDraw / MaximumPropulsionDraw;
    public float AuxiliaryThrusterPowerFactor => AuxiliaryThrusterReserveDraw <= 0f ? 0f : AuxiliaryThrusterDraw / AuxiliaryThrusterReserveDraw;
    public float MainThrusterPowerFactor => MaximumMainThrusterDraw <= 0f ? 0f : MainThrusterDraw / MaximumMainThrusterDraw;
    public float MainThrusterAvailable => Math.Max(0f, PropulsionAvailable - AuxiliaryThrusterReserveDraw);
    public float WeaponsPowerFactor => MaximumWeaponsDraw <= 0f ? 0f : WeaponsDraw / MaximumWeaponsDraw;
    public float ShieldsPowerFactor => MaximumShieldsDraw <= 0f ? 0f : ShieldsDraw / MaximumShieldsDraw;
    public float SensorsPowerFactor => MaximumSensorsDraw <= 0f ? 0f : SensorsDraw / MaximumSensorsDraw;

    internal PowerState(float maximumMainThrusterDraw, float maximumReverseThrusterDraw,
        float maximumSideThrusterDrawPerAxis, float maximumWeaponsDraw,
        float maximumShieldsDraw, float maximumSensorsDraw)
    {
        MaximumMainThrusterDraw = maximumMainThrusterDraw;
        MaximumReverseThrusterDraw = maximumReverseThrusterDraw;
        MaximumSideThrusterDrawPerAxis = maximumSideThrusterDrawPerAxis;
        MaximumPropulsionDraw = maximumMainThrusterDraw + AuxiliaryThrusterReserveDraw;
        MaximumWeaponsDraw = maximumWeaponsDraw;
        MaximumShieldsDraw = maximumShieldsDraw;
        MaximumSensorsDraw = maximumSensorsDraw;
        // Operational default: a standard 125-PU ship can fully operate its 60-PU bridge
        // and 40-PU lance station while retaining a small shield feed. Voltarium may change it.
        PropulsionAllocationPercent = 48f;
        WeaponsAllocationPercent = 32f;
        ShieldsAllocationPercent = 20f;
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
