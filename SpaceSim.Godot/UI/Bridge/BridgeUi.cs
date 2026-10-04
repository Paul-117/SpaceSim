using System.Text.Json;
using Godot;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;

namespace SpaceSim.GodotClient.UI;

public partial class BridgeUi : Control
{
    private static readonly Color Cyan = new("8bdcff");
    private static readonly Color LabelBlue = new("8eb7d2");
    private static readonly Color Value = new("e8f5ff");
    private static readonly Color Orange = new("ff7440");
    private static readonly Color Green = new("65eca8");
    private static readonly Color Yellow = new("ffcf5c");
    private static readonly Color Red = new("ff7777");
    private static readonly Color DamagedOrange = new("ff9c3d");

    public WorldState World { get; set; } = null!;
    public ShipCommand Command { get; set; }
    public bool IsBridgeActive { get; set; }
    public bool AutopilotActive { get; set; }
    public string AutopilotTargetName { get; set; } = "-";
    public event Action? WarpMapRequested;

    private readonly Dictionary<string, Label> _labels = new();
    private readonly Dictionary<string, ColorRect[]> _bars = new();
    private readonly Dictionary<string, Sprite2D> _assets = new();
    private Font _font = ThemeDB.FallbackFont;
    private TextureRect _referenceOverlay = null!;
    private Button _warpButton = null!;
    private ColorRect[] _warpSegments = [];
    private ColorRect[] _energySegments = [];
    private ColorRect[] _thrustSegments = [];
    private ColorRect[] _contactHullSegments = [];
    private ColorRect[] _shipHullSegments = [];
    private Label _encounter = null!;
    private Label _version = null!;
    private Label _warpState = null!;
    private Label _warpPercent = null!;
    private Label _contactName = null!;
    private Label _contactClass = null!;
    private Label _contactDistance = null!;
    private Label _contactVelocity = null!;
    private Label _contactReactor = null!;
    private Label _contactShields = null!;
    private Label _contactWeapons = null!;
    private Label? _contactHull;
    private Label _amariumState = null!;
    private Label _sensoriumState = null!;
    private Label _voltariumState = null!;
    private Label _systemsHull = null!;
    private Label _energyValue = null!;
    private Label _starboardState = null!;
    private Label _portState = null!;
    private Label _reverseState = null!;
    private Label _mainState = null!;
    private Label _velocity = null!;
    private Label _angularVelocity = null!;
    private Label _thrust = null!;
    private Label _boardComputerStatus = null!;
    private Label _boardComputerCommand = null!;
    private Sprite2D _boardComputerLed = null!;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        var bridgeFont = new FontFile();
        if (bridgeFont.LoadDynamicFont("res://Assets/Fonts/Rajdhani-Regular.ttf") == Error.Ok) _font = bridgeFont;

