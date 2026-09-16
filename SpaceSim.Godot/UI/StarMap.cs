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
        Vector2 panelSize = new(MathF.Min(960, viewport.X - 60), MathF.Min(510, viewport.Y - 220));
        _panel = new Rect2((viewport - panelSize) / 2, panelSize);
        int index = 0;
        foreach (var encounter in World.Encounters)
        {
            var button = _destinations[encounter.Id];
            bool current = encounter.Id == World.CurrentEncounter.Id;
            button.Disabled = current;
            button.Text = $"{encounter.Name}\n{encounter.Targets.Count} / {encounter.InitialTargetCount} Ziele"
                + (current ? "\nAKTUELL" : SelectedEncounterId == encounter.Id ? "\nAUSGEWÄHLT" : "\nSprungpunkt wählen");
            button.Size = new Vector2(230, 105);
            button.Position = PointPosition(index++) + new Vector2(-115, 36);
        }
        JumpButton.Disabled = !World.WarpDrive.IsReady || SelectedEncounterId is null ||
            SelectedEncounterId == World.CurrentEncounter.Id;
        JumpButton.Size = new Vector2(170, 44);
        JumpButton.Position = _panel.Position + new Vector2(_panel.Size.X - 202, _panel.Size.Y - 70);
        CloseButton.Size = new Vector2(235, 44);
        CloseButton.Position = _panel.Position + new Vector2(32, _panel.Size.Y - 70);
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

        Vector2 first = PointPosition(0);
        Vector2 second = PointPosition(1);
        DrawLine(first, second, ViewSettings.Line, 1, true);
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

        float y = _panel.Size.Y - 144;
        var drive = World.WarpDrive;
        Color chargeColor = drive.IsReady ? ViewSettings.Cyan : ViewSettings.Amber;
        Text(origin + new Vector2(32, y), "WARP DRIVE", 12, ViewSettings.Muted);
        Text(origin + new Vector2(155, y), drive.IsReady ? "100%  ·  READY" :
            $"{drive.ChargeFraction * 100:0}%  ·  Noch {drive.RemainingSeconds:0.0} s", 15, chargeColor);
        string status = SelectedEncounterId is { } selected ? $"Ziel: Encounter {selected}" : "Kein Sprungpunkt ausgewählt";
        Text(origin + new Vector2(_panel.Size.X - 300, y), status, 13, ViewSettings.Muted);
        DrawRect(new Rect2(origin + new Vector2(32, y + 16), new Vector2(_panel.Size.X - 64, 5)), ViewSettings.Line);
        DrawRect(new Rect2(origin + new Vector2(32, y + 16), new Vector2((_panel.Size.X - 64) * drive.ChargeFraction, 5)), chargeColor);
        Text(origin + new Vector2(290, _panel.Size.Y - 44), "Fortschritt bleibt erhalten.", 13, ViewSettings.Muted);
    }

    private Vector2 PointPosition(int index) => _panel.Position + new Vector2(_panel.Size.X * (index == 0 ? 0.27f : 0.73f), 158);
    private void Text(Vector2 position, string value, int size, Color color) =>
        DrawString(ThemeDB.FallbackFont, position, value, fontSize: size, modulate: color);
}
