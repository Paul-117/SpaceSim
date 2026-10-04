using Godot;
using SpaceSim.Core.Simulation;
using NVector3 = System.Numerics.Vector3;

namespace SpaceSim.GodotClient.Rendering;

public partial class ArenaView : Node2D
{
    private sealed record Beam(Vector2 Origin, Vector2 FadeStart, Vector2 VisualEnd, WeaponOwner Owner) { public float Age; }
    private sealed record Impact(Vector2 Position) { public float Age; }
    private readonly List<Beam> _beams = new();
    private readonly List<Impact> _impacts = new();
    public WorldState World { get; set; } = null!;
    public Vector2 ShipPosition { get; set; }
    public Vector2 ShipForward { get; set; } = Vector2.Up;
    /// <summary>World-space centre of the visible tactical camera rectangle.</summary>
    public Vector2 CameraPosition { get; set; }
    /// <summary>Detached mode suppresses ship-centred off-screen enemy guidance.</summary>
    public bool IsFreeCamera { get; set; }
    public bool HidePlayerGuidance { get; set; }
    public Vector2? HyperspaceEntryPoint { get; set; }
    public Vector2? HyperspaceEnemyPoint { get; set; }
    /// <summary>Presentation-only contact selection made from the bridge map.</summary>
    public int? SelectedEnemyId { get; set; }
    /// <summary>Current Camera2D magnification; indicators use it to derive visible world bounds.</summary>
    public float CameraZoom { get; set; } = 1f;

    public void ShowEvents(IReadOnlyList<SimulationEvent> events)
    {
        foreach (var item in events)
        {
            if (item is EncounterChanged)
            {
                _beams.Clear();
                _impacts.Clear();
            }
            // Ship-attached Lance presets own the player and all configured enemy shot visuals.
            // The tactical beam remains a fallback for enemy classes without art.
            if (item is WeaponFired shot && shot.Owner != WeaponOwner.Player && !IsShipPresetShot(shot))
                _beams.Add(new Beam(ViewSettings.Project(shot.Origin),
                    ViewSettings.Project(shot.VisualFadeStart ?? shot.End),
                    ViewSettings.Project(shot.VisualEnd ?? shot.End), shot.Owner));
            if (item is TargetHit hit)
                _impacts.Add(new Impact(ViewSettings.Project(hit.Position)));
            if (item is EnemyDestroyed destroyed)
                _impacts.Add(new Impact(ViewSettings.Project(destroyed.Position)));
            if (item is ShipCollision collision)
                _impacts.Add(new Impact(ViewSettings.Project(collision.Position)));
            if (item is ShieldHit shield)
                _impacts.Add(new Impact(ViewSettings.Project(shield.Position)));
        }
    }

