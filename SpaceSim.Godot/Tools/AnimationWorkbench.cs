using System.Text.Json;
using Godot;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.Tools;

/// <summary>Development-only sprite animation editor. It never changes simulation state.</summary>
public partial class AnimationWorkbench : Node2D
{
    private const string AssetsRoot = "res://Assets/Sprites";
    private const string PresetsRoot = "res://Assets/Sprites/Nomad/AnimationPresets";
    private const string PreviewAnimationName = "preview";
    private const string PlayerShipSpritePath = "res://Assets/Sprites/Nomad/Nomad-transparent.png";
    private const float ShipPreviewTargetPixels = 46f;

    private readonly List<string> _assetPaths = [];
    private readonly List<string> _shipPaths = [];
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private Node2D _previewAnchor = null!;
    private Sprite2D _shipPreview = null!;
    private AnimatedSprite2D _animationPreview = null!;
    private OptionButton _assetPicker = null!;
    private OptionButton _shipPicker = null!;
    private Label _assetFolderLabel = null!;
    private Label _shipFolderLabel = null!;
    private FileDialog _folderPicker = null!;
    private FileDialog _shipFolderPicker = null!;
    private SpinBox _columns = null!;
    private SpinBox _rows = null!;
    private SpinBox _framesPerSecond = null!;
    private SpinBox _scaleX = null!;
    private SpinBox _scaleY = null!;
    private SpinBox _offsetX = null!;
    private SpinBox _offsetY = null!;
    private SpinBox _animationRotation = null!;
    private SpinBox _viewRotation = null!;
    private CheckButton _drawInFront = null!;
    private LineEdit _presetName = null!;
    private Label _summary = null!;
    private Label _saveStatus = null!;
    private Label _viewStatus = null!;
    private Panel _sidePanel = null!;
    private ScrollContainer _sideScroll = null!;
    private Panel _viewPanel = null!;
    private Vector2 _viewPan;
    private float _viewZoom = 1f;
    private readonly string _workspaceRoot = Path.GetFullPath(Path.Combine(ProjectSettings.GlobalizePath("res://"), ".."));
    private string _assetFolder = AssetsRoot;
    private string _shipFolder = string.Empty;

    public override void _Ready()
    {
        Name = "AnimationWorkbench";
        CreatePreview();
        CreateInterface();
        _shipFolder = ProjectSettings.GlobalizePath(AssetsRoot);
        DiscoverShips(_shipFolder);
        DiscoverAssets(_assetFolder);
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        Vector2 viewport = GetViewportRect().Size;
        UpdateSidePanelLayout(viewport);
        UpdateViewPan((float)delta);
        _previewAnchor.Position = new Vector2(MathF.Max(940f, viewport.X * .66f), viewport.Y * .52f) + _viewPan;
        _previewAnchor.Scale = Vector2.One * _viewZoom;
        _previewAnchor.Rotation = Mathf.DegToRad((float)_viewRotation.Value);
        _viewStatus.Text = $"CURRENT ZOOM\n{_viewZoom:0.000}×";
        QueueRedraw();
    }

    public override void _UnhandledInput(InputEvent input)
    {
        if (input is not InputEventMouseButton { Pressed: true } mouse || mouse.Position.X <= 450f) return;
        float direction = mouse.ButtonIndex switch
        {
            MouseButton.WheelUp => 1f,
            MouseButton.WheelDown => -1f,
            _ => 0f
        };
        if (Mathf.IsZeroApprox(direction)) return;

        float candidate = _viewZoom * Mathf.Pow(TacticalCameraSettings.MouseWheelZoomFactor, direction);
        if (float.IsFinite(candidate) && candidate > 0f) _viewZoom = candidate;
        GetViewport().SetInputAsHandled();
    }

