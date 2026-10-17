using System.Text.Json;
using Godot;
using SpaceSim.Core.Simulation;

namespace SpaceSim.GodotClient.UI;

/// <summary>
/// Renders the authored loadout layout without embedding its coordinates in code.
/// The JSON exported by the layout editor remains the single source of truth.
/// </summary>
public partial class LoadoutUi : Control
{
    public bool IsOpen { get; private set; }
    public WorldState World { get; set; } = null!;

    private Font _font = ThemeDB.FallbackFont;
    private readonly Dictionary<string, Label> _labels = new();

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        var loadoutFont = new FontFile();
        if (loadoutFont.LoadDynamicFont("res://Assets/Fonts/Rajdhani-Regular.ttf") == Error.Ok) _font = loadoutFont;

        string source = Godot.FileAccess.GetFileAsString("res://Assets/Loadout/loadout-layout.json");
        List<LayoutItem> items = JsonSerializer.Deserialize<List<LayoutItem>>(source,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Loadout layout could not be read.");

        Control layout = GetNode<Control>("Layout");
        foreach (LayoutItem item in items)
        {
            switch (item.Type)
            {
                case "asset": AddAsset(layout, item); break;
                case "text": AddText(layout, item); break;
            }
        }
        Visible = false;
    }

    public override void _Process(double delta) => Visible = IsOpen;

    public override void _Input(InputEvent input)
    {
        if (!IsOpen || input is not InputEventKey { Pressed: true, Echo: false } key ||
            (key.PhysicalKeycode != Key.L && key.Keycode != Key.L)) return;

        Close();
        GetViewport().SetInputAsHandled();
    }

    public void Open()
    {
        UpdateValues();
        IsOpen = true;
        Visible = true;
        GetTree().Paused = true;
    }

    public void Close()
    {
        IsOpen = false;
        Visible = false;
        GetTree().Paused = false;
    }

    private void AddAsset(Control parent, LayoutItem item)
    {
        Texture2D texture = GD.Load<Texture2D>($"res://Assets/Loadout/{item.File}");
        if (texture is null)
        {
            GD.PushWarning($"Loadout asset not found: {item.File}");
            return;
        }

        var asset = new Sprite2D
        {
            Name = item.Id,
            Texture = texture,
            Position = new Vector2(item.X, item.Y),
            Centered = false,
            Scale = new Vector2(item.Width / texture.GetWidth(), item.Height / texture.GetHeight())
        };
        parent.AddChild(asset);
    }

    private void AddText(Control parent, LayoutItem item)
    {
        var label = new Label
        {
            Name = item.Id,
            Position = new Vector2(item.X, item.Y),
            Size = new Vector2(item.Width, item.Height),
            Text = item.Text ?? string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", item.FontSize);
        label.AddThemeColorOverride("font_color", new Color(item.Color ?? "ffffff"));
        parent.AddChild(label);
        _labels[item.Id] = label;
    }

    private void UpdateValues()
    {
        if (World is null) return;

        var ship = World.Ship;
        var tuning = ship.Tuning;
        var booster = tuning.MainBooster;
        var reactor = ship.Reactor.Definition;
        var sensor = World.PlayerSensor;
        var weapon = tuning.BowWeapon;
        var shields = tuning.Shield;
        var computer = World.PlayerBoardComputer;

        Set("booster-value-0", booster.Name);
        Set("booster-value-1", $"{booster.ThrustNewtons / 1_000f:0} kN");
        Set("booster-value-2", $"{booster.RampUpSeconds:0.0} s");
        Set("booster-value-3", $"{booster.MaximumSpeedMetersPerSecond:0} m/s");
        Set("booster-value-4", $"{booster.PowerDraw:0} PU");

        Set("reactor-value-0", reactor.Name);
        Set("reactor-value-1", $"{reactor.MaximumOutputPower:0} PU");
        Set("reactor-value-2", $"{reactor.MaximumFuelUsagePerMinute:0.0} U/min");
        Set("reactor-value-3", $"{reactor.RampUpSeconds:0.0} s");

        Set("sensor-value-0", sensor.Name);
        Set("sensor-value-1", $"{sensor.MinimumRangeMeters:0} m");
        Set("sensor-value-2", $"{sensor.MaximumRangeMeters:0} m");
        Set("sensor-value-3", $"{sensor.PowerUsage:0} PU");

        Set("weapon-value-0", weapon.Name);
        Set("weapon-value-1", $"{weapon.Damage:0}");
        Set("weapon-value-2", $"{weapon.RangeMeters:0} m");
        Set("weapon-value-3", $"{weapon.ChargeSeconds:0.0} s");
        Set("weapon-value-7", $"{weapon.PowerDraw:0} PU");

        Set("shields-value-0", shields.Name);
        Set("shields-value-1", $"{shields.MaximumHitPoints:0}");
        Set("shields-value-2", $"{shields.RechargeSeconds:0.0} s");
        Set("shields-value-3", $"{shields.RebootSeconds:0.0} s");
        Set("shields-value-4", $"{shields.PowerDraw:0} PU");

        Set("computer-value-0", computer.Class.ToString().ToUpperInvariant());
        Set("computer-value-1", ToRoman(computer.Mark));
    }

    private void Set(string id, string value)
    {
        if (_labels.TryGetValue(id, out Label? label)) label.Text = value;
    }

    private static string ToRoman(int value) => value switch
    {
        1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "V"
    };

    private sealed class LayoutItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string? Text { get; set; }
        public string? Color { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int FontSize { get; set; }
    }
}
