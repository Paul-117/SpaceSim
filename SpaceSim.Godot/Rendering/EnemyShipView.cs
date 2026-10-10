using System.Text.Json;
using Godot;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;

namespace SpaceSim.GodotClient.Rendering;

/// <summary>
/// Presentation for an enemy class. It receives only snapshots and normal
/// <see cref="ShipCommand"/> values; gameplay remains entirely in SpaceSim.Core.
/// </summary>
public partial class EnemyShipView : Node2D
{
    private const float ShipSpriteScale = 0.026f;

    public EnemyShipClass ShipClass { get; init; }

    private AnimatedSprite2D _booster = null!;
    private AnimatedSprite2D _lanceShot = null!;
    private ShipAnimationPreset _boosterPreset = null!;

    public override void _Ready()
    {
        ShipVisualConfig visual = GetVisualConfig(ShipClass);
        AddChild(new Sprite2D
        {
            Texture = GD.Load<Texture2D>(visual.SpritePath),
            Scale = Vector2.One * ShipSpriteScale,
            Rotation = -Mathf.Pi / 2f,
            ZIndex = -1
        });

        _booster = CreateAnimation(LoadPreset(visual.BoosterPresetPath), loop: true, out _boosterPreset);
        _booster.Visible = false;
        _lanceShot = CreateAnimation(LoadPreset(visual.LancePresetPath), loop: false, out _);
        _lanceShot.Visible = false;
        _lanceShot.AnimationFinished += () => _lanceShot.Visible = false;
        AddChild(_booster);
        AddChild(_lanceShot);
    }

    public void Refresh(ShipCommand command, float effectiveMainThrust)
    {
        float intensity = command.MainThrust ? Math.Clamp(effectiveMainThrust, 0f, 1f) : 0f;
        _booster.Visible = intensity > 0.01f;
        if (!_booster.Visible) return;

        float sizeFactor = 0.35f + intensity * 0.65f;
        _booster.Scale = new Vector2(_boosterPreset.ScaleXValue * sizeFactor,
            _boosterPreset.ScaleYValue * sizeFactor);
        _booster.Modulate = new Color(1f, 1f, 1f, 0.30f + intensity * 0.70f);
        _booster.SpeedScale = 0.55f + intensity * 0.90f;
        if (!_booster.IsPlaying()) _booster.Play();
    }

    public void PlayLanceShot()
    {
        _lanceShot.Frame = 0;
        _lanceShot.Visible = true;
        _lanceShot.Play();
    }

    public static bool HasVisual(EnemyShipClass shipClass) => shipClass is
        EnemyShipClass.Interceptor or EnemyShipClass.Corvette or EnemyShipClass.Frigate;

    private static ShipVisualConfig GetVisualConfig(EnemyShipClass shipClass) => shipClass switch
    {
        EnemyShipClass.Interceptor => new ShipVisualConfig(
            "res://Assets/Sprites/Transporter/Frachter2.png",
            "res://Assets/Sprites/Transporter/AnimationPresets/Frachter Booster.json",
            "res://Assets/Sprites/Transporter/AnimationPresets/Frachter Lance.json"),
        EnemyShipClass.Corvette => new ShipVisualConfig(
            "res://Assets/Sprites/Corvette/Corvette.png",
            "res://Assets/Sprites/Corvette/AnimationPresets/Corvette Booster.json",
            "res://Assets/Sprites/Corvette/AnimationPresets/Corvette Lance.json"),
        EnemyShipClass.Frigate => new ShipVisualConfig(
            "res://Assets/Sprites/Frigatte/Frigatte.png",
            "res://Assets/Sprites/Frigatte/AnimationPresets/Frigatte Booster.json",
            "res://Assets/Sprites/Frigatte/AnimationPresets/Frigatte Lance.json"),
        _ => throw new ArgumentOutOfRangeException(nameof(shipClass), shipClass, "No enemy visual is configured.")
    };

    private static AnimatedSprite2D CreateAnimation(ShipAnimationPreset preset, bool loop,
        out ShipAnimationPreset createdPreset)
    {
        createdPreset = preset;
        Texture2D texture = GD.Load<Texture2D>(preset.AssetPath);
        SpriteFrames frames = new();
        frames.RemoveAnimation("default");
        frames.AddAnimation("effect");
        frames.SetAnimationLoopMode("effect", loop ? SpriteFrames.LoopMode.Linear : SpriteFrames.LoopMode.None);
        frames.SetAnimationSpeed("effect", preset.FramesPerSecond);

        Vector2 textureSize = texture.GetSize();
        float frameWidth = textureSize.X / preset.Columns;
        float frameHeight = textureSize.Y / preset.Rows;
        for (int row = 0; row < preset.Rows; row++)
        for (int column = 0; column < preset.Columns; column++)
            frames.AddFrame("effect", new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(column * frameWidth, row * frameHeight, frameWidth, frameHeight),
                FilterClip = true
            });

        return new AnimatedSprite2D
        {
            SpriteFrames = frames,
            Animation = "effect",
            Position = new Vector2(preset.OffsetX, preset.OffsetY),
            Scale = new Vector2(preset.ScaleXValue, preset.ScaleYValue),
            Rotation = -Mathf.Pi / 2f + Mathf.DegToRad(preset.RotationDegrees),
            ZIndex = preset.DrawInFront ? 1 : -2
        };
    }

    private static ShipAnimationPreset LoadPreset(string presetPath)
    {
        string source = File.ReadAllText(ProjectSettings.GlobalizePath(presetPath));
        return JsonSerializer.Deserialize<ShipAnimationPreset>(source)
               ?? throw new InvalidOperationException($"Invalid animation preset: {presetPath}");
    }

    private sealed class ShipAnimationPreset
    {
        public string AssetPath { get; init; } = string.Empty;
        public int Columns { get; init; } = 1;
        public int Rows { get; init; } = 1;
        public float FramesPerSecond { get; init; } = 12f;
        public float? Scale { get; init; }
        public float? ScaleX { get; init; }
        public float? ScaleY { get; init; }
        public float OffsetX { get; init; }
        public float OffsetY { get; init; }
        public float RotationDegrees { get; init; }
        public bool DrawInFront { get; init; }
        public float ScaleXValue => ScaleX ?? Scale ?? 1f;
        public float ScaleYValue => ScaleY ?? Scale ?? 1f;
    }

    private sealed record ShipVisualConfig(string SpritePath, string BoosterPresetPath, string LancePresetPath);
}
