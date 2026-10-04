using Godot;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

public partial class GameOverOverlay : Control
{
    public Button MainMenuButton { get; } = CockpitButton.Create("Main Menu");
    public event Action? MainMenuRequested;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        MainMenuButton.Size = new Vector2(220, 52);
        MainMenuButton.Pressed += () => MainMenuRequested?.Invoke();
        AddChild(MainMenuButton);
        Hide();
    }

    public override void _Process(double delta)
    {
        MainMenuButton.Position = GetViewportRect().Size / 2f + new Vector2(-110, 78);
        if (Visible) QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.015f, 0.01f, 0.018f, 0.82f));
        Vector2 center = size / 2f;
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-106, -54), "GAME OVER",
            fontSize: 34, modulate: new Color("ff6b6b"));
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-112, -12), "SCHIFF ZERSTÖRT",
            fontSize: 20, modulate: ViewSettings.Text);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-145, 27),
            "Das Schiff wurde zerstoert.", fontSize: 13, modulate: ViewSettings.Muted);
    }
}
