using System.Numerics;

namespace SpaceSim.Core.Navigation;

/// <summary>
/// Fixed neutral station inside an encounter. Its footprint is authoritative so
/// navigation, proximity interaction and rendering agree on the same location.
/// </summary>
public sealed class ShopState
{
    public const float DefaultWidthMeters = 1_000f;
    public const float DefaultDepthMeters = 1_000f;
    public const float DefaultInteractionRangeMeters = 100f;

    public Vector3 Position { get; }
    public float WidthMeters { get; }
    public float DepthMeters { get; }
    public float InteractionRangeMeters { get; }

    internal ShopState(Vector3 position, float widthMeters = DefaultWidthMeters,
        float depthMeters = DefaultDepthMeters, float interactionRangeMeters = DefaultInteractionRangeMeters)
    {
        Position = position;
        WidthMeters = widthMeters;
        DepthMeters = depthMeters;
        InteractionRangeMeters = interactionRangeMeters;
    }

    /// <summary>Distance to the rectangular station footprint in the X/Z flight plane.</summary>
    public float DistanceToFootprint(Vector3 position)
    {
        float x = MathF.Max(0f, MathF.Abs(position.X - Position.X) - WidthMeters / 2f);
        float z = MathF.Max(0f, MathF.Abs(position.Z - Position.Z) - DepthMeters / 2f);
        return MathF.Sqrt(x * x + z * z);
    }

    public bool IsInInteractionRange(Vector3 position) => DistanceToFootprint(position) <= InteractionRangeMeters;

    /// <summary>A direct contact is inside (or exactly on) the station's solid footprint.</summary>
    public bool IsColliding(Vector3 position) => DistanceToFootprint(position) <= 0f;
}
