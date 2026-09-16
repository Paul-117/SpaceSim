using Godot;
using SpaceSim.Core.Simulation;
using NVector3 = System.Numerics.Vector3;

namespace SpaceSim.GodotClient.Rendering;

public partial class ArenaView : Node2D
{
    private sealed record Beam(Vector2 Origin, Vector2 End, WeaponOwner Owner) { public float Age; }
    private sealed record Impact(Vector2 Position) { public float Age; }
    private readonly List<Beam> _beams = new();
    private readonly List<Impact> _impacts = new();
    public WorldState World { get; set; } = null!;
    public Vector2 ShipPosition { get; set; }
    public Vector2 ShipForward { get; set; } = Vector2.Up;

    public void ShowEvents(IReadOnlyList<SimulationEvent> events)
    {
        foreach (var item in events)
        {
            if (item is EncounterChanged)
            {
                _beams.Clear();
                _impacts.Clear();
            }
            if (item is WeaponFired shot)
                _beams.Add(new Beam(ViewSettings.Project(shot.Origin), ViewSettings.Project(shot.End), shot.Owner));
            if (item is TargetHit hit)
                _impacts.Add(new Impact(ViewSettings.Project(hit.Position)));
            if (item is EnemyDestroyed destroyed)
                _impacts.Add(new Impact(ViewSettings.Project(destroyed.Position)));
        }
    }

    public override void _Process(double delta)
    {
        foreach (var beam in _beams) beam.Age += (float)delta;
        foreach (var impact in _impacts) impact.Age += (float)delta;
        _beams.RemoveAll(b => b.Age >= ViewSettings.BeamDurationSeconds);
        _impacts.RemoveAll(i => i.Age >= ViewSettings.ImpactDurationSeconds);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (World is null) return;
        DrawAimAndDrift();
        DrawEnemy();
        DrawEnemyIndicator();
        foreach (var target in World.Targets)
        {
            Vector2 center = ViewSettings.Project(target.Position);
            float radius = target.RadiusMeters * ViewSettings.PixelsPerMeter;
            var color = ViewSettings.Amber;
            DrawCircle(center, radius, ViewSettings.Alpha(color, 0.055f));
            DrawArc(center, radius, 0, MathF.Tau, 40, ViewSettings.Alpha(color, 0.8f), 1.4f, true);
            DrawArc(center, radius * 0.45f, 0, MathF.Tau, 24, ViewSettings.Alpha(color, 0.35f), 1, true);
            DrawCircle(center, 2, color);
            foreach (var direction in new[] { Vector2.Up, Vector2.Down, Vector2.Left, Vector2.Right })
                DrawLine(center + direction * (radius + 4), center + direction * (radius + 9), color, 1, true);
            float distance = NVector3.Distance(target.Position, World.Ship.Position);
            DrawString(ThemeDB.FallbackFont, center + new Vector2(radius + 15, 4),
                $"T{target.Id:00}  {distance:0} m", fontSize: 12, modulate: ViewSettings.Muted);
        }
        DrawNearestIndicator();
        foreach (var beam in _beams)
        {
            float alpha = 1f - beam.Age / ViewSettings.BeamDurationSeconds;
            Color beamColor = beam.Owner == WeaponOwner.Enemy ? new Color("ff6577") : ViewSettings.Cyan;
            DrawLine(beam.Origin, beam.End, ViewSettings.Alpha(beamColor, alpha * 0.12f), 14, true);
            DrawLine(beam.Origin, beam.End, ViewSettings.Alpha(beamColor, alpha * 0.55f), 5, true);
            DrawLine(beam.Origin, beam.End, new Color(0.9f, 1f, 1f, alpha), 1.8f, true);
        }
        foreach (var impact in _impacts)
        {
            float progress = impact.Age / ViewSettings.ImpactDurationSeconds;
            DrawArc(impact.Position, 14 + 35 * progress, 0, MathF.Tau, 40,
                ViewSettings.Alpha(ViewSettings.Amber, 1f - progress), 2, true);
        }
    }

    public void ResetVisuals()
    {
        _beams.Clear();
        _impacts.Clear();
    }

    private void DrawEnemy()
    {
        foreach (var enemy in World.CurrentEnemies)
            DrawEnemy(enemy);
    }

