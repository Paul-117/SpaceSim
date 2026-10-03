namespace SpaceSim.Core.Power;

/// <summary>Authoritative reactor output, ramp and fuel state.</summary>
public sealed class ReactorState
{
    public float MaximumOutputPower { get; }
    public float FuelCapacity { get; }
    /// <summary>The requested reactor level from the Voltarium, in percent.</summary>
    public float TargetOperatingLevelPercent { get; internal set; }
    /// <summary>The physical reactor level after its ramp, in percent.</summary>
    public float OperatingLevelPercent { get; internal set; }
    public float AvailablePower => Fuel <= 0f ? 0f : MaximumOutputPower * OperatingLevelPercent / 100f;
    public float CurrentDraw { get; internal set; }
    public float Fuel { get; internal set; }
    public float FuelUsagePerMinute { get; internal set; }
    public bool IsFuelDepleted => Fuel <= 0f;

    internal ReactorState(float maximumOutputPower, float operatingLevelPercent, float fuelCapacity)
    {
        MaximumOutputPower = maximumOutputPower;
        FuelCapacity = fuelCapacity;
        TargetOperatingLevelPercent = operatingLevelPercent;
        OperatingLevelPercent = operatingLevelPercent;
        Fuel = fuelCapacity;
    }
}
