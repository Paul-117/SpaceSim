namespace SpaceSim.Core.Ships;

/// <summary>One tick of intent. FireLance is a pulse, not a queued fire request.</summary>
public readonly record struct ShipCommand(
    bool MainThrust = false,
    bool ReverseThrust = false,
    bool YawLeft = false,
    bool YawRight = false,
    bool FireLance = false,
    /// <summary>Moves the player lance mount left/right without applying ship yaw torque.</summary>
    bool AimLanceLeft = false,
    bool AimLanceRight = false,
    /// <summary>Normalised yaw-thruster authority. Keyboard and AI use 1; fine-control stations may use less.</summary>
    float YawIntensity = 1f);

public interface IShipControl
{
    ShipCommand ReadCommand();
}
