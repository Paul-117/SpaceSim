namespace SpaceSim.Core.Weapons;

/// <summary>Player-owned horizontal lance mount offset. Negative is port/left, positive is starboard/right.</summary>
public sealed class LanceAimState
{
    public float YawOffsetDegrees { get; internal set; }
}
