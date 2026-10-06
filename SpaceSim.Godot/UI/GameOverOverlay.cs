using Godot;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

public partial class GameOverOverlay : Control
{
    public Button MainMenuButton { get; } = CockpitButton.Create("Main Menu");
    public Button RestartButton { get; } = CockpitButton.Create("Restart");
    public Button QuitButton { get; } = CockpitButton.Create("Quit");
    public event Action? MainMenuRequested;
    public event Action? RestartRequested;
    public event Action? QuitRequested;

    private string _headline = "GAME OVER";
    private string _status = "SCHIFF ZERSTOERT";
    private string _detail = "Das Schiff wurde zerstoert.";
    private Color _headlineColor = new Color("ff6b6b");

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        MainMenuButton.Size = new Vector2(220, 52);
        RestartButton.Size = new Vector2(220, 52);
        QuitButton.Size = new Vector2(220, 52);
        MainMenuButton.Pressed += () => MainMenuRequested?.Invoke();
        RestartButton.Pressed += () => RestartRequested?.Invoke();
        QuitButton.Pressed += () => QuitRequested?.Invoke();
        AddChild(MainMenuButton);
        AddChild(RestartButton);
        AddChild(QuitButton);
        Hide();
    }

    public override void _Process(double delta)
    {
        Vector2 center = GetViewportRect().Size / 2f;
        MainMenuButton.Position = center + new Vector2(-110, 78);
        RestartButton.Position = center + new Vector2(-110, 138);
        QuitButton.Position = center + new Vector2(-110, 198);
        if (Visible) QueueRedraw();
    }

    public void ShowResult(bool playerWon)
    {
        _headline = playerWon ? "DUELL GEWONNEN" : "GAME OVER";
        _status = playerWon ? "GEGNER ZERSTOERT" : "SCHIFF ZERSTOERT";
        _detail = playerWon ? "Das gegnerische Schiff wurde zerstoert." : "Das Schiff wurde zerstoert.";
        _headlineColor = playerWon ? ViewSettings.Cyan : new Color("ff6b6b");
        Show();
    }

    public override void _Draw()
    {
        Vector2 size = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, size), new Color(0.015f, 0.01f, 0.018f, 0.82f));
        Vector2 center = size / 2f;
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-140, -54), _headline,
            fontSize: 34, modulate: _headlineColor);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-112, -12), _status,
            fontSize: 20, modulate: ViewSettings.Text);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-145, 27), _detail,
            fontSize: 13, modulate: ViewSettings.Muted);
    }
}