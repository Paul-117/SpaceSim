using System.Text.Json;
using Godot;
using SpaceSim.Core.Simulation;

namespace SpaceSim.GodotClient.UI;

/// <summary>
/// Presentation-only shop overlay. Purchases will be introduced later; the UI
/// already reads the authoritative player-credit balance from the WorldState.
/// </summary>
public partial class ShopUi : Control
{
    public bool IsOpen { get; private set; }
    public WorldState World { get; set; } = null!;

    private readonly Dictionary<string, Label> _labels = new();
    private Label _fallbackStatus = null!;
    private Font _font = ThemeDB.FallbackFont;
    private bool _hasAuthoredLayout;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        MouseFilter = MouseFilterEnum.Stop;
        var shopFont = new FontFile();
        if (shopFont.LoadDynamicFont("res://Assets/Fonts/Rajdhani-Regular.ttf") == Error.Ok) _font = shopFont;

        _fallbackStatus = GetNode<Label>("FallbackStatus");
        string source = Godot.FileAccess.GetFileAsString("res://Assets/Shop/shop-layout.json");
        List<LayoutItem> items = JsonSerializer.Deserialize<List<LayoutItem>>(source,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        Control layout = GetNode<Control>("Layout");
        foreach (LayoutItem item in items)
        {
            switch (item.Type)
            {
                case "asset": AddAsset(layout, item); break;
                case "text": AddText(layout, item); break;
            }
        }
        _hasAuthoredLayout = items.Count > 0;
        _fallbackStatus.Visible = !_hasAuthoredLayout;
        Visible = false;
    }

    public override void _Process(double delta)
    {
        Visible = IsOpen;
        if (IsOpen) UpdateValues();
    }

    public override void _Input(InputEvent input)
    {
        if (!IsOpen || input is not InputEventKey { Pressed: true, Echo: false } key ||
            (key.PhysicalKeycode != Key.E && key.Keycode != Key.E && key.Keycode != Key.Escape)) return;

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
        Texture2D texture = GD.Load<Texture2D>($"res://Assets/Shop/{item.File}");
        if (texture is null)
        {
            GD.PushWarning($"Shop asset not found: {item.File}");
            return;
        }
        parent.AddChild(new Sprite2D
        {
            Name = item.Id,
            Texture = texture,
            Position = new Vector2(item.X, item.Y),
            Centered = false,
            Scale = new Vector2(item.Width / texture.GetWidth(), item.Height / texture.GetHeight())
        });
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
        string credits = World.Credits.ToString();
        Set("shop-credits", credits);
        Set("shop-current-credits", credits);
        if (!_hasAuthoredLayout)
            _fallbackStatus.Text = $"SHOP\n\nCREDITS  {credits}\n\nSHOP INVENTORY COMING SOON\n\n[E] / [ESC] CLOSE";
    }

    private void Set(string id, string value)
    {
        if (_labels.TryGetValue(id, out Label? label)) label.Text = value;
    }

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
