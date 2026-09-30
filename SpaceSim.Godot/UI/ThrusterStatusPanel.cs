using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>Presentation-only bridge status for the four propulsion groups.</summary>
public partial class ThrusterStatusPanel : Control
{
    public WorldState World { get; set; } = null!;
    public ShipCommand Command { get; set; }
    private Font Font => ThemeDB.FallbackFont;
    private float _mainThrust;
    private float _mainStatus;
    private float _auxiliaryStatus;
    private float _availableEnergy;

    public override void _Process(double delta)
    {
        Vector2 viewport = GetViewportRect().Size;
        Size = new Vector2(320, 184);
        Position = new Vector2(30, viewport.Y - Size.Y * 1.25f);
        if (World is not null)
        {
            _availableEnergy = World.Ship.Power.PropulsionAvailable;
            _mainStatus = Ratio(_availableEnergy, World.Ship.Power.MaximumPropulsionDraw);
            _auxiliaryStatus = Ratio(_availableEnergy, World.Ship.Power.AuxiliaryThrusterReserveDraw);
            float mainInput = Command.MainThrust ? Math.Clamp(Command.MainThrustIntensity, 0f, 1f) : 0f;
            _mainThrust = mainInput * World.Ship.Power.MainThrusterPowerFactor *
                World.Ship.Systems.PropulsionCondition;
        }
        QueueRedraw();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(.0196f, .0314f, .0549f, .94f));
        DrawRect(new Rect2(Vector2.Zero, Size), ViewSettings.Line, false, 1);
        Text(new Vector2(12, 20), "AVAILABLE ENERGY:", 11, ViewSettings.Muted);
        Text(new Vector2(220, 20), $"{_availableEnergy:0.0} PU", 11, ViewSettings.Text);
        DrawLine(new Vector2(12, 28), new Vector2(Size.X - 12, 28), ViewSettings.Line, 1);
        Header(50, "STARBOARD THRUSTERS", _auxiliaryStatus);
        Header(74, "PORT THRUSTERS", _auxiliaryStatus);
        Header(98, "REVERSE THRUSTERS", _auxiliaryStatus);
        Header(122, "MAIN THRUSTERS", _mainStatus);
        Text(new Vector2(12, 150), "THRUST", 10, ViewSettings.Muted);
        Text(new Vector2(274, 150), $"{_mainThrust * 100:0}%", 10, ViewSettings.Text);
        Rect2 track = new(12, 157, 296, 12);
        DrawRect(track, new Color("14283b"));
        DrawRect(new Rect2(track.Position, new Vector2(track.Size.X * _mainThrust, track.Size.Y)), new Color("3b9cff"));
        DrawRect(track, ViewSettings.Line, false, 1);
    }

    private void Header(float y, string name, float availability)
    {
        Text(new Vector2(12, y), name, 11, ViewSettings.Muted);
        (string text, Color color) = Status(availability);
        Text(new Vector2(190, y), "STATUS:", 10, ViewSettings.Muted);
        Text(new Vector2(242, y), text, 10, color);
    }

    private static (string Text, Color Color) Status(float availability) => availability switch
    {
        >= .999f => ("ONLINE", ViewSettings.Green),
        > 0f => ("LIMITED", ViewSettings.Amber),
        _ => ("OFFLINE", new Color("ff6577"))
    };

    private static float Ratio(float value, float maximum) => maximum <= 0f ? 0f : Math.Clamp(value / maximum, 0f, 1f);

    private void Text(Vector2 position, string text, int size, Color color) =>
        DrawString(Font, position, text, fontSize: size, modulate: color);
}
