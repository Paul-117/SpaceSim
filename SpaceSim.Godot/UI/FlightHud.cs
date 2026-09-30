using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

public partial class FlightHud : Control
{
    public WorldState World { get; set; } = null!;
    public SimulationSettings Settings { get; set; } = null!;
    public ShipCommand Command { get; set; }
    public bool ArmariumOnline { get; set; }
    public bool IsFocused { get; set; } = true;
    public float CameraZoom { get; set; } = 1f;
    public Button WarpButton { get; } = CockpitButton.Create("Warp Drive");
    public event Action? WarpMapRequested;
    private Font Font => ThemeDB.FallbackFont;
    private readonly StyleBoxFlat _panelStyle = new()
    {
        BgColor = ViewSettings.Panel,
        BorderColor = ViewSettings.Line,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        WarpButton.AddThemeStyleboxOverride("normal", WarpStyle(new Color(0f, 0f, 0f, 0f), ViewSettings.Cyan));
        WarpButton.AddThemeStyleboxOverride("hover", WarpStyle(new Color(.1f, .24f, .31f, .25f), ViewSettings.Cyan));
        WarpButton.AddThemeStyleboxOverride("pressed", WarpStyle(new Color(.15f, .34f, .42f, .35f), ViewSettings.Cyan));
        WarpButton.Pressed += () => WarpMapRequested?.Invoke();
        AddChild(WarpButton);
    }

    public override void _Process(double delta)
    {
        Vector2 viewport = GetViewportRect().Size;
        WarpButton.Position = new Vector2(viewport.X / 2f - 100, 10);
        WarpButton.Size = new Vector2(200, 30);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (World is null) return;
        float width = GetViewportRect().Size.X;
        float height = GetViewportRect().Size.Y;
        DrawRect(new Rect2(0, 0, width, 154), new Color(.0196f, .0314f, .0549f, .96f));
        DrawRect(new Rect2(0, height - 154, width, 154), new Color(.0196f, .0314f, .0549f, .96f));
        Text(new Vector2(30, 35), "SPACESIM", 23, ViewSettings.Text);
        Text(new Vector2(165, 34), $"/  {World.CurrentEncounter.Name.ToUpperInvariant()}", 13, ViewSettings.Muted);
        Text(new Vector2(width - 226, 33), "FLIGHT LAB     /     V 1.9.4", 12, ViewSettings.Cyan);
        Text(new Vector2(width - 510, 33), ArmariumOnline ? "ARMARIUM ONLINE" : "ARMARIUM OFFLINE", 12,
            ArmariumOnline ? ViewSettings.Green : ViewSettings.Muted);
        DrawWarpLoadBar(width);
        DrawLine(new Vector2(30, 49), new Vector2(width - 30, 49), ViewSettings.Line, 1);

        const float metricWidth = 236;
        Metric(30, 64, metricWidth, "GESCHWINDIGKEIT", $"{World.Ship.Velocity.Length():0.0}", "m/s");
        Metric(276, 64, metricWidth, "WINKELGESCHWINDIGKEIT", $"{World.Ship.AngularVelocity.Y:+0.000;-0.000;0.000}", "rad/s");

        float heading = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(World.Ship.Forward.X, -World.Ship.Forward.Z)), 360);
        string scale = CameraZoom is >= .01f and <= 10_000f ? $"{CameraZoom:0.00}x" : $"{CameraZoom:0.###E+0}x";
        RightText(width - 30, height - 154,
            $"POS  X {World.Ship.Position.X,9:0.0}   Z {World.Ship.Position.Z,9:0.0} m    SCALE {scale}", 12, ViewSettings.Muted);
        RightText(width - 30, height - 132,
            $"INTEGRITY {World.Ship.Hull.CurrentHull}/{World.Ship.Hull.MaximumHull}  SYS ENG {World.Ship.Systems.PropulsionCondition * 100:0}% WPN {World.Ship.Systems.WeaponsCondition * 100:0}% SHD {World.Ship.Systems.ShieldsCondition * 100:0}%", 11, ViewSettings.Muted);
        RightText(width - 30, height - 109, $"KURS {heading:000.0} DEG    SIM {World.TimeSeconds:0.0}s", 12, ViewSettings.Muted);
        DrawLine(new Vector2(30, height - 96), new Vector2(width - 30, height - 96), ViewSettings.Line, 1);
        var enemies = World.CurrentEnemies.ToArray();
        if (enemies.FirstOrDefault() is { } enemy)
        {
            string state = World.CurrentEncounter.GetEnemyAi(enemy.EnemyId)?.CurrentState.ToString().ToUpperInvariant() ?? "-";
            Rect2 enemyPanel = new(30, 164, 340, 62);
            Panel(enemyPanel);
            Text(enemyPanel.Position + new Vector2(12, 22),
                $"ENEMY {enemies.Length} / E{enemy.EnemyId} {state} / LANCE {enemy.Lance.ChargeFraction * 100:0}%",
                11, new Color("ff6577"));
            var ai = World.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
            Text(enemyPanel.Position + new Vector2(12, 45),
                $"{enemy.Difficulty.ToString().ToUpperInvariant()}  HULL {enemy.Ship.Hull.CurrentHull}/{enemy.Ship.Hull.MaximumHull}  SHD {enemy.Ship.Shield.CurrentShield:0}  {(ai?.IsPlayerDetected == true ? "COMBAT" : "PATROL")}",
                10, new Color("ff6577"));
        }
        if (!IsFocused) Text(new Vector2(width / 2 - 160, height / 2 + 70), "FENSTER INAKTIV  /  Eingabe aus", 14, ViewSettings.Amber);
    }

    private void Metric(float x, float y, float width, string title, string value, string unit)
    {
        Panel(new Rect2(x, y, width, 83));
        Text(new Vector2(x + 14, y + 21), title, 11, ViewSettings.Muted);
        Text(new Vector2(x + 14, y + 58), value, 27, ViewSettings.Text);
        Text(new Vector2(x + 24 + Font.GetStringSize(value, fontSize: 27).X, y + 57), unit, 12, ViewSettings.Muted);
    }

    private void Panel(Rect2 rect) => DrawStyleBox(_panelStyle, rect);
    private void DrawWarpLoadBar(float width)
    {
        Rect2 bounds = new(width / 2f - 100, 10, 200, 30);
        Color fill = World.WarpDrive.IsReady ? ViewSettings.Cyan : ViewSettings.Amber;
        DrawRect(bounds, new Color("102432"));
        DrawRect(new Rect2(bounds.Position, new Vector2(bounds.Size.X * World.WarpDrive.ChargeFraction, bounds.Size.Y)),
            ViewSettings.Alpha(fill, .5f));
        DrawRect(bounds, fill, false, 1);
    }
    private static StyleBoxFlat WarpStyle(Color background, Color border) => new()
    {
        BgColor = background, BorderColor = border,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5, CornerRadiusTopRight = 5, CornerRadiusBottomLeft = 5, CornerRadiusBottomRight = 5
    };
    private void Text(Vector2 position, string text, int size, Color color) => DrawString(Font, position, text, fontSize: size, modulate: color);
    private void RightText(float right, float y, string text, int size, Color color) =>
        Text(new Vector2(right - Font.GetStringSize(text, fontSize: size).X, y), text, size, color);
}
