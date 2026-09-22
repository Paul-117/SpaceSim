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
    public bool IsFocused { get; set; } = true;
    /// <summary>Presentation-only Camera2D magnification supplied by Flight.</summary>
    public float CameraZoom { get; set; } = 1f;
    public Button WarpButton { get; } = CockpitButton.Create("Warp Drive");
    public event Action? WarpMapRequested;
    private Font Font => ThemeDB.FallbackFont;
    private readonly StyleBoxFlat _panelStyle = new()
    {
        BgColor = ViewSettings.Panel,
        BorderColor = ViewSettings.Line,
        BorderWidthLeft = 1, BorderWidthTop = 1, BorderWidthRight = 1, BorderWidthBottom = 1,
        CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
        CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4
    };

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
        WarpButton.Pressed += () => WarpMapRequested?.Invoke();
        AddChild(WarpButton);
    }
    public override void _Process(double delta)
    {
        Vector2 viewport = GetViewportRect().Size;
        WarpButton.Position = new Vector2(viewport.X - 220, viewport.Y - 77);
        WarpButton.Size = new Vector2(190, 40);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (World is null) return;
        float width = GetViewportRect().Size.X;
        float height = GetViewportRect().Size.Y;
        // Keep targets and star trails from obscuring the cockpit readouts.
        DrawRect(new Rect2(0, 0, width, 154), new Color(0.0196f, 0.0314f, 0.0549f, 0.96f));
        DrawRect(new Rect2(0, height - 154, width, 154), new Color(0.0196f, 0.0314f, 0.0549f, 0.96f));
        Text(new Vector2(30, 35), "SPACESIM", 23, ViewSettings.Text);
        Text(new Vector2(165, 34), $"/  {World.CurrentEncounter.Name.ToUpperInvariant()}", 13, ViewSettings.Muted);
        Text(new Vector2(width - 226, 33), "FLIGHT LAB     /     V 1.7", 12, ViewSettings.Cyan);
        DrawLine(new Vector2(30, 49), new Vector2(width - 30, 49), ViewSettings.Line, 1);

        float column = (width - 60) / 4;
        Metric(30, 64, column - 10, "GESCHWINDIGKEIT", $"{World.Ship.Velocity.Length():0.0}", "m/s");
        Metric(30 + column, 64, column - 10, "WINKELGESCHWINDIGKEIT", $"{World.Ship.AngularVelocity.Y:+0.000;-0.000;0.000}", "rad/s");
        float lanceX = 30 + column * 2;
        Panel(new Rect2(lanceX, 64, column - 10, 83));
        Text(new Vector2(lanceX + 14, 85), "LANZE", 11, ViewSettings.Muted);
        var chargeColor = World.Lance.IsReady ? ViewSettings.Cyan : ViewSettings.Amber;
        Text(new Vector2(lanceX + 14, 117), $"{World.Lance.ChargeFraction * 100:0}%", 27, chargeColor);
        Text(new Vector2(lanceX + 117, 115), World.Lance.IsReady ? "READY" : "LÄDT", 13, chargeColor);
        DrawRect(new Rect2(lanceX + 14, 131, column - 38, 3), ViewSettings.Line);
        DrawRect(new Rect2(lanceX + 14, 131, (column - 38) * World.Lance.ChargeFraction, 3), chargeColor);
        Metric(30 + column * 3, 64, column - 10, $"TREFFER GESAMT · HIER {World.CurrentEncounter.HitCount}/{World.CurrentEncounter.InitialTargetCount}",
            $"{World.HitCount:000}", $"/ {World.Targets.Count} übrig");

        float heading = Mathf.PosMod(Mathf.RadToDeg(Mathf.Atan2(World.Ship.Forward.X, -World.Ship.Forward.Z)), 360);
        string scale = CameraZoom is >= 0.01f and <= 10_000f ? $"{CameraZoom:0.00}x" : $"{CameraZoom:0.###E+0}x";
        Text(new Vector2(30, height - 132),
            $"POS  X {World.Ship.Position.X,9:0.0}   Z {World.Ship.Position.Z,9:0.0} m    SCALE {scale}",
            12, ViewSettings.Muted);
        float speedLimit = Settings.MaximumNominalSpeedMetersPerSecond * World.Ship.Power.PropulsionPowerFactor * World.Ship.Systems.PropulsionCondition;
        string overspeed = World.Ship.Velocity.Length() > speedLimit + 0.1f ? "  OVERSPEED" : string.Empty;
        Text(new Vector2(30, height - 109), $"HULL {World.Ship.Hull.CurrentHull}/{World.Ship.Hull.MaximumHull}  SYS ENG {World.Ship.Systems.PropulsionCondition * 100:0}% WPN {World.Ship.Systems.WeaponsCondition * 100:0}% SHD {World.Ship.Systems.ShieldsCondition * 100:0}%", 11, ViewSettings.Muted);
        Text(new Vector2(width / 2 - 145, height - 109), $"SPEED {World.Ship.Velocity.Length():0}/{speedLimit:0} m/s{overspeed}", 11,
            overspeed.Length > 0 ? ViewSettings.Amber : ViewSettings.Cyan);
        Text(new Vector2(width - 242, height - 132), $"KURS {heading:000.0} DEG    SIM {World.TimeSeconds:0.0}s", 12, ViewSettings.Muted);
        DrawLine(new Vector2(30, height - 96), new Vector2(width - 30, height - 96), ViewSettings.Line, 1);

        KeyHint(new Vector2(30, height - 76), "W", "SCHUB", Command.MainThrust);
        KeyHint(new Vector2(169, height - 76), "S", "RUECKSCHUB", Command.ReverseThrust);
        KeyHint(new Vector2(340, height - 76), "A / D", "DREHMOMENT", Command.YawLeft || Command.YawRight, 56);
        KeyHint(new Vector2(561, height - 76), "SPACE", "Lance", Command.FireLance, 70);
        Text(new Vector2(30, height - 21), "TRAEGHEITSFLUG   /   Zum Bremsen Gegenschub geben. Rotation bleibt erhalten.", 12, ViewSettings.Muted);
        Text(new Vector2(width - 220, height - 21), World.WarpDrive.IsReady ? "WARP 100% / READY" :
            $"WARP {World.WarpDrive.ChargeFraction * 100:0}% / {World.WarpDrive.RemainingSeconds:0.0} s",
            12, World.WarpDrive.IsReady ? ViewSettings.Cyan : ViewSettings.Amber);
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
                $"{enemy.Difficulty.ToString().ToUpperInvariant()}  HULL {enemy.Ship.Hull.CurrentHull}/{enemy.Ship.Hull.MaximumHull}  SHD {enemy.Ship.Shield.CurrentShield:0}  RISK {ai?.CurrentRiskLevel.ToString().ToUpperInvariant()}",
                10, new Color("ff6577"));
        }
        if (!IsFocused)
            Text(new Vector2(width / 2 - 160, height / 2 + 70), "FENSTER INAKTIV  ·  Eingabe aus", 14, ViewSettings.Amber);
    }

    private void Metric(float x, float y, float width, string title, string value, string unit)
    {
        Panel(new Rect2(x, y, width, 83));
        Text(new Vector2(x + 14, y + 21), title, 11, ViewSettings.Muted);
        Text(new Vector2(x + 14, y + 58), value, 27, ViewSettings.Text);
        float valueWidth = Font.GetStringSize(value, fontSize: 27).X;
        Text(new Vector2(x + 24 + valueWidth, y + 57), unit, 12, ViewSettings.Muted);
    }

    private void Panel(Rect2 rect)
    {
        DrawStyleBox(_panelStyle, rect);
    }

    private void KeyHint(Vector2 origin, string key, string label, bool active, float width = 30)
    {
        var color = active ? ViewSettings.Cyan : ViewSettings.Muted;
        DrawRect(new Rect2(origin, new Vector2(width, 28)), active ? new Color("173b48") : ViewSettings.Panel);
        DrawRect(new Rect2(origin, new Vector2(width, 28)), active ? ViewSettings.Cyan : ViewSettings.Line, false, 1);
        Text(origin + new Vector2(8, 19), key, 12, color);
        Text(origin + new Vector2(width + 10, 19), label, 13, ViewSettings.Text);
    }

    private void Text(Vector2 position, string text, int size, Color color) =>
        DrawString(Font, position, text, fontSize: size, modulate: color);
}