        _referenceOverlay = GetNode<TextureRect>("ReferenceOverlay");
        BuildLayout(GetNode<Control>("Layout"));
        BindDynamicLabels();
        CreateWarpButton();
        _referenceOverlay.Visible = OS.GetCmdlineUserArgs().Contains("--bridge-reference");
    }

    public override void _Process(double delta)
    {
        Visible = IsBridgeActive;
        if (!Visible || World is null) return;

        UpdateWarp();
        UpdateContact();
        UpdateSystems();
        UpdateEnergy();
        UpdateFlight();
        UpdateBoardComputer();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (@event is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F8 })
        {
            _referenceOverlay.Visible = !_referenceOverlay.Visible;
            GetViewport().SetInputAsHandled();
        }
    }

    private void BuildLayout(Control root)
    {
        string source = Godot.FileAccess.GetFileAsString("res://Assets/Bridge/bridge-layout.json");
        var items = JsonSerializer.Deserialize<List<LayoutItem>>(source,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? throw new InvalidOperationException("Bridge layout could not be read.");

        foreach (LayoutItem item in items)
        {
            switch (item.Type)
            {
                case "asset" when item.Id != "chassis":
                    _assets[item.Id] = AddFullAsset(root, item);
                    break;
                case "sprite":
                    AddSpriteAsset(root, item);
                    break;
                case "text":
                    _labels[item.Id] = AddText(root, item);
                    break;
                case "bar":
                    _bars[item.Id] = AddSegments(root, item);
                    break;
            }
        }
    }

    private void BindDynamicLabels()
    {
        _encounter = Label("encounter");
        _version = Label("version");
        _warpState = Label("warp-state");
        _warpPercent = Label("warp-percent");
        _contactName = Label("contact-name-value");
        _contactClass = Label("contact-class-value");
        _contactDistance = Label("contact-distance-value");
        _contactVelocity = Label("contact-velocity-value");
        _contactReactor = Label("contact-reactor-value");
        _contactShields = Label("contact-shields-value");
        _contactWeapons = Label("contact-weapons-value");
        _contactHull = OptionalLabel("contact-hull-value");
        _amariumState = Label("systems-amarium-value");
        _sensoriumState = Label("systems-sensorium-value");
        _voltariumState = Label("systems-voltarium-value");
        _systemsHull = Label("systems-hull-value");
        _energyValue = Label("energy-value");
        _starboardState = Label("energy-thruster-value");
        _portState = Label("port-thruster-state");
        _reverseState = Label("reverse-thruster-state");
        _mainState = Label("main-thruster-state");
        _velocity = Label("flight-velocity-value");
        _angularVelocity = Label("flight-angular-value");
        _thrust = Label("flight-thrust-value");
        _boardComputerStatus = Label("board-computer-status");
        _boardComputerCommand = Label("board-computer-command");
        _boardComputerLed = _assets["board-computer-led"];
        _warpSegments = Bar("warp-bar");
        _energySegments = Bar("energy-bar");
        _thrustSegments = Bar("thrust-bar");
        _shipHullSegments = Bar("ship-hull-bar");
        _contactHullSegments = Bar("contact-hull-bar");
    }

    private void CreateWarpButton()
    {
        _warpButton = new Button
        {
            Name = "WarpIconButton",
            Position = new Vector2(600, 30),
            Size = new Vector2(90, 90),
            Flat = true,
            TooltipText = "Warp Drive",
            MouseDefaultCursorShape = CursorShape.PointingHand,
            Modulate = new Color(1, 1, 1, 0)
        };
        _warpButton.Pressed += () => WarpMapRequested?.Invoke();
        AddChild(_warpButton);
    }

    private static ColorRect[] AddSegments(Control parent, LayoutItem item)
    {
        int count = Math.Max(1, item.Segments);
        float gap = Math.Max(0f, item.Gap);
        float segmentWidth = item.SegmentWidth > 0f
            ? item.SegmentWidth
            : (item.Width - gap * (count - 1)) / count;
        float segmentHeight = item.SegmentHeight > 0f ? item.SegmentHeight : item.Height;
        var segments = new ColorRect[count];
        for (int index = 0; index < count; index++)
        {
            segments[index] = new ColorRect
            {
                Name = item.Id + index,
                Position = new Vector2(item.X + index * (segmentWidth + gap), item.Y),
                Size = new Vector2(segmentWidth, segmentHeight),
                Color = new Color(item.Color ?? "ffffff"),
                MouseFilter = MouseFilterEnum.Ignore
            };
            parent.AddChild(segments[index]);
        }
        return segments;
    }

    private static void SetSegments(IEnumerable<ColorRect> segments, float fraction)
    {
        ColorRect[] array = segments.ToArray();
        int active = Mathf.RoundToInt(Mathf.Clamp(fraction, 0f, 1f) * array.Length);
        for (int index = 0; index < array.Length; index++) array[index].Visible = index < active;
    }

    private void UpdateContact()
    {
        EnemyShipState? enemy = World.VisibleEnemies.FirstOrDefault();
        if (enemy is null)
        {
            SetContactValues("-", "-", "-", "-", "-", "-", "-", "-");
            SetSegments(_contactHullSegments, 0f);
            return;
        }

        float distance = (enemy.Ship.Position - World.Ship.Position).Length();
        float relativeVelocity = (enemy.Ship.Velocity - World.Ship.Velocity).Length();
        bool shieldsOnline = enemy.Ship.Shield.CurrentShield > 0.001f && enemy.Ship.Power.ShieldsDraw > 0.001f;
        bool weaponReady = enemy.Lance.IsReady;
        SetContactValues(enemy.Name, enemy.ShipClass.ToString(), $"{distance:0} m", $"{relativeVelocity:0.0} m/s",
            $"{enemy.Ship.Reactor.OperatingLevelPercent:0}%", shieldsOnline ? "Online" : "Offline",
            weaponReady ? "Ready" : "Charging", $"{enemy.Ship.Hull.CurrentHull:0}/{enemy.Ship.Hull.MaximumHull:0}");
        SetLabelColor(_contactShields, shieldsOnline ? Green : Red);
        SetLabelColor(_contactWeapons, weaponReady ? Green : Yellow);
        SetSegments(_contactHullSegments, Ratio(enemy.Ship.Hull.CurrentHull, enemy.Ship.Hull.MaximumHull));
    }

    private void UpdateWarp()
    {
        float charge = World.WarpDrive.ChargeFraction;
        bool ready = World.WarpDrive.IsReady;
        _warpButton.Disabled = !ready;
        _warpState.Text = ready ? "READY" : "CHARGING";
        SetLabelColor(_warpState, ready ? Cyan : Orange);
        _warpPercent.Text = $"{charge * 100f:0}%";
        SetSegments(_warpSegments, charge);
    }

    private void UpdateSystems()
    {
        SetSystemStatus(_amariumState, Ratio(World.Ship.Power.WeaponsAvailable, World.Ship.Power.MaximumWeaponsDraw));
        SetSystemStatus(_voltariumState, Ratio(World.Ship.Reactor.AvailablePower, World.Ship.Reactor.MaximumOutputPower));
        SetSystemStatus(_sensoriumState, 1f);
        float hull = Ratio(World.Ship.Hull.CurrentHull, World.Ship.Hull.MaximumHull);
        _systemsHull.Text = $"Hull Integrity: {hull * 100f:0}%";
        SetSegments(_shipHullSegments, hull);
    }

    private void UpdateEnergy()
    {
        float energy = World.Ship.Power.PropulsionAvailable;
        float energyRatio = Ratio(energy, World.Ship.Power.MaximumPropulsionDraw);
        _energyValue.Text = $"{energy:0.0} PU";
        SetSegments(_energySegments, energyRatio);
        SetThrusterStatus(_starboardState, energyRatio);
        SetThrusterStatus(_portState, energyRatio);
        SetThrusterStatus(_reverseState, energyRatio);
        SetThrusterStatus(_mainState, energyRatio);
    }

    private void UpdateFlight()
    {
        float mainInput = Command.MainThrust ? Math.Clamp(Command.MainThrustIntensity, 0f, 1f) : 0f;
        float thrust = mainInput * World.Ship.Power.MainThrusterPowerFactor * World.Ship.Systems.PropulsionCondition;
        _velocity.Text = $"{World.Ship.Velocity.Length():0.0} m/s";
        _angularVelocity.Text = $"{World.Ship.AngularVelocity.Y:+0.000;-0.000;0.000} rad/s";
        _thrust.Text = $"{thrust * 100:0}%";
        SetSegments(_thrustSegments, thrust);
        _encounter.Text = $"/ {World.CurrentEncounter.Name}";
        _version.Text = $"FlightLab / Version {ProjectSettings.GetSetting("application/config/version", "2.1.2").AsString()}";
    }

    private void UpdateBoardComputer()
    {
        // The Board Computer has no independent power or damage state yet.
        // Keep this status resolver here so a future BoardComputer subsystem can
        // provide those values without changing the Bridge layout or label API.
        BoardComputerStatus status = AutopilotActive ? BoardComputerStatus.Active : BoardComputerStatus.Online;
        _boardComputerStatus.Text = status switch
        {
            BoardComputerStatus.Active => "ACTIVE",
            BoardComputerStatus.Limited => "LIMITED",
            BoardComputerStatus.Damaged => "DAMAGED",
            BoardComputerStatus.Offline => "OFFLINE",
            _ => "ONLINE"
        };
        _boardComputerCommand.Text = AutopilotActive ? "Autopilot" : "-";

        Color color = status switch
        {
            BoardComputerStatus.Limited => Yellow,
            BoardComputerStatus.Damaged => DamagedOrange,
            BoardComputerStatus.Offline => Red,
            _ => Green
        };
        SetLabelColor(_boardComputerStatus, color);

        _boardComputerLed.Texture = status switch
        {
            BoardComputerStatus.Limited or BoardComputerStatus.Damaged => Load("18..png"),
            BoardComputerStatus.Offline => Load("19.png"),
            _ => Load("17.png")
        };
        _boardComputerLed.Modulate = status == BoardComputerStatus.Damaged ? DamagedOrange : Colors.White;
    }

    private void SetContactValues(string name, string shipClass, string distance, string velocity, string reactor, string shields, string weapons, string hull)
    {
        _contactName.Text = name;
        _contactClass.Text = shipClass;
        _contactDistance.Text = distance;
        _contactVelocity.Text = velocity;
        _contactReactor.Text = reactor;
        _contactShields.Text = shields;
        _contactWeapons.Text = weapons;
        if (_contactHull is not null) _contactHull.Text = hull;
    }

    private static void SetSystemStatus(Label label, float fraction)
    {
        label.Text = fraction >= .999f ? "ONLINE" : fraction > 0f ? "LIMITED" : "OFFLINE";
        SetLabelColor(label, fraction >= .999f ? Green : fraction > 0f ? Yellow : Red);
    }

    private static void SetThrusterStatus(Label label, float fraction)
    {
        label.Text = fraction >= .999f ? "ONLINE" : fraction > 0f ? "LIMITED" : "OFFLINE";
        SetLabelColor(label, fraction >= .999f ? Green : fraction > 0f ? Yellow : Red);
    }

    private static void SetLabelColor(Label label, Color color) => label.AddThemeColorOverride("font_color", color);

    private Label AddText(Control parent, LayoutItem item)
    {
        var label = new Label
        {
            Name = item.Id,
            Position = new Vector2(item.X, item.Y),
            Size = new Vector2(item.Width, item.Height),
            Text = item.Text ?? string.Empty,
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        label.AddThemeFontOverride("font", _font);
        label.AddThemeFontSizeOverride("font_size", item.FontSize);
        label.AddThemeColorOverride("font_color", new Color(item.Color ?? "ffffff"));
        parent.AddChild(label);
        return label;
    }

    private static Sprite2D AddFullAsset(Control parent, LayoutItem item)
    {
        Texture2D texture = Load(item.File);
        var asset = new Sprite2D
        {
            Name = item.Id,
            Texture = texture,
            Position = new Vector2(item.X, item.Y),
            Centered = false,
            Scale = new Vector2(item.Width / (float)texture.GetWidth(), item.Height / (float)texture.GetHeight())
        };
        parent.AddChild(asset);
        return asset;
    }

    private static void AddSpriteAsset(Control parent, LayoutItem item)
    {
        if (item.Crop is null) return;
        var sprite = new Sprite2D
        {
            Name = item.Id,
            Texture = Load(item.File),
            Position = new Vector2(item.X, item.Y),
            Centered = false,
            RegionEnabled = true,
            RegionRect = new Rect2(item.Crop.X, item.Crop.Y, item.Crop.Width, item.Crop.Height),
            Scale = new Vector2(item.Width / item.Crop.Width, item.Height / item.Crop.Height)
        };
        parent.AddChild(sprite);
    }

    private Label Label(string id) => _labels[id];
    private Label? OptionalLabel(string id) => _labels.GetValueOrDefault(id);
    private ColorRect[] Bar(string id) => _bars[id];
    private static float Ratio(float value, float maximum) => maximum <= 0f ? 0f : Mathf.Clamp(value / maximum, 0f, 1f);
    private static Texture2D Load(string name) => GD.Load<Texture2D>($"res://Assets/Bridge/{name}");

    private sealed class LayoutItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string File { get; set; } = string.Empty;
        public string? Text { get; set; }
        public string? Color { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
        public int FontSize { get; set; }
        public int Segments { get; set; }
        public float Fill { get; set; }
        public float Gap { get; set; }
        public float SegmentWidth { get; set; }
        public float SegmentHeight { get; set; }
        public LayoutCrop? Crop { get; set; }
    }

    private sealed class LayoutCrop
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Width { get; set; }
        public float Height { get; set; }
    }

    private enum BoardComputerStatus
    {
        Online,
        Active,
        Limited,
        Damaged,
        Offline
    }
}
