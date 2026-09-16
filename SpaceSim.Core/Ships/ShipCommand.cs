namespace SpaceSim.Core.Ships;

/// <summary>One tick of intent. FireLance is a pulse, not a queued fire request.</summary>
public readonly record struct ShipCommand(
    bool MainThrust = false,
    bool ReverseThrust = false,
    bool YawLeft = false,
    bool YawRight = false,
    bool FireLance = false);

public interface IShipControl
{
    ShipCommand ReadCommand();
}
