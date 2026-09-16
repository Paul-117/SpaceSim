using Godot;

namespace SpaceSim.GodotClient.Rendering;

/// <summary>Stateless hashed tiles: bounded drawing cost, no finite background edge.</summary>
public partial class Starfield : Node2D
{
    public Vector2 CameraPosition { get; set; }

    public override void _Process(double delta) => QueueRedraw();

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color("05080e"));
        DrawLayer(size, 115f, 0.12f, 0.4f, 17);
        DrawLayer(size, 185f, 0.38f, 0.72f, 73);
    }

    private void DrawLayer(Vector2 size, float cellSize, float parallax, float brightness, uint seed)
    {
        Vector2 offset = CameraPosition * parallax - size / 2f;
        int startX = Mathf.FloorToInt(offset.X / cellSize);
        int startY = Mathf.FloorToInt(offset.Y / cellSize);
        int columns = Mathf.CeilToInt(size.X / cellSize) + 2;
        int rows = Mathf.CeilToInt(size.Y / cellSize) + 2;
        for (int y = startY; y <= startY + rows; y++)
        for (int x = startX; x <= startX + columns; x++)
        {
            uint hash = Hash(unchecked((uint)x * 73856093u ^ (uint)y * 19349663u ^ seed));
            float localX = (hash & 0xffff) / 65535f;
            float localY = (hash >> 16) / 65535f;
            Vector2 point = new Vector2(x + localX, y + localY) * cellSize - offset;
            float alpha = brightness * (0.4f + localX * 0.6f);
            DrawCircle(point, localY > 0.93f ? 1.6f : 0.8f, new Color(0.7f, 0.83f, 0.95f, alpha));
            if (localY > 0.975f)
            {
                var color = new Color(0.5f, 0.7f, 0.9f, alpha * 0.4f);
                DrawLine(point - Vector2.Right * 4, point + Vector2.Right * 4, color, 1, true);
                DrawLine(point - Vector2.Down * 4, point + Vector2.Down * 4, color, 1, true);
            }
        }
    }

    private static uint Hash(uint value)
    {
        value ^= value >> 16;
        value = unchecked(value * 0x7feb352du);
        value ^= value >> 15;
        value = unchecked(value * 0x846ca68bu);
        return value ^ (value >> 16);
    }
}