    private void DrawEnemy(SpaceSim.Core.Combat.EnemyShipState enemy)
    {
        Vector2 center = ViewSettings.Project(enemy.Ship.Position);
        Vector2 forward = new(enemy.Ship.Forward.X, enemy.Ship.Forward.Z);
        Vector2 right = new(-forward.Y, forward.X);
        Vector2[] hull =
        {
            center + forward * 23,
            center - forward * 14 + right * 14,
            center - forward * 8,
            center - forward * 14 - right * 14,
            center + forward * 23
        };
        Color enemyColor = new("ff6577");
        DrawColoredPolygon(hull[..^1], new Color("3c1722"));
        DrawPolyline(hull, enemyColor, 1.8f, true);
        DrawLine(center, center + forward * 18, enemyColor, 2, true);
        var ai = World.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
        if (ai?.LastCommand.MainThrust == true)
            DrawLine(center - forward * 15, center - forward * 32, new Color("ffb15c"), 4, true);
        DrawString(ThemeDB.FallbackFont, center + right * 25 + new Vector2(4, 4),
            $"ENEMY {enemy.EnemyId}  {ai?.CurrentState.ToString().ToUpperInvariant()}  LANCE {enemy.Lance.ChargeFraction * 100:0}%",
            fontSize: 12, modulate: enemyColor);
    }

    private void DrawEnemyIndicator()
    {
        foreach (var enemy in World.CurrentEnemies)
            DrawEnemyIndicator(enemy);
    }

    private void DrawEnemyIndicator(SpaceSim.Core.Combat.EnemyShipState enemy)
    {
        Vector2 delta = ViewSettings.Project(enemy.Ship.Position) - ShipPosition;
        Vector2 half = GetViewportRect().Size / 2f - new Vector2(80, 170);
        if (MathF.Abs(delta.X) <= half.X && MathF.Abs(delta.Y) <= half.Y) return;
        float scale = MathF.Min(half.X / MathF.Max(1, MathF.Abs(delta.X)),
            half.Y / MathF.Max(1, MathF.Abs(delta.Y)));
        Vector2 tip = ShipPosition + delta * scale;
        Vector2 direction = delta.Normalized();
        Color color = new("ff6577");
        DrawPolyline(new[] { tip - direction.Rotated(0.5f) * 14, tip,
            tip - direction.Rotated(-0.5f) * 14 }, color, 2.5f, true);
        float distance = NVector3.Distance(enemy.Ship.Position, World.Ship.Position);
        Vector2 labelOffset = tip.X >= ShipPosition.X ? new Vector2(-145, 4) : new Vector2(12, 4);
        DrawString(ThemeDB.FallbackFont, tip + labelOffset, $"ENEMY  {distance:0} m",
            fontSize: 12, modulate: color);
    }

    private void DrawAimAndDrift()
    {
        for (float distance = 42; distance < 280; distance += 18)
            DrawLine(ShipPosition + ShipForward * distance, ShipPosition + ShipForward * (distance + 5),
                ViewSettings.Alpha(ViewSettings.Cyan, World.Lance.IsReady ? 0.3f : 0.12f), 1, true);
        Vector2 velocity = ViewSettings.Project(World.Ship.Velocity);
        if (velocity.Length() < 0.5f) return;
        Vector2 direction = velocity.Normalized();
        Vector2 tip = ShipPosition + direction * Mathf.Clamp(velocity.Length() * 1.5f, 45, 130);
        DrawLine(ShipPosition + direction * 32, tip, ViewSettings.Alpha(ViewSettings.Cyan, 0.45f), 1, true);
        DrawPolyline(new[] { tip - direction.Rotated(0.5f) * 8, tip, tip - direction.Rotated(-0.5f) * 8 },
            ViewSettings.Cyan, 1, true);
    }

    private void DrawNearestIndicator()
    {
        var nearest = World.Targets.MinBy(t => NVector3.DistanceSquared(t.Position, World.Ship.Position));
        if (nearest is null) return;
        Vector2 delta = ViewSettings.Project(nearest.Position) - ShipPosition;
        Vector2 half = GetViewportRect().Size / 2f - new Vector2(65, 165);
        if (MathF.Abs(delta.X) <= half.X && MathF.Abs(delta.Y) <= half.Y) return;
        float scale = MathF.Min(half.X / MathF.Max(1, MathF.Abs(delta.X)), half.Y / MathF.Max(1, MathF.Abs(delta.Y)));
        Vector2 tip = ShipPosition + delta * scale;
        Vector2 direction = delta.Normalized();
        DrawPolyline(new[] { tip - direction.Rotated(0.5f) * 12, tip, tip - direction.Rotated(-0.5f) * 12 },
            ViewSettings.Amber, 2, true);
        DrawString(ThemeDB.FallbackFont, tip + new Vector2(12, 4), $"T{nearest.Id:00}", fontSize: 12,
            modulate: ViewSettings.Amber);
    }
}
