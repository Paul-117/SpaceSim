namespace SpaceSim.GodotClient.Rendering;

/// <summary>Presentation-only wheel response for the player-centred tactical camera.</summary>
public static class TacticalCameraSettings
{
    /// <summary>Each wheel notch changes magnification by this factor; there is no gameplay zoom cap.</summary>
    public const float MouseWheelZoomFactor = 1.15f;
    /// <summary>Detached-camera movement in screen pixels per second at one-times zoom.</summary>
    public const float FreePanPixelsPerSecond = 700f;
}
