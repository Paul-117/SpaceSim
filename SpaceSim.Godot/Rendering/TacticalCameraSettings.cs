namespace SpaceSim.GodotClient.Rendering;

/// <summary>Presentation-only limits for the player-centred tactical camera.</summary>
public static class TacticalCameraSettings
{
    public const float MinimumZoom = 0.45f;
    public const float MaximumZoom = 1.80f;
    public const float MouseWheelZoomStep = 0.10f;
}
