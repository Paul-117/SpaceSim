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
    /// <summary>Normalised main-engine demand. Full direct commands use 1; the bridge ramps this while held.</summary>
    float MainThrustIntensity = 1f,
    /// <summary>Normalised reverse-engine demand. Full direct commands use 1; the bridge ramps this while held.</summary>
    float ReverseThrustIntensity = 1f,
    /// <summary>Normalised yaw-thruster authority. Keyboard and AI use 1; fine-control stations may use less.</summary>
    float YawIntensity = 1f);

public interface IShipControl
{
    ShipCommand ReadCommand();
}
