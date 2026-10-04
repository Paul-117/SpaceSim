using System.Text.Json;
using Godot;
using SpaceSim.Core.Ships;

namespace SpaceSim.GodotClient.Rendering;

public partial class ShipView : Node2D
{
    private const string PlayerShipSpritePath = "res://Assets/Sprites/Nomad/Nomad-transparent.png";
    private const string PresetsRoot = "res://Assets/Sprites/Nomad/AnimationPresets/";
    private const string MainBoosterPreset = PresetsRoot + "Main Booster.json";
    private const string LeftTopBoosterPreset = PresetsRoot + "Booster Links Oben.json";
    private const string LeftBottomBoosterPreset = PresetsRoot + "Booster Links Unten.json";
    private const string RightTopBoosterPreset = PresetsRoot + "Booster Rechts Oben.json";
    private const string RightBottomBoosterPreset = PresetsRoot + "Booster Rechts Unten.json";
    private const string LanceShotPreset = PresetsRoot + "Lance1.json";
    private const float PlayerShipSpriteScale = 0.026f;

    private float _time;
    private float _reentryTime = float.NegativeInfinity;
    private AnimatedSprite2D _mainBooster = null!;
    private AnimatedSprite2D _leftTopBooster = null!;
    private AnimatedSprite2D _leftBottomBooster = null!;
    private AnimatedSprite2D _rightTopBooster = null!;
    private AnimatedSprite2D _rightBottomBooster = null!;
    private AnimatedSprite2D _lanceShot = null!;
    private ShipAnimationPreset _mainBoosterPreset = null!;
    private ShipAnimationPreset _lanceShotPreset = null!;

    public override void _Ready()
    {
        Texture2D playerShipTexture = GD.Load<Texture2D>(PlayerShipSpritePath);
        AddChild(new Sprite2D
        {
            Texture = playerShipTexture,
            Scale = Vector2.One * PlayerShipSpriteScale,
            Rotation = -Mathf.Pi / 2f,
            ZIndex = -1
        });

        _mainBooster = CreateLoopingAnimation(MainBoosterPreset, out _mainBoosterPreset);
        _leftTopBooster = CreateLoopingAnimation(LeftTopBoosterPreset, out _);
        _leftBottomBooster = CreateLoopingAnimation(LeftBottomBoosterPreset, out _);
        _rightTopBooster = CreateLoopingAnimation(RightTopBoosterPreset, out _);
        _rightBottomBooster = CreateLoopingAnimation(RightBottomBoosterPreset, out _);
        _lanceShot = CreateOneShotAnimation(LanceShotPreset, out _lanceShotPreset);

        AddChild(_mainBooster);
        AddChild(_leftTopBooster);
        AddChild(_leftBottomBooster);
        AddChild(_rightTopBooster);
        AddChild(_rightBottomBooster);
        AddChild(_lanceShot);
    }

    public void StartReentry(float time)
    {
        _reentryTime = time;
        QueueRedraw();
    }

    public void Refresh(ShipCommand command, float time, float effectiveMainThrust)
    {
        _time = time;
        RefreshMainBooster(effectiveMainThrust);
        SetLoopVisible(_leftTopBooster, command.YawRight);
        SetLoopVisible(_rightBottomBooster, command.YawRight);
        SetLoopVisible(_rightTopBooster, command.YawLeft);
        SetLoopVisible(_leftBottomBooster, command.YawLeft);
        QueueRedraw();
    }

    public void PlayLanceShot(float lanceYawDegrees)
    {
        _lanceShot.Rotation = -Mathf.Pi / 2f + Mathf.DegToRad(_lanceShotPreset.RotationDegrees + lanceYawDegrees);
        _lanceShot.Frame = 0;
        _lanceShot.Visible = true;
        _lanceShot.Play();
    }

    public override void _Draw()
    {
        float reentryAge = _time - _reentryTime;
        if (reentryAge is < 0f or >= 3f) return;

        float progress = reentryAge / 3f;
        float radius = 90f * (1f - progress) + 12f;
        DrawArc(Vector2.Zero, radius, 0f, MathF.Tau, 48,
            ViewSettings.Alpha(ViewSettings.Cyan, 1f - progress), 2.5f, true);
        DrawCircle(Vector2.Zero, radius * .72f,
            ViewSettings.Alpha(ViewSettings.Cyan, .08f * (1f - progress)));
    }

    private AnimatedSprite2D CreateLoopingAnimation(string presetPath, out ShipAnimationPreset preset)
    {
        preset = LoadPreset(presetPath);
        AnimatedSprite2D animation = CreateAnimation(preset, loop: true);
        animation.Visible = false;
        return animation;
    }

    private AnimatedSprite2D CreateOneShotAnimation(string presetPath, out ShipAnimationPreset preset)
    {
        preset = LoadPreset(presetPath);
        AnimatedSprite2D animation = CreateAnimation(preset, loop: false);
        animation.Visible = false;
        animation.AnimationFinished += () => animation.Visible = false;
        return animation;
    }

    private static AnimatedSprite2D CreateAnimation(ShipAnimationPreset preset, bool loop)
    {
        Texture2D texture = LoadTexture(preset.AssetPath);
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
        {
            frames.AddFrame("effect", new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(column * frameWidth, row * frameHeight, frameWidth, frameHeight),
                FilterClip = true
            });
        }

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

    private void RefreshMainBooster(float effectiveMainThrust)
    {
        float intensity = Math.Clamp(effectiveMainThrust, 0f, 1f);
        _mainBooster.Visible = intensity > 0.01f;
        if (!_mainBooster.Visible) return;

        float sizeFactor = 0.35f + intensity * 0.65f;
        _mainBooster.Scale = new Vector2(_mainBoosterPreset.ScaleXValue * sizeFactor,
            _mainBoosterPreset.ScaleYValue * sizeFactor);
        _mainBooster.Modulate = new Color(1f, 1f, 1f, 0.3f + intensity * 0.7f);
        _mainBooster.SpeedScale = 0.55f + intensity * 0.90f;
        if (!_mainBooster.IsPlaying()) _mainBooster.Play();
    }

    private static void SetLoopVisible(AnimatedSprite2D animation, bool active)
    {
        animation.Visible = active;
        if (active && !animation.IsPlaying()) animation.Play();
    }

    private static ShipAnimationPreset LoadPreset(string presetPath)
    {
        string source = File.ReadAllText(ProjectSettings.GlobalizePath(presetPath));
        ShipAnimationPreset? preset = JsonSerializer.Deserialize<ShipAnimationPreset>(source);
        return preset ?? throw new InvalidOperationException($"Invalid animation preset: {presetPath}");
    }

    private static Texture2D LoadTexture(string path)
    {
        if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)) return GD.Load<Texture2D>(path);
        Image image = Image.LoadFromFile(path);
        return ImageTexture.CreateFromImage(image);
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
}