    private bool IsShipPresetShot(WeaponFired shot) => World.VisibleEnemies
        .Where(enemy => EnemyShipView.HasVisual(enemy.ShipClass) && !enemy.IsDestroyed)
        .Any(enemy => NVector3.DistanceSquared(enemy.Ship.Position, shot.Origin) <= 4f);

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
        if (!HidePlayerGuidance) DrawAimAndDrift();
        DrawEnemy();
        if (!HidePlayerGuidance && !IsFreeCamera) DrawEnemyIndicator();
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
        if (!HidePlayerGuidance) DrawNearestIndicator();
        if (HyperspaceEnemyPoint is { } lastKnown)
        {
            Color marker = new Color("94a0a3");
            float markerHalfSize = ScreenPixelsToWorld(9);
            DrawLine(lastKnown + new Vector2(-markerHalfSize, -markerHalfSize), lastKnown + new Vector2(markerHalfSize, markerHalfSize),
                marker, ScreenPixelsToWorld(1.8f), true);
            DrawLine(lastKnown + new Vector2(-markerHalfSize, markerHalfSize), lastKnown + new Vector2(markerHalfSize, -markerHalfSize),
                marker, ScreenPixelsToWorld(1.8f), true);
            DrawString(ThemeDB.FallbackFont, lastKnown + new Vector2(ScreenPixelsToWorld(14), ScreenPixelsToWorld(-10)), "LAST KNOWN POSITION",
                fontSize: ScreenFontSize(12), modulate: marker);
        }
        if (HyperspaceEntryPoint is { } entry)
        {
            if (HyperspaceEnemyPoint is { } knownPoint)
            {
                DrawLine(entry, knownPoint, ViewSettings.Alpha(ViewSettings.Amber, .72f), ScreenPixelsToWorld(1.4f), true);
                float distanceMeters = entry.DistanceTo(knownPoint) / ViewSettings.PixelsPerMeter;
                Vector2 midpoint = entry.Lerp(knownPoint, .5f);
                DrawString(ThemeDB.FallbackFont, midpoint + new Vector2(ScreenPixelsToWorld(8), ScreenPixelsToWorld(-7)), $"{distanceMeters:0} m",
                    fontSize: ScreenFontSize(12), modulate: ViewSettings.Amber);
            }
            float entryHalfSize = ScreenPixelsToWorld(15);
            DrawLine(entry + new Vector2(-entryHalfSize, -entryHalfSize), entry + new Vector2(entryHalfSize, entryHalfSize),
                new Color("ff6577"), ScreenPixelsToWorld(2), true);
            DrawLine(entry + new Vector2(-entryHalfSize, entryHalfSize), entry + new Vector2(entryHalfSize, -entryHalfSize),
                new Color("ff6577"), ScreenPixelsToWorld(2), true);
            DrawArc(entry, ScreenPixelsToWorld(23), 0, MathF.Tau, 32, ViewSettings.Alpha(new Color("ff6577"), .7f),
                ScreenPixelsToWorld(1.2f), true);
        }
        foreach (var beam in _beams)
        {
            float alpha = 1f - beam.Age / ViewSettings.BeamDurationSeconds;
            Color beamColor = beam.Owner == WeaponOwner.Enemy ? new Color("ff6577") : ViewSettings.Cyan;
            DrawBeamSegment(beam.Origin, beam.FadeStart, beamColor, alpha);
            DrawFadingBeamTail(beam.FadeStart, beam.VisualEnd, beamColor, alpha);
        }
        foreach (var impact in _impacts)
        {
            float progress = impact.Age / ViewSettings.ImpactDurationSeconds;
            DrawArc(impact.Position, 14 + 35 * progress, 0, MathF.Tau, 40,
                ViewSettings.Alpha(ViewSettings.Amber, 1f - progress), 2, true);
        }
    }

    private void DrawBeamSegment(Vector2 from, Vector2 to, Color color, float alpha)
    {
        DrawLine(from, to, ViewSettings.Alpha(color, alpha * 0.12f), 14, true);
        DrawLine(from, to, ViewSettings.Alpha(color, alpha * 0.55f), 5, true);
        DrawLine(from, to, new Color(0.9f, 1f, 1f, alpha), 1.8f, true);
    }

    private void DrawFadingBeamTail(Vector2 from, Vector2 to, Color color, float alpha)
    {
        const int segments = 18;
        for (int index = 0; index < segments; index++)
        {
            float start = (float)index / segments;
            float end = (float)(index + 1) / segments;
            float intensity = (1f - start) * (1f - start);
            Vector2 a = from.Lerp(to, start);
            Vector2 b = from.Lerp(to, end);
            DrawLine(a, b, ViewSettings.Alpha(color, alpha * intensity * 0.08f), 12f * intensity, true);
            DrawLine(a, b, ViewSettings.Alpha(color, alpha * intensity * 0.40f), 3.5f * intensity, true);
            DrawLine(a, b, ViewSettings.Alpha(new Color(0.9f, 1f, 1f), alpha * intensity), MathF.Max(.3f, intensity), true);
        }
    }

    public void ResetVisuals()
    {
        _beams.Clear();
        _impacts.Clear();
    }

    /// <summary>Returns the visible enemy under a bridge-map click in world pixel coordinates.</summary>
    public bool TrySelectEnemy(Vector2 worldPosition, out int enemyId)
    {
        enemyId = 0;
        if (World is null) return false;
        float radius = 28f / MathF.Max(0.001f, CameraZoom);
        var enemy = World.VisibleEnemies
            .Select(candidate => new { Enemy = candidate, DistanceSquared = (ViewSettings.Project(candidate.Ship.Position) - worldPosition).LengthSquared() })
            .Where(candidate => candidate.DistanceSquared <= radius * radius)
            .OrderBy(candidate => candidate.DistanceSquared)
            .FirstOrDefault();
        if (enemy is null) return false;
        enemyId = enemy.Enemy.EnemyId;
        return true;
    }

    private void DrawEnemy()
    {
        foreach (var enemy in World.VisibleEnemies)
            DrawEnemy(enemy);
    }

    private void DrawEnemy(SpaceSim.Core.Combat.EnemyShipState enemy)
    {
        Vector2 center = ViewSettings.Project(enemy.Ship.Position);
        Vector2 forward = new(enemy.Ship.Forward.X, enemy.Ship.Forward.Z);
        Vector2 right = new(-forward.Y, forward.X);
        Color enemyColor = new("ff6577");
        // Configured enemy classes have their own ship-attached sprites and effects. The simple
        // tactical hull remains the fallback for classes without visual assets.
        if (!EnemyShipView.HasVisual(enemy.ShipClass))
        {
            Vector2[] hull =
            {
                center + forward * 23,
                center - forward * 14 + right * 14,
                center - forward * 8,
                center - forward * 14 - right * 14,
                center + forward * 23
            };
            DrawColoredPolygon(hull[..^1], new Color("3c1722"));
            DrawPolyline(hull, enemyColor, 1.8f, true);
            DrawLine(center, center + forward * 18, enemyColor, 2, true);
        }
        if (SelectedEnemyId == enemy.EnemyId)
            DrawArc(center, 30, 0, MathF.Tau, 32, ViewSettings.Alpha(ViewSettings.Cyan, .8f), 1.5f, true);
        Vector2 nameSize = ThemeDB.FallbackFont.GetStringSize(enemy.Name, fontSize: 12);
        DrawString(ThemeDB.FallbackFont, center + new Vector2(-nameSize.X / 2f, -30), enemy.Name,
            fontSize: 12, modulate: enemyColor);
        var ai = World.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
        if (!EnemyShipView.HasVisual(enemy.ShipClass) && ai?.LastCommand.MainThrust == true)
            DrawLine(center - forward * 15, center - forward * 32, new Color("ffb15c"), 4, true);
        DrawString(ThemeDB.FallbackFont, center + right * 25 + new Vector2(4, 4),
            $"{enemy.ShipClass.ToString().ToUpperInvariant()}  {ai?.CurrentState.ToString().ToUpperInvariant()}  LANCE {enemy.Lance.ChargeFraction * 100:0}%",
            fontSize: 12, modulate: enemyColor);
    }

    private void DrawEnemyIndicator()
    {
        foreach (var enemy in World.VisibleEnemies)
            DrawEnemyIndicator(enemy);
    }

    private void DrawEnemyIndicator(SpaceSim.Core.Combat.EnemyShipState enemy)
    {
        Vector2 delta = ViewSettings.Project(enemy.Ship.Position) - ShipPosition;
        Vector2 half = VisibleWorldHalfExtent(new Vector2(80, 170));
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
        DrawBowOrientationLine();
        Vector2 velocity = ViewSettings.Project(World.Ship.Velocity);
        if (velocity.Length() < 0.5f) return;
        Vector2 direction = velocity.Normalized();
        Vector2 tip = ShipPosition + direction * Mathf.Clamp(velocity.Length() * 1.5f, 45, 130);
        DrawLine(ShipPosition + direction * 32, tip, ViewSettings.Alpha(ViewSettings.Cyan, 0.45f), 1, true);
        DrawPolyline(new[] { tip - direction.Rotated(0.5f) * 8, tip, tip - direction.Rotated(-0.5f) * 8 },
            ViewSettings.Cyan, 1, true);
    }

    /// <summary>Draws the bow axis through the current visible camera rectangle.</summary>
    private void DrawBowOrientationLine()
    {
        if (ShipForward.LengthSquared() < 0.000001f) return;
        Vector2 direction = ShipForward.Normalized();
        if (!TryGetVisibleLineSegment(ShipPosition, direction, out float first, out float last)) return;
        // The course guide always starts at the bow and never draws behind the ship,
        // including while the tactical camera is detached.
        first = MathF.Max(0f, first);
        if (last <= first) return;

        // Keep dash, gap and thickness stable in screen pixels at every zoom level.
        float zoom = MathF.Max(0.001f, CameraZoom);
        float dashLength = 11f / zoom;
        float gapLength = 8f / zoom;
        float lineWidth = 1f / zoom;
        for (float start = first; start < last; start += dashLength + gapLength)
        {
            float end = MathF.Min(last, start + dashLength);
            DrawLine(ShipPosition + direction * start, ShipPosition + direction * end,
                ViewSettings.Alpha(ViewSettings.Cyan, 0.30f), lineWidth, true);
        }
    }

    private bool TryGetVisibleLineSegment(Vector2 point, Vector2 direction, out float first, out float last)
    {
        Rect2 visible = VisibleWorldRect();
        var intersections = new List<float>(4);
        if (MathF.Abs(direction.X) > 0.000001f)
        {
            AddVerticalIntersection(visible.Position.X);
            AddVerticalIntersection(visible.End.X);
        }
        if (MathF.Abs(direction.Y) > 0.000001f)
        {
            AddHorizontalIntersection(visible.Position.Y);
            AddHorizontalIntersection(visible.End.Y);
        }
        if (intersections.Count < 2)
        {
            first = last = 0f;
            return false;
        }
        first = intersections.Min();
        last = intersections.Max();
        return last - first > 0.0001f;

        void AddVerticalIntersection(float x)
        {
            float t = (x - point.X) / direction.X;
            float y = point.Y + direction.Y * t;
            if (y >= visible.Position.Y - 0.001f && y <= visible.End.Y + 0.001f) intersections.Add(t);
        }
        void AddHorizontalIntersection(float y)
        {
            float t = (y - point.Y) / direction.Y;
            float x = point.X + direction.X * t;
            if (x >= visible.Position.X - 0.001f && x <= visible.End.X + 0.001f) intersections.Add(t);
        }
    }

    private void DrawNearestIndicator()
    {
        var nearest = World.Targets.MinBy(t => NVector3.DistanceSquared(t.Position, World.Ship.Position));
        if (nearest is null) return;
        Vector2 delta = ViewSettings.Project(nearest.Position) - ShipPosition;
        Vector2 half = VisibleWorldHalfExtent(new Vector2(65, 165));
        if (MathF.Abs(delta.X) <= half.X && MathF.Abs(delta.Y) <= half.Y) return;
        float scale = MathF.Min(half.X / MathF.Max(1, MathF.Abs(delta.X)), half.Y / MathF.Max(1, MathF.Abs(delta.Y)));
        Vector2 tip = ShipPosition + delta * scale;
        Vector2 direction = delta.Normalized();
        DrawPolyline(new[] { tip - direction.Rotated(0.5f) * 12, tip, tip - direction.Rotated(-0.5f) * 12 },
            ViewSettings.Amber, 2, true);
        DrawString(ThemeDB.FallbackFont, tip + new Vector2(12, 4), $"T{nearest.Id:00}", fontSize: 12,
            modulate: ViewSettings.Amber);
    }

    private Vector2 VisibleWorldHalfExtent(Vector2 screenMargin) =>
        (GetViewportRect().Size / 2f - screenMargin) / MathF.Max(0.001f, CameraZoom);

    private float ScreenPixelsToWorld(float pixels) => pixels / MathF.Max(0.001f, CameraZoom);

    private int ScreenFontSize(int pixels) => Math.Max(1, Mathf.RoundToInt(ScreenPixelsToWorld(pixels)));

    private Rect2 VisibleWorldRect()
    {
        Vector2 half = VisibleWorldHalfExtent(Vector2.Zero);
        return new Rect2(CameraPosition - half, half * 2f);
    }
}
