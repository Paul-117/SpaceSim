namespace SpaceSim.Core.Navigation;

public sealed class WarpDriveState
{
    internal double ChargedSeconds { get; set; }
    public float ChargeFraction { get; internal set; }
    public double RemainingSeconds { get; internal set; }
    public bool IsReady { get; internal set; }
}
