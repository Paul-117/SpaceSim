using Godot;
using SpaceSim.Core.Ships;

namespace SpaceSim.GodotClient.Rendering;

public partial class ShipView : Node2D
{
    private ShipCommand _command;
    private bool _ready;
    private float _time;
    private float _lanceTurretAngleDegrees;

    public void Refresh(ShipCommand command, bool ready, float time, float lanceTurretAngleDegrees)
    {
        _command = command;
        _ready = ready;
        _time = time;
        _lanceTurretAngleDegrees = lanceTurretAngleDegrees;
        QueueRedraw();
    }

    public override void _Draw()
    {
        float flicker = 3f * MathF.Sin(_time * 47f);
        if (_command.MainThrust)
        {
            DrawColoredPolygon(new[] { new Vector2(-6, 13), new Vector2(0, 40 + flicker), new Vector2(6, 13) },
                ViewSettings.Alpha(ViewSettings.Cyan, 0.25f));
            DrawColoredPolygon(new[] { new Vector2(-3, 13), new Vector2(0, 30 + flicker), new Vector2(3, 13) },
                ViewSettings.Cyan);
        }
        if (_command.ReverseThrust)
        {
            DrawLine(new Vector2(-9, -5), new Vector2(-11, -18 - flicker), ViewSettings.Cyan, 3, true);
            DrawLine(new Vector2(9, -5), new Vector2(11, -18 - flicker), ViewSettings.Cyan, 3, true);
        }
        if (_command.YawLeft)
        {
            DrawLine(new Vector2(7, -7), new Vector2(17 + flicker, -7), ViewSettings.Cyan, 2, true);
            DrawLine(new Vector2(-9, 9), new Vector2(-19 - flicker, 9), ViewSettings.Cyan, 2, true);
        }
        if (_command.YawRight)
        {
            DrawLine(new Vector2(-7, -7), new Vector2(-17 - flicker, -7), ViewSettings.Cyan, 2, true);
            DrawLine(new Vector2(9, 9), new Vector2(19 + flicker, 9), ViewSettings.Cyan, 2, true);
        }

        Vector2[] hull = { new(0, -23), new(14, 15), new(0, 9), new(-14, 15) };
        DrawColoredPolygon(hull, new Color("172d3e"));
        DrawPolyline(new[] { hull[0], hull[1], hull[2], hull[3], hull[0] }, ViewSettings.Text, 1.5f, true);
        Vector2 lanceDirection = Vector2.Up.Rotated(Mathf.DegToRad(_lanceTurretAngleDegrees));
        DrawLine(new Vector2(0, 5), lanceDirection * 20, ViewSettings.Cyan, 2, true);
        DrawCircle(lanceDirection * 20, 2.5f, _ready ? ViewSettings.Cyan : ViewSettings.Amber);
    }
}