    public override void _Draw()
    {
        Vector2 viewport = GetViewportRect().Size;
        DrawRect(new Rect2(Vector2.Zero, viewport), new Color("07101a"));

        float previewStartX = _sidePanel is null ? 800f : _sidePanel.Position.X + _sidePanel.Size.X + 24f;
        const float gridStep = 50f;
        Color grid = new Color("13324a");
        for (float x = previewStartX; x < viewport.X; x += gridStep)
            DrawLine(new Vector2(x, 0), new Vector2(x, viewport.Y), grid, 1f, true);
        for (float y = 0; y < viewport.Y; y += gridStep)
            DrawLine(new Vector2(previewStartX, y), new Vector2(viewport.X, y), grid, 1f, true);

        Vector2 anchor = _previewAnchor?.Position ?? Vector2.Zero;
        DrawLine(anchor + new Vector2(-260, 0), anchor + new Vector2(260, 0), new Color("27546e"), 1.5f, true);
        DrawLine(anchor + new Vector2(0, -260), anchor + new Vector2(0, 260), new Color("27546e"), 1.5f, true);
        DrawArc(anchor, 38f, 0, MathF.Tau, 48, new Color("386d89"), 1.2f, true);
        DrawString(ThemeDB.FallbackFont, anchor + new Vector2(16, -18), "SHIP ANCHOR", HorizontalAlignment.Left, -1,
            13, new Color("7fa6bd"));
        DrawDistanceScale(viewport);
    }

    private void CreatePreview()
    {
        _previewAnchor = new Node2D { ZIndex = 1 };
        AddChild(_previewAnchor);

        _shipPreview = new Sprite2D
        {
            Rotation = -Mathf.Pi / 2f,
            ZIndex = 1
        };
        _previewAnchor.AddChild(_shipPreview);
        SetShipPreviewTexture(GD.Load<Texture2D>(PlayerShipSpritePath));

        _animationPreview = new AnimatedSprite2D
        {
            Position = new Vector2(0, 22),
            Rotation = -Mathf.Pi / 2f,
            ZIndex = -1
        };
        _previewAnchor.AddChild(_animationPreview);
    }

