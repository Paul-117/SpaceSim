using Godot;
using NVector3 = System.Numerics.Vector3;

namespace SpaceSim.GodotClient.Rendering;

public static class ViewSettings
{
    public const float PixelsPerMeter = 0.85f;
    public const float BeamDurationSeconds = 0.16f;
    public const float ImpactDurationSeconds = 0.35f;
    public static readonly Color Cyan = new("6ee7ef");
    public static readonly Color Amber = new("ffb66b");
    public static readonly Color Green = new("6ce3a0");
    public static readonly Color Text = new("dde9ef");
    public static readonly Color Muted = new("8199ae");
    public static readonly Color Line = new("20394c");
    public static readonly Color Panel = new(0.035f, 0.059f, 0.090f, 0.95f);

    public static Vector2 Project(NVector3 position) => new(position.X * PixelsPerMeter, position.Z * PixelsPerMeter);
    public static NVector3 Unproject(Vector2 position) => new(position.X / PixelsPerMeter, 0f, position.Y / PixelsPerMeter);
    public static Color Alpha(Color color, float alpha) => new(color.R, color.G, color.B, alpha);
}
