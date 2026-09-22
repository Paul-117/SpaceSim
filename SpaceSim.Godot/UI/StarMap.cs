using Godot;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>Live overlay: selection is UI state; only Jump produces a navigation request.</summary>
public partial class StarMap : Control
{
    public WorldState World { get; set; } = null!;
    public event Action<int>? JumpRequested;
    public Button JumpButton { get; } = CockpitButton.Create("Jump");
    public Button CloseButton { get; } = CockpitButton.Create("Zurück zum Flug  [Esc]");
    private readonly Dictionary<int, Button> _destinations = new();
    public int? SelectedEncounterId { get; private set; }
    private Rect2 _panel;
    private Vector2 _destinationSize;
    private int _destinationColumns;

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
        CloseButton.Pressed += Close;
        JumpButton.Pressed += () =>
        {
            if (World.WarpDrive.IsReady && SelectedEncounterId is { } id && id != World.CurrentEncounter.Id)
                JumpRequested?.Invoke(id);
        };
        Refresh();
        Hide();
    }

    public void Open()
    {
        SelectedEncounterId = null;
        Refresh();
        Show();
    }

    public void Close() => Hide();
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
            button.Disabled = current;
            int activeEnemies = encounter.Enemies.Count(enemy => !enemy.IsDestroyed);
            string enemyStatus = encounter.Enemies.Count == 0 ? string.Empty :
                activeEnemies == 0 ? "\nGegner zerstÃ¶rt" : $"\n{activeEnemies} GEGNER AKTIV";
            button.Text = $"{encounter.Name}\n{encounter.Targets.Count} / {encounter.InitialTargetCount} Ziele"
                + enemyStatus
                + (current ? "\nAKTUELL" : SelectedEncounterId == encounter.Id ? "\nAUSGEWÄHLT" : "\nSprungpunkt wählen");
            button.Size = _destinationSize;
            button.Position = PointPosition(index++) + new Vector2(-_destinationSize.X / 2, 26);
        }
        JumpButton.Disabled = !World.WarpDrive.IsReady || SelectedEncounterId is null ||
            SelectedEncounterId == World.CurrentEncounter.Id;
        JumpButton.Size = new Vector2(170, 44);
        JumpButton.Position = _panel.Position + new Vector2(_panel.Size.X - 202, _panel.Size.Y - 58);
        CloseButton.Size = new Vector2(235, 44);
        CloseButton.Position = _panel.Position + new Vector2(32, _panel.Size.Y - 58);
        QueueRedraw();
    }

    public override void _Draw()
    {
        Vector2 viewport = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, viewport), new Color(0.01f, 0.02f, 0.035f, 0.68f));
        DrawRect(_panel, new Color(0.025f, 0.045f, 0.073f, 0.96f));
        DrawRect(_panel, ViewSettings.Line, false, 1);
        Vector2 origin = _panel.Position;
        Text(origin + new Vector2(32, 39), "STERNENKARTE", 23, ViewSettings.Text);
        Text(origin + new Vector2(32, 66), "Sprungpunkt auswählen und mit Jump bestätigen.", 14, ViewSettings.Muted);
        Text(origin + new Vector2(_panel.Size.X - 240, 37), "LIVE  /  SIMULATION LÄUFT", 12, ViewSettings.Cyan);
        Text(origin + new Vector2(_panel.Size.X - 240, 61), $"{World.CurrentEncounter.Name}  ·  {World.TimeSeconds:0.0} s", 13, ViewSettings.Muted);

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