    private void CreateInterface()
    {
        CanvasLayer overlay = new();
        AddChild(overlay);

        _sidePanel = new Panel
        {
            Position = new Vector2(22, 22),
            Size = new Vector2(760, 980)
        };
        _sidePanel.AddThemeStyleboxOverride("panel", PanelStyle());
        overlay.AddChild(_sidePanel);

        _sideScroll = new ScrollContainer
        {
            Position = new Vector2(12, 12),
            Size = new Vector2(736, 956),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled
        };
        _sidePanel.AddChild(_sideScroll);

        VBoxContainer content = new()
        {
            CustomMinimumSize = new Vector2(712, 720),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        content.AddThemeConstantOverride("separation", 10);
        _sideScroll.AddChild(content);

        Label heading = TextLabel("ANIMATION WORKBENCH", 24, new Color("6ee7ef"));
        content.AddChild(heading);
        content.AddChild(TextLabel("Schiff und Sprite-Sheet aus dem Projekt laden und die Animation am Schiffsanker ausrichten.", 13, new Color("9db4c3")));

        _shipPicker = new OptionButton { TooltipText = "Schiffsvorschau aus dem gewÃ¤hlten Schiff-Ordner" };
        _shipPicker.ItemSelected += _ => UpdateShipPreview();
        _shipPicker.GetPopup().MaxSize = new Vector2I(360, 420);
        content.AddChild(Labeled("SCHIFF", _shipPicker));

        Button selectShipFolder = new() { Text = "SCHIFF-ORDNER AUSWÃ„HLEN" };
        selectShipFolder.Pressed += () => _shipFolderPicker.PopupCentered(new Vector2I(820, 620));
        content.AddChild(selectShipFolder);
        _shipFolderLabel = TextLabel(string.Empty, 12, new Color("9db4c3"));
        _shipFolderLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _shipFolderLabel.CustomMinimumSize = new Vector2(0, 35);
        content.AddChild(_shipFolderLabel);

        _assetPicker = new OptionButton { TooltipText = "PNG-Datei aus dem gewählten Projektordner" };
        _assetPicker.ItemSelected += _ => RebuildAnimation();
        _assetPicker.GetPopup().MaxSize = new Vector2I(360, 420);
        content.AddChild(Labeled("ASSET", _assetPicker));

        Button selectFolder = new() { Text = "ASSET-ORDNER AUSWÄHLEN" };
        selectFolder.Pressed += () => _folderPicker.PopupCentered(new Vector2I(820, 620));
        content.AddChild(selectFolder);
        _assetFolderLabel = TextLabel(_assetFolder, 12, new Color("9db4c3"));
        _assetFolderLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _assetFolderLabel.CustomMinimumSize = new Vector2(0, 35);
        content.AddChild(_assetFolderLabel);

        GridContainer animationSettings = new() { Columns = 3 };
        animationSettings.AddThemeConstantOverride("separation", 14);
        content.AddChild(animationSettings);

        _columns = Number(8, 1, 32, 1, RebuildAnimation);
        _rows = Number(1, 1, 32, 1, RebuildAnimation);
        _framesPerSecond = Number(12, 1, 60, 1, RebuildAnimation);
        animationSettings.AddChild(Labeled("SPALTEN", _columns));
        animationSettings.AddChild(Labeled("ZEILEN", _rows));
        animationSettings.AddChild(Labeled("FPS", _framesPerSecond));

        _scaleX = Number(.05, .001, 10, .001, UpdatePreviewTransform);
        _scaleY = Number(.05, .001, 10, .001, UpdatePreviewTransform);
        animationSettings.AddChild(Labeled("SCALE X", _scaleX));
        animationSettings.AddChild(Labeled("SCALE Y", _scaleY));
        animationSettings.AddChild(new Control());

        _offsetX = Number(0, -1_000_000, 1_000_000, 1, UpdatePreviewTransform);
        _offsetY = Number(22, -1_000_000, 1_000_000, 1, UpdatePreviewTransform);
        animationSettings.AddChild(Labeled("OFFSET X", _offsetX));
        animationSettings.AddChild(Labeled("OFFSET Y", _offsetY));
        animationSettings.AddChild(new Control());

        _animationRotation = Number(0, -180, 180, 1, UpdatePreviewTransform);
        animationSettings.AddChild(Labeled("ANIMATION ROTATION", _animationRotation));
        animationSettings.AddChild(new Control());
        animationSettings.AddChild(new Control());

        _drawInFront = new CheckButton { Text = "VOR DEM SCHIFF ZEICHNEN" };
        _drawInFront.Toggled += _ => UpdatePreviewTransform();
        content.AddChild(_drawInFront);

        _summary = TextLabel(string.Empty, 13, new Color("a9dce2"));
        _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        _summary.CustomMinimumSize = new Vector2(0, 74);
        content.AddChild(_summary);

        HSeparator separator = new();
        content.AddChild(separator);
        content.AddChild(TextLabel("PRESET SPEICHERN", 16, new Color("ffbe73")));
        _presetName = new LineEdit { PlaceholderText = "z. B. MainBooster" };
        content.AddChild(_presetName);
        Button save = new() { Text = "PRESET IN ASSETS SPEICHERN" };
        save.Pressed += SavePreset;
        content.AddChild(save);
        _saveStatus = TextLabel("", 12, new Color("9db4c3"));
        _saveStatus.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        content.AddChild(_saveStatus);

        Label help = TextLabel("Die Vorschau verwendet den gleichen Anker wie die Schiffsdarstellung.\nÄnderungen gelten sofort nur in dieser Werkbank; ein gespeichertes Preset kann später direkt von einer Schiffsanimation verwendet werden.", 12, new Color("728ca1"));
        help.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        help.CustomMinimumSize = new Vector2(0, 64);
        content.AddChild(help);

        CreateViewControls(overlay);

        _folderPicker = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "Asset-Ordner auswählen",
            CurrentDir = _workspaceRoot
        };
        _folderPicker.DirSelected += SelectAssetFolder;
        overlay.AddChild(_folderPicker);

        _shipFolderPicker = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = FileDialog.FileModeEnum.OpenDir,
            Title = "Schiff-Ordner auswÃ¤hlen",
            CurrentDir = _workspaceRoot
        };
        _shipFolderPicker.DirSelected += SelectShipFolder;
        overlay.AddChild(_shipFolderPicker);
    }

    private void CreateViewControls(CanvasLayer overlay)
    {
        _viewPanel = new Panel
        {
            Position = new Vector2(1600, 22),
            Size = new Vector2(270, 220)
        };
        _viewPanel.AddThemeStyleboxOverride("panel", PanelStyle());
        overlay.AddChild(_viewPanel);

        VBoxContainer content = new()
        {
            Position = new Vector2(16, 15),
            Size = new Vector2(238, 190)
        };
        content.AddThemeConstantOverride("separation", 8);
        _viewPanel.AddChild(content);
        content.AddChild(TextLabel("VIEW CONTROLS", 18, new Color("6ee7ef")));

        _viewStatus = TextLabel(string.Empty, 13, new Color("a9dce2"));
        content.AddChild(_viewStatus);

        _viewRotation = Number(0, -180, 180, 1, () => { });
        content.AddChild(Labeled("VIEW ROTATION", _viewRotation));

        Button resetView = new() { Text = "BASEGAME ZOOM  1,0×" };
        resetView.Pressed += ResetViewZoom;
        content.AddChild(resetView);
    }

    private void DiscoverAssets(string folder)
    {
        _assetFolder = folder;
        if (_assetFolderLabel is not null) _assetFolderLabel.Text = $"ORDNER: {_assetFolder}";
        _assetPaths.Clear();
        _assetPicker.Clear();
        CollectPngAssets(_assetFolder, _assetPaths);
        _assetPaths.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string assetPath in _assetPaths)
            _assetPicker.AddItem(assetPath.Replace(_assetFolder + "/", string.Empty));

        int booster = _assetPaths.FindIndex(path => path.EndsWith("BoosterFlames.png", StringComparison.OrdinalIgnoreCase));
        _assetPicker.Select(Math.Max(0, booster));
        if (_assetPaths.Count > 0) RebuildAnimation();
        else _saveStatus.Text = $"Keine PNG-Assets unter {_assetFolder} gefunden.";
    }

    private void SelectAssetFolder(string folder)
    {
        _folderPicker.CurrentDir = folder;
        DiscoverAssets(folder);
    }

    private void DiscoverShips(string folder)
    {
        _shipFolder = folder;
        _shipFolderLabel.Text = $"SCHIFF-ORDNER: {_shipFolder}";
        _shipPaths.Clear();
        _shipPicker.Clear();
        CollectPngAssets(_shipFolder, _shipPaths);
        _shipPaths.Sort(StringComparer.OrdinalIgnoreCase);
        foreach (string shipPath in _shipPaths)
            _shipPicker.AddItem(shipPath.Replace(_shipFolder + "/", string.Empty).Replace(_shipFolder + "\\", string.Empty));

        int nomad = _shipPaths.FindIndex(path => path.EndsWith("Nomad-transparent.png", StringComparison.OrdinalIgnoreCase));
        _shipPicker.Select(Math.Max(0, nomad));
        if (_shipPaths.Count > 0) UpdateShipPreview();
        else _saveStatus.Text = $"Keine PNG-Schiffe unter {_shipFolder} gefunden.";
    }

    private void SelectShipFolder(string folder)
    {
        _shipFolderPicker.CurrentDir = folder;
        DiscoverShips(folder);
        // In the planned ship folders, the animation sheets live next to their ship.
        DiscoverAssets(folder);
    }

    private void UpdateShipPreview()
    {
        if (_shipPaths.Count == 0) return;
        SetShipPreviewTexture(LoadTexture(SelectedShipPath()));
    }

    private void SetShipPreviewTexture(Texture2D texture)
    {
        _shipPreview.Texture = texture;
        Vector2 size = texture.GetSize();
        float largestDimension = MathF.Max(1f, MathF.Max(size.X, size.Y));
        _shipPreview.Scale = Vector2.One * ShipPreviewTargetPixels / largestDimension;
    }

    private void RebuildAnimation()
    {
        if (_assetPaths.Count == 0) return;
        string path = _assetPaths[_assetPicker.Selected];
        Texture2D texture = LoadTexture(path);
        int columns = (int)_columns.Value;
        int rows = (int)_rows.Value;
        Vector2 textureSize = texture.GetSize();
        float frameWidth = textureSize.X / columns;
        float frameHeight = textureSize.Y / rows;

        SpriteFrames frames = new();
        frames.RemoveAnimation("default");
        frames.AddAnimation(PreviewAnimationName);
        frames.SetAnimationLoopMode(PreviewAnimationName, SpriteFrames.LoopMode.Linear);
        frames.SetAnimationSpeed(PreviewAnimationName, (float)_framesPerSecond.Value);
        for (int row = 0; row < rows; row++)
        for (int column = 0; column < columns; column++)
        {
            frames.AddFrame(PreviewAnimationName, new AtlasTexture
            {
                Atlas = texture,
                Region = new Rect2(column * frameWidth, row * frameHeight, frameWidth, frameHeight),
                FilterClip = true
            });
        }

        _animationPreview.SpriteFrames = frames;
        _animationPreview.Play(PreviewAnimationName);
        UpdatePreviewTransform();
        _summary.Text = $"{path}\n{columns} × {rows} = {columns * rows} Frames | {textureSize.X:0} × {textureSize.Y:0} px";
    }

    private void UpdatePreviewTransform()
    {
        _animationPreview.Position = new Vector2((float)_offsetX.Value, (float)_offsetY.Value);
        _animationPreview.Scale = new Vector2((float)_scaleX.Value, (float)_scaleY.Value);
        _animationPreview.Rotation = -Mathf.Pi / 2f + Mathf.DegToRad((float)_animationRotation.Value);
        _animationPreview.ZIndex = _drawInFront.ButtonPressed ? 2 : -1;
        _summary.Text = $"{SelectedAssetPath()}\nOffset: {_offsetX.Value:0}, {_offsetY.Value:0} | Scale: {_scaleX.Value:0.000}, {_scaleY.Value:0.000} | " +
                        $"Rotation: {_animationRotation.Value:0}° | " +
                        (_drawInFront.ButtonPressed ? "vor dem Schiff" : "hinter dem Schiff");
    }

    private void UpdateViewPan(float delta)
    {
        if (GetViewport().GetMousePosition().X <= _sidePanel.Position.X + _sidePanel.Size.X + 16f) return;

        Vector2 direction = Vector2.Zero;
        if (Godot.Input.IsPhysicalKeyPressed(Key.W)) direction.Y -= 1f;
        if (Godot.Input.IsPhysicalKeyPressed(Key.S)) direction.Y += 1f;
        if (Godot.Input.IsPhysicalKeyPressed(Key.A)) direction.X -= 1f;
        if (Godot.Input.IsPhysicalKeyPressed(Key.D)) direction.X += 1f;
        if (direction != Vector2.Zero)
            _viewPan += direction.Normalized() * TacticalCameraSettings.FreePanPixelsPerSecond /
                MathF.Max(0.001f, _viewZoom) * delta;
    }

    private void ResetViewZoom() => _viewZoom = 1f;

    private void UpdateSidePanelLayout(Vector2 viewport)
    {
        float panelHeight = Math.Max(260f, viewport.Y - 44f);
        _sidePanel.Size = new Vector2(760f, panelHeight);
        _sideScroll.Size = new Vector2(736f, panelHeight - 24f);
        _viewPanel.Position = new Vector2(viewport.X - 292f, 22f);
    }

    private void DrawDistanceScale(Vector2 viewport)
    {
        float meters = NiceDistance(160f / (ViewSettings.PixelsPerMeter * _viewZoom));
        float length = meters * ViewSettings.PixelsPerMeter * _viewZoom;
        Vector2 start = new(viewport.X - 46f - length, viewport.Y - 52f);
        Vector2 end = new(viewport.X - 46f, viewport.Y - 52f);
        Color color = new("a9dce2");
        DrawLine(start, end, color, 2f, true);
        DrawLine(start + Vector2.Up * 8f, start + Vector2.Down * 8f, color, 2f, true);
        DrawLine(end + Vector2.Up * 8f, end + Vector2.Down * 8f, color, 2f, true);
        DrawString(ThemeDB.FallbackFont, start + new Vector2(0, -14), $"{meters:0} m", HorizontalAlignment.Left, -1,
            14, color);
    }

    private static float NiceDistance(float desiredMeters)
    {
        float exponent = MathF.Pow(10f, MathF.Floor(MathF.Log10(MathF.Max(0.001f, desiredMeters))));
        float normalized = desiredMeters / exponent;
        float multiplier = normalized <= 1f ? 1f : normalized <= 2f ? 2f : normalized <= 5f ? 5f : 10f;
        return multiplier * exponent;
    }

    private void SavePreset()
    {
        string name = SanitizeFileName(_presetName.Text);
        if (string.IsNullOrWhiteSpace(name))
        {
            _saveStatus.Text = "Bitte zuerst einen Preset-Namen eintragen.";
            return;
        }

        AnimationPreset preset = new(
            SelectedShipPath(), SelectedAssetPath(), (int)_columns.Value, (int)_rows.Value, (float)_framesPerSecond.Value,
            (float)_scaleX.Value, (float)_scaleY.Value, (float)_offsetX.Value, (float)_offsetY.Value, (float)_animationRotation.Value,
            _drawInFront.ButtonPressed);
        string selectedShip = SelectedShipPath();
        string directory = string.IsNullOrWhiteSpace(selectedShip)
            ? ProjectSettings.GlobalizePath(PresetsRoot)
            : Path.Combine(selectedShip.StartsWith("res://", StringComparison.OrdinalIgnoreCase)
                ? ProjectSettings.GlobalizePath(selectedShip.GetBaseDir())
                : Path.GetDirectoryName(selectedShip)!, "AnimationPresets");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, name + ".json");
        File.WriteAllText(path, JsonSerializer.Serialize(preset, _jsonOptions));
        _saveStatus.Text = $"Gespeichert: {path}";
    }

    private string SelectedAssetPath() => _assetPaths.Count == 0 ? string.Empty : _assetPaths[_assetPicker.Selected];
    private string SelectedShipPath() => _shipPaths.Count == 0 ? string.Empty : _shipPaths[_shipPicker.Selected];

    private static void CollectPngAssets(string directory, ICollection<string> result)
    {
        DirAccess? access = DirAccess.Open(directory);
        if (access is null) return;
        access.ListDirBegin();
        while (true)
        {
            string entry = access.GetNext();
            if (string.IsNullOrEmpty(entry)) break;
            if (entry is "." or "..") continue;
            string path = directory + "/" + entry;
            if (access.CurrentIsDir() && !IsIgnoredDirectory(entry)) CollectPngAssets(path, result);
            else if (entry.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) result.Add(path);
        }
        access.ListDirEnd();
    }

    private static Texture2D LoadTexture(string path)
    {
        if (path.StartsWith("res://", StringComparison.OrdinalIgnoreCase)) return GD.Load<Texture2D>(path);
        Image image = Image.LoadFromFile(path);
        return ImageTexture.CreateFromImage(image);
    }

    private static bool IsIgnoredDirectory(string entry) => entry is ".git" or ".godot" or ".dotnet" or ".nuget" or "bin" or "obj";

    private static SpinBox Number(double value, double min, double max, double step, Action changed)
    {
        SpinBox number = new() { Value = value, MinValue = min, MaxValue = max, Step = step, Size = new Vector2(166, 34) };
        number.ValueChanged += _ => changed();
        return number;
    }

    private static Control Labeled(string caption, Control control)
    {
        VBoxContainer box = new();
        box.AddThemeConstantOverride("separation", 4);
        box.AddChild(TextLabel(caption, 12, new Color("7fa6bd")));
        box.AddChild(control);
        return box;
    }

    private static Control Pair(string leftCaption, Control left, string rightCaption, Control right)
    {
        HBoxContainer row = new();
        row.AddThemeConstantOverride("separation", 16);
        row.AddChild(Labeled(leftCaption, left));
        row.AddChild(Labeled(rightCaption, right));
        return row;
    }

    private static Label TextLabel(string text, int size, Color color)
    {
        Label label = new() { Text = text, Modulate = color };
        label.AddThemeFontSizeOverride("font_size", size);
        return label;
    }

    private static StyleBoxFlat PanelStyle() => new()
    {
        BgColor = new Color("0c1b28e8"),
        BorderColor = new Color("2b627b"),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = 5,
        CornerRadiusTopRight = 5,
        CornerRadiusBottomLeft = 5,
        CornerRadiusBottomRight = 5
    };

    private static string SanitizeFileName(string value)
    {
        foreach (char invalid in Path.GetInvalidFileNameChars()) value = value.Replace(invalid, '_');
        return value.Trim();
    }

    private sealed record AnimationPreset(string ShipPath, string AssetPath, int Columns, int Rows, float FramesPerSecond,
        float ScaleX, float ScaleY, float OffsetX, float OffsetY, float RotationDegrees, bool DrawInFront);
}
