namespace SpaceSim.Core.Weapons;

public sealed class LanceState
{
    internal double ChargedSeconds { get; set; }
    public float ChargeFraction { get; internal set; }
    public bool IsReady => ChargeFraction >= 1f;
}
