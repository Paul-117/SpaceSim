using Godot;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>Live overlay: selection is UI state; only Jump produces a navigation request.</summary>
public partial class StarMap : Control
{
    public WorldState World { get; set; } = null!;
    public event Action<int>? JumpRequested;
    public event Action<bool>? SensoriumEnabledChanged;
    public Button JumpButton { get; } = CockpitButton.Create("Jump");
    public Button CloseButton { get; } = CockpitButton.Create("Zurück zum Flug  [Esc]");
    public bool SensoriumEnabled { get; set; } = true;
    private readonly Dictionary<int, Button> _destinations = new();
    private readonly Button _optionsButton = CockpitButton.Create("⚙");
    private readonly CheckButton _sensoriumToggle = new() { Text = "SENSORIUM", FocusMode = FocusModeEnum.None };
    public int? SelectedEncounterId { get; private set; }
    private Rect2 _panel;
    private Rect2 _optionsPanel;
    private Vector2 _destinationSize;
    private int _destinationColumns;
    private bool _optionsOpen;

    public Button DestinationButton(int id) => _destinations[id];

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;
        foreach (var encounter in World.Encounters)
        {
            var button = CockpitButton.Create(encounter.Name);
            button.Pressed += () => { SelectedEncounterId = encounter.Id; Refresh(); };
            _destinations.Add(encounter.Id, button);
            AddChild(button);
        }
        AddChild(JumpButton);
        AddChild(CloseButton);
        _optionsButton.TooltipText = "Brückenoptionen";
        _optionsButton.AddThemeFontSizeOverride("font_size", 19);
        _optionsButton.Pressed += () => { _optionsOpen = !_optionsOpen; Refresh(); };
        _sensoriumToggle.ButtonPressed = SensoriumEnabled;
        _sensoriumToggle.AddThemeFontSizeOverride("font_size", 12);
        _sensoriumToggle.AddThemeColorOverride("font_color", ViewSettings.Text);
        _sensoriumToggle.Toggled += enabled =>
        {
            SensoriumEnabled = enabled;
            SensoriumEnabledChanged?.Invoke(enabled);
        };
        AddChild(_sensoriumToggle);
        AddChild(_optionsButton);
        CloseButton.Pressed += Close;
        JumpButton.Pressed += () =>
        {
            if (World.HyperspacePhase == HyperspacePhase.SelectingDestination &&
                SelectedEncounterId is { } id &&
                (id != World.CurrentEncounter.Id || World.HyperspaceOriginEncounterId is null))
                JumpRequested?.Invoke(id);
        };
        Refresh();
        Hide();
    }

    public void Open()
    {
        SelectedEncounterId = null;
        _optionsOpen = false;
        Refresh();
        Show();
    }

    public void Close()
    {
        _optionsOpen = false;
        Hide();
    }
    public override void _Process(double delta)
    {
        if (Visible) Refresh();
    }

    public void Refresh()
    {
        Vector2 viewport = GetViewportRect().Size;
        Vector2 panelSize = new(MathF.Min(960, viewport.X - 60), MathF.Min(560, viewport.Y - 60));
        _panel = new Rect2((viewport - panelSize) / 2, panelSize);
        _destinationColumns = _panel.Size.X >= 880 ? Math.Max(1, World.Encounters.Count) : Math.Min(2, World.Encounters.Count);
        float gap = 14;
        float buttonWidth = (_panel.Size.X - 64 - gap * (_destinationColumns - 1)) / _destinationColumns;
        _destinationSize = new Vector2(MathF.Min(215, buttonWidth), 106);
        int index = 0;
        foreach (var encounter in World.Encounters)
        {
            var button = _destinations[encounter.Id];
            bool current = encounter.Id == World.CurrentEncounter.Id;
            button.Disabled = (current && World.HyperspaceOriginEncounterId is not null) ||
                              World.HyperspacePhase != HyperspacePhase.SelectingDestination;
            int activeEnemies = encounter.Enemies.Count(enemy => !enemy.IsDestroyed);
            string enemyStatus = encounter.Enemies.Count == 0 ? string.Empty :
                activeEnemies == 0 ? "\nGegner zerstÃ¶rt" : $"\n{activeEnemies} GEGNER AKTIV";
            button.Text = $"{encounter.Name}\n{encounter.Targets.Count} / {encounter.InitialTargetCount} Ziele"
                + (current ? "\nAKTUELL" : SelectedEncounterId == encounter.Id ? "\nAUSGEWÄHLT" : "\nSprungpunkt wählen");
            button.Size = _destinationSize;
            button.Position = PointPosition(index++) + new Vector2(-_destinationSize.X / 2, 26);
        }
        JumpButton.Disabled = World.HyperspacePhase != HyperspacePhase.SelectingDestination || SelectedEncounterId is null ||
            (SelectedEncounterId == World.CurrentEncounter.Id && World.HyperspaceOriginEncounterId is not null);
        JumpButton.Size = new Vector2(170, 44);
        JumpButton.Position = _panel.Position + new Vector2(_panel.Size.X - 202, _panel.Size.Y - 58);
        CloseButton.Size = new Vector2(235, 44);
        CloseButton.Position = _panel.Position + new Vector2(32, _panel.Size.Y - 58);
        CloseButton.Visible = World.IsPlayerInRealSpace;
        _optionsButton.Size = new Vector2(34, 34);
        _optionsButton.Position = _panel.Position + new Vector2(_panel.Size.X - 50, 17);
        _optionsPanel = new Rect2(_panel.Position + new Vector2(_panel.Size.X - 262, 74), new Vector2(244, 58));
        _sensoriumToggle.Position = _optionsPanel.Position + new Vector2(12, 24);
        _sensoriumToggle.Size = new Vector2(_optionsPanel.Size.X - 24, 28);
        _sensoriumToggle.Visible = _optionsOpen;
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 viewport = GetViewportRect().Size;
        // The star map is a modal navigation screen: bridge HUD and tactical world must not show through.
        DrawRect(new Rect2(Vector2.Zero, viewport), new Color("02040a"));
        DrawRect(_panel, new Color(0.025f, 0.045f, 0.073f, 0.96f));
        DrawRect(_panel, ViewSettings.Line, false, 1);
        Vector2 origin = _panel.Position;
        Text(origin + new Vector2(32, 39), "STERNENKARTE", 23, ViewSettings.Text);
        Text(origin + new Vector2(32, 66), "Sprungpunkt auswählen und mit Jump bestätigen.", 14, ViewSettings.Muted);
        Text(origin + new Vector2(_panel.Size.X - 278, 37), "LIVE  /  SIMULATION LÄUFT", 12, ViewSettings.Cyan);
        Text(origin + new Vector2(_panel.Size.X - 278, 61), $"{World.CurrentEncounter.Name}  ·  {World.TimeSeconds:0.0} s", 13, ViewSettings.Muted);

        if (_optionsOpen)
        {
            DrawRect(_optionsPanel, ViewSettings.Panel);
            DrawRect(_optionsPanel, ViewSettings.Line, false, 1);
            Text(_optionsPanel.Position + new Vector2(12, 17), "BRÜCKENOPTIONEN", 10, ViewSettings.Muted);
        }

        for (int i = 1; i < World.Encounters.Count; i++)
            DrawLine(PointPosition(i - 1), PointPosition(i), ViewSettings.Line, 1, true);
        for (int i = 0; i < 20; i++)
        {
            Vector2 star = origin + new Vector2(30 + (i * 137 % (int)(_panel.Size.X - 60)), 100 + i * 43 % 165);
            DrawCircle(star, 1, ViewSettings.Alpha(ViewSettings.Muted, 0.45f));
        }
        int index = 0;
        foreach (var encounter in World.Encounters)
        {
            Vector2 point = PointPosition(index++);
            bool current = encounter.Id == World.CurrentEncounter.Id;
            Color color = current ? ViewSettings.Cyan : ViewSettings.Amber;
            DrawCircle(point, 8, color);
            DrawArc(point, SelectedEncounterId == encounter.Id ? 25 : 18, 0, MathF.Tau, 40, color, 1.5f, true);
            if (current) Text(point + new Vector2(-39, -32), "POSITION", 11, color);
        }

        float y = _panel.Size.Y - 126;
        var drive = World.WarpDrive;
        Color chargeColor = drive.IsReady ? ViewSettings.Cyan : ViewSettings.Amber;
        Text(origin + new Vector2(32, y), "WARP DRIVE", 12, ViewSettings.Muted);
        Text(origin + new Vector2(155, y), drive.IsReady ? "100%  ·  READY" :
            $"{drive.ChargeFraction * 100:0}%  ·  Noch {drive.RemainingSeconds:0.0} s", 15, chargeColor);
        string status = SelectedEncounterId is { } selected ? $"Ziel: Encounter {selected}" : "Kein Sprungpunkt ausgewählt";
        Text(origin + new Vector2(32, y + 48), status, 13, ViewSettings.Muted);
        DrawRect(new Rect2(origin + new Vector2(32, y + 16), new Vector2(_panel.Size.X - 64, 5)), ViewSettings.Line);
        DrawRect(new Rect2(origin + new Vector2(32, y + 16), new Vector2((_panel.Size.X - 64) * drive.ChargeFraction, 5)), chargeColor);
    }

    private Vector2 PointPosition(int index)
    {
        int columns = Math.Max(1, _destinationColumns);
        int row = index / columns;
        int column = index % columns;
        float gap = 14;
        float cellWidth = (_panel.Size.X - 64 - gap * (columns - 1)) / columns;
        return _panel.Position + new Vector2(32 + cellWidth * (column + 0.5f) + gap * column, 132 + row * 132);
    }
    private void Text(Vector2 position, string value, int size, Color color) =>
        DrawString(ThemeDB.FallbackFont, position, value, fontSize: size, modulate: color);
}
