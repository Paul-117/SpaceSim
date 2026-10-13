using Godot;
using System.Text.Json;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.Core.Power;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>One generated loadout prepared for a direct duel. The player manually flies ship one.</summary>
public sealed record DuelShipSelection(string Name, GeneratedShipLoadout Loadout);

/// <summary>Modal start and pause menu. It owns only presentation and user intent.</summary>
public sealed partial class MainMenuOverlay : Control
{
    private const string DuelLoadoutsPath = "user://duel_loadouts.cfg";
    public event Action? StartRequested;
    public event Action<DuelShipSelection, DuelShipSelection>? DuelRequested;
    public event Action? ResumeRequested;
    public event Action? MainMenuRequested;
    public event Action? QuitRequested;
    public event Action<bool>? SensoriumEnabledChanged;
    public event Action<bool>? QuickStartEnabledChanged;
    public event Action<bool>? VoiceInputEnabledChanged;
    public event Action<bool>? VoiceOutputEnabledChanged;
    public event Action<BoosterConfiguration>? BoosterConfigurationChanged;

    public bool IsOpen => Visible;
    public bool SensoriumEnabled { get; set; } = true;
    public bool QuickStartEnabled { get; set; }
    public bool VoiceInputEnabled { get; set; } = true;
    public bool VoiceOutputEnabled { get; set; } = true;
    public BoosterConfiguration BoosterConfiguration { get; set; } = BoosterConfiguration.Default;

    private VBoxContainer _mainActions = null!;
    private VBoxContainer _pauseActions = null!;
    private VBoxContainer _options = null!;
    private VBoxContainer _boosters = null!;
    private VBoxContainer _duelSelection = null!;
    private DuelLoadoutCard _playerDuelCard = null!;
    private DuelLoadoutCard _enemyDuelCard = null!;
    private DuelShipSelection? _playerDuelLoadout;
    private DuelShipSelection? _enemyDuelLoadout;
    private Label _title = null!;
    private Label _subtitle = null!;
    private CheckButton _sensoriumToggle = null!;
    private CheckButton _quickStartToggle = null!;
    private CheckButton _voiceInputToggle = null!;
    private CheckButton _voiceOutputToggle = null!;
    private SpinBox _mainBoosterPower = null!;
    private SpinBox _mainBoosterRamp = null!;
    private SpinBox _mainBoosterSpeed = null!;
    private SpinBox _reverseBoosterPower = null!;
    private SpinBox _reverseBoosterSpeed = null!;
    private SpinBox _sideBoosterPower = null!;
    private SpinBox _sideBoosterRotationSpeed = null!;
    private bool _synchronizingBoosterFields;
    private bool _returnToPause;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;

        AddChild(new ColorRect
        {
            Color = new Color("02050ae8"),
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f
        });

        var center = new CenterContainer
        {
            AnchorRight = 1f,
            AnchorBottom = 1f,
            MouseFilter = MouseFilterEnum.Stop
        };
        AddChild(center);

        var panel = new VBoxContainer
        {
            CustomMinimumSize = new Vector2(360, 0),
            MouseFilter = MouseFilterEnum.Stop
        };
        panel.AddThemeConstantOverride("separation", 14);
        center.AddChild(panel);

        _title = Title("SPACESIM");
        _subtitle = Subtitle("BRIDGE SYSTEMS ONLINE");
        panel.AddChild(_title);
        panel.AddChild(_subtitle);
        panel.AddChild(new HSeparator());

        _mainActions = new VBoxContainer();
        _mainActions.AddThemeConstantOverride("separation", 9);
        _mainActions.AddChild(MenuButton("START", () => StartRequested?.Invoke()));
        _mainActions.AddChild(MenuButton("1VS1", ShowDuelSelection));
        _mainActions.AddChild(MenuButton("OPTIONS", () => ShowOptions(false)));
        _mainActions.AddChild(MenuButton("QUIT", () => QuitRequested?.Invoke()));
        panel.AddChild(_mainActions);

        _pauseActions = new VBoxContainer { Visible = false };
        _pauseActions.AddThemeConstantOverride("separation", 9);
        _pauseActions.AddChild(MenuButton("RESUME", () => ResumeRequested?.Invoke()));
        _pauseActions.AddChild(MenuButton("OPTIONS", () => ShowOptions(true)));
        _pauseActions.AddChild(MenuButton("MAIN MENU", () => MainMenuRequested?.Invoke()));
        _pauseActions.AddChild(MenuButton("QUIT", () => QuitRequested?.Invoke()));
        panel.AddChild(_pauseActions);

        _options = new VBoxContainer { Visible = false };
        _options.AddThemeConstantOverride("separation", 10);
        _options.AddChild(OptionTitle("OPTIONS"));
        _sensoriumToggle = OptionToggle("SENSORIUM");
        _sensoriumToggle.Toggled += enabled =>
        {
            SensoriumEnabled = enabled;
            SensoriumEnabledChanged?.Invoke(enabled);
        };
        _options.AddChild(_sensoriumToggle);
        _quickStartToggle = OptionToggle("QUICK START");
        _quickStartToggle.Toggled += enabled =>
        {
            QuickStartEnabled = enabled;
            QuickStartEnabledChanged?.Invoke(enabled);
        };
        _options.AddChild(_quickStartToggle);
        _voiceInputToggle = OptionToggle("VOICE INPUT");
        _voiceInputToggle.Toggled += enabled =>
        {
            VoiceInputEnabled = enabled;
            VoiceInputEnabledChanged?.Invoke(enabled);
        };
        _options.AddChild(_voiceInputToggle);
        _voiceOutputToggle = OptionToggle("VOICE OUTPUT");
        _voiceOutputToggle.Toggled += enabled =>
        {
            VoiceOutputEnabled = enabled;
            VoiceOutputEnabledChanged?.Invoke(enabled);
        };
        _options.AddChild(_voiceOutputToggle);
        _options.AddChild(MenuButton("BOOSTERS", ShowBoosters));
        _options.AddChild(MenuButton("BACK", () =>
        {
            if (_returnToPause) ShowPause();
            else ShowMain();
        }));
        panel.AddChild(_options);

        _boosters = new VBoxContainer { Visible = false };
        _boosters.AddThemeConstantOverride("separation", 8);
        _boosters.AddChild(OptionTitle("BOOSTER CONFIGURATION"));
        _boosters.AddChild(OptionTitle("MAIN BOOSTER"));
        _mainBoosterPower = BoosterValue("Leistung (kN)", 1, 1_000, .1);
        _mainBoosterRamp = BoosterValue("Ramp-up (s)", .1, 60, .1);
        _mainBoosterSpeed = BoosterValue("Max. Geschwindigkeit (m/s)", 1, 10_000, 1);
        _boosters.AddChild(_mainBoosterPower);
        _boosters.AddChild(_mainBoosterRamp);
        _boosters.AddChild(_mainBoosterSpeed);
        _boosters.AddChild(new HSeparator());
        _boosters.AddChild(OptionTitle("REVERSE BOOSTER"));
        _reverseBoosterPower = BoosterValue("Leistung (kN)", 1, 1_000, .1);
        _reverseBoosterSpeed = BoosterValue("Max. Geschwindigkeit (m/s)", 1, 10_000, 1);
        _boosters.AddChild(_reverseBoosterPower);
        _boosters.AddChild(_reverseBoosterSpeed);
        _boosters.AddChild(new HSeparator());
        _boosters.AddChild(OptionTitle("SIDE BOOSTER"));
        _sideBoosterPower = BoosterValue("Leistung (kN)", .1, 100, .01);
        _sideBoosterRotationSpeed = BoosterValue("Max. Rotation (Grad/s)", 1, 2_000, 1);
        _boosters.AddChild(_sideBoosterPower);
        _boosters.AddChild(_sideBoosterRotationSpeed);
        _boosters.AddChild(MenuButton("BACK", ShowOptionsFromBoosters));
        foreach (SpinBox field in new[] { _mainBoosterPower, _mainBoosterRamp, _mainBoosterSpeed,
            _reverseBoosterPower, _reverseBoosterSpeed, _sideBoosterPower, _sideBoosterRotationSpeed })
            field.ValueChanged += _ => PublishBoosterConfiguration();
        panel.AddChild(_boosters);

        _duelSelection = new VBoxContainer { Visible = false, CustomMinimumSize = new Vector2(820, 0) };
        _duelSelection.AddThemeConstantOverride("separation", 9);
        _duelSelection.AddChild(OptionTitle("1 VS 1 LOADOUTS"));
        _duelSelection.AddChild(Subtitle("SCHIFF 1: SPIELERSTEUERUNG  ·  SCHIFF 2: KESTREL-KI"));
        var duelShips = new HBoxContainer();
        duelShips.AddThemeConstantOverride("separation", 18);
        _playerDuelCard = new DuelLoadoutCard("SCHIFF 1 / SPIELER", "SCHIFF 1");
        _enemyDuelCard = new DuelLoadoutCard("SCHIFF 2 / KESTREL", "SCHIFF 2");
        duelShips.AddChild(_playerDuelCard.Root);
        duelShips.AddChild(_enemyDuelCard.Root);
        _duelSelection.AddChild(duelShips);
        _duelSelection.AddChild(MenuButton("START DUEL", StartSelectedDuel));
        _duelSelection.AddChild(MenuButton("BACK", ShowMain));
        panel.AddChild(_duelSelection);
        LoadDuelLoadouts();
        Hide();
    }

    public void ShowMain()
    {
        _returnToPause = false;
        _title.Text = "SPACESIM";
        _subtitle.Text = "BRIDGE SYSTEMS ONLINE";
        _mainActions.Show();
        _pauseActions.Hide();
        _options.Hide();
        _boosters.Hide();
        _duelSelection.Hide();
        Show();
    }

    public void ShowPause()
    {
        _returnToPause = true;
        _title.Text = "PAUSED";
        _subtitle.Text = "BRIDGE MENU";
        _mainActions.Hide();
        _pauseActions.Show();
        _options.Hide();
        _boosters.Hide();
        _duelSelection.Hide();
        Show();
    }

    public void ShowOptions(bool returnToPause)
    {
        _returnToPause = returnToPause;
        _title.Text = "OPTIONS";
        _subtitle.Text = "BRIDGE CONFIGURATION";
        _sensoriumToggle.ButtonPressed = SensoriumEnabled;
        _quickStartToggle.ButtonPressed = QuickStartEnabled;
        _voiceInputToggle.ButtonPressed = VoiceInputEnabled;
        _voiceOutputToggle.ButtonPressed = VoiceOutputEnabled;
        _mainActions.Hide();
        _pauseActions.Hide();
        _boosters.Hide();
        _duelSelection.Hide();
        _options.Show();
        Show();
    }

    public void HandleEscape()
    {
        if (_boosters.Visible) { ShowOptionsFromBoosters(); return; }
        if (_duelSelection.Visible) { ShowMain(); return; }
        if (_options.Visible)
        {
            if (_returnToPause) ShowPause(); else ShowMain();
            return;
        }
        if (_pauseActions.Visible) ResumeRequested?.Invoke();
    }

    private void ShowBoosters()
    {
        SyncBoosterFields();
        _title.Text = "BOOSTERS";
        _subtitle.Text = "NOMAD PROPULSION CONFIGURATION";
        _mainActions.Hide();
        _pauseActions.Hide();
        _options.Hide();
        _boosters.Show();
    }

    public void ShowDuelSelection()
    {
        if (_playerDuelLoadout is null) GeneratePlayerDuelLoadout();
        if (_enemyDuelLoadout is null) GenerateEnemyDuelLoadout();
        _title.Text = "1 VS 1";
        _subtitle.Text = "SELECT PROCEDURAL LOADOUTS";
        _mainActions.Hide();
        _pauseActions.Hide();
        _options.Hide();
        _boosters.Hide();
        _duelSelection.Show();
        Show();
    }

    private void GeneratePlayerDuelLoadout()
    {
        _playerDuelLoadout = _playerDuelCard.Generate();
    }

    private void GenerateEnemyDuelLoadout()
    {
        _enemyDuelLoadout = _enemyDuelCard.Generate();
    }

    private void StartSelectedDuel()
    {
        try
        {
            _playerDuelLoadout = _playerDuelCard.BuildSelection();
            _enemyDuelLoadout = _enemyDuelCard.BuildSelection();
            SaveDuelLoadouts();
            DuelRequested?.Invoke(_playerDuelLoadout, _enemyDuelLoadout);
        }
        catch (InvalidOperationException error)
        {
            _playerDuelCard.ShowError(error.Message);
            _enemyDuelCard.ShowError(error.Message);
        }
    }

    /// <summary>Saves both editable cards only after they have passed the loadout validation.</summary>
    private void SaveDuelLoadouts()
    {
        var preferences = new ConfigFile();
        preferences.SetValue("duel", "saved_loadouts", JsonSerializer.Serialize(new SavedDuelLoadouts(
            _playerDuelCard.CurrentConfiguration, _playerDuelCard.LockedFieldNames,
            _enemyDuelCard.CurrentConfiguration, _enemyDuelCard.LockedFieldNames)));
        if (preferences.Save(DuelLoadoutsPath) != Error.Ok)
            GD.PushWarning("Could not save 1VS1 loadouts.");
    }

    /// <summary>Restores the last valid player/enemy setup, including every per-row LOCK state.</summary>
    private void LoadDuelLoadouts()
    {
        var preferences = new ConfigFile();
        if (preferences.Load(DuelLoadoutsPath) != Error.Ok || !preferences.HasSectionKey("duel", "saved_loadouts")) return;
        try
        {
            SavedDuelLoadouts? saved = JsonSerializer.Deserialize<SavedDuelLoadouts>(
                preferences.GetValue("duel", "saved_loadouts").AsString());
            if (saved is null) return;
            _playerDuelCard.Restore(saved.Player, saved.PlayerLocks);
            _enemyDuelCard.Restore(saved.Enemy, saved.EnemyLocks);
            _playerDuelLoadout = _playerDuelCard.BuildSelection();
            _enemyDuelLoadout = _enemyDuelCard.BuildSelection();
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Could not restore saved 1VS1 loadouts: {exception.Message}");
        }
    }

    private sealed record SavedDuelLoadouts(
        DuelLoadoutConfiguration Player, string[] PlayerLocks,
        DuelLoadoutConfiguration Enemy, string[] EnemyLocks);

    private static DuelShipSelection GenerateDuelLoadout(EnemyShipClass shipClass, string prefix)
    {
        GeneratedShipLoadout? loadout = null;
        for (int attempt = 0; attempt < 48 && loadout is null; attempt++)
        {
            try { loadout = ShipLoadoutGenerator.Generate(shipClass, Random.Shared.Next()); }
            catch (InvalidOperationException) { }
        }
        if (loadout is null) throw new InvalidOperationException($"Kein gültiges Loadout für {shipClass} gefunden.");
        string[] names = ["AURORA", "CALYPSO", "DAUNTLESS", "ECLIPSE", "HELIOS", "KESTREL", "MERIDIAN", "ORION", "PEREGRINE", "VANGUARD"];
        return new DuelShipSelection($"{prefix} {names[Random.Shared.Next(names.Length)]}", loadout);
    }

    private static string DescribeDuelLoadout(DuelShipSelection selection)
    {
        GeneratedShipLoadout loadout = selection.Loadout;
        var tuning = loadout.Tuning;
        return $"NAME: {selection.Name}\n" +
               $"KLASSE: {loadout.ShipClass}  ·  SUBKLASSE: {loadout.Subclass}\n" +
               $"GEWICHT: {loadout.Hull.MassKg / 1000f:0.00} t  ·  HÜLLE: {loadout.Hull.MaximumHull:0} HP\n\n" +
               $"REAKTOR: {tuning.Reactor.Name}\n" +
               $"  Output {tuning.Reactor.MaximumOutputPower:0} PU · Fuel {tuning.Reactor.MaximumFuelUsagePerMinute:0.0}/min · Ramp {tuning.Reactor.RampUpSeconds:0} s\n" +
               $"BUGWAFFE: {tuning.BowWeapon.Name}\n" +
               $"  Schaden {tuning.BowWeapon.Damage:0} · Reichweite {tuning.BowWeapon.RangeMeters:0} m · Laden {tuning.BowWeapon.ChargeSeconds:0.0} s · {tuning.BowWeapon.DamagePerSecond:0.00} DPS\n" +
               $"  Schwenk +/-{tuning.BowWeapon.TurretMaximumAngleDegrees:0} Grad · {tuning.BowWeapon.TurretDegreesPerSecond:0.0} Grad/s · {tuning.BowWeapon.PowerDraw:0} PU\n" +
               $"SCHILD: {tuning.Shield.Name}\n" +
               $"  {tuning.Shield.MaximumHitPoints:0} HP · Aufladen {tuning.Shield.RechargeSeconds:0.0} s · Reboot {tuning.Shield.RebootSeconds:0} s · {tuning.Shield.PowerDraw:0} PU\n" +
               $"SENSOREN: {loadout.Sensor.Name}\n" +
               $"  Passive Reichweite {loadout.Sensor.MinimumRangeMeters:0}-{loadout.Sensor.MaximumRangeMeters:0} m · {loadout.Sensor.PowerUsage:0} PU\n" +
               $"MAIN: {tuning.MainBooster.Name}\n" +
               $"  {tuning.MainBooster.ThrustNewtons / 1000f:0} kN · Ramp {tuning.MainBooster.RampUpSeconds:0.0} s · {tuning.MainBooster.MaximumSpeedMetersPerSecond:0} m/s · {tuning.MainBooster.PowerDraw:0} PU\n" +
               $"REVERSE: {tuning.ReverseBooster.Name}\n" +
               $"  {tuning.ReverseBooster.ThrustNewtons / 1000f:0} kN · {tuning.ReverseBooster.MaximumSpeedMetersPerSecond:0} m/s · {tuning.ReverseBooster.PowerDraw:0} PU\n" +
               $"SIDE: {tuning.SideBooster.Name}\n" +
               $"  {tuning.SideBooster.ThrustNewtons / 1000f:0.0} kN · {tuning.SideBooster.MaximumRotationDegreesPerSecond:0} Grad/s · {tuning.SideBooster.PowerDraw:0} PU\n" +
               $"BOARDCOMPUTER: {loadout.BoardComputer.Name}\n" +
               $"  Update {loadout.BoardComputer.CommandUpdateIntervalTicks} Ticks · Reaktion {loadout.BoardComputer.ReactionDelayTicks} Ticks · Feuer-Toleranz x{loadout.BoardComputer.FireAimToleranceMultiplier:0.00}";
    }

    /// <summary>Per-field editor used by 1VS1. A checked LOCK row is retained by the next generation.</summary>
    private sealed class DuelLoadoutCard
    {
        private readonly string _prefix;
        private readonly Dictionary<string, OptionButton> _fields = [];
        private readonly Dictionary<string, CheckButton> _locks = [];
        private readonly Label _status;
        private IReadOnlyList<(float MassKg, float Hull)> _hullOptions = [];
        private bool _synchronizing;

        public VBoxContainer Root { get; }

        public DuelLoadoutCard(string heading, string prefix)
        {
            _prefix = prefix;
            Root = new VBoxContainer { CustomMinimumSize = new Vector2(395, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
            Root.AddThemeConstantOverride("separation", 6);
            Root.AddChild(OptionTitle(heading));
            var generate = MenuButton("GENERATE", GenerateFromButton);
            generate.CustomMinimumSize = new Vector2(395, 34);
            Root.AddChild(generate);
            _status = new Label { Text = "UNLOCKED ROWS WILL BE GENERATED", AutowrapMode = TextServer.AutowrapMode.WordSmart };
            _status.AddThemeFontSizeOverride("font_size", 11);
            _status.AddThemeColorOverride("font_color", ViewSettings.Muted);
            Root.AddChild(_status);

            var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(395, 392), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
            var rows = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            rows.AddThemeConstantOverride("separation", 4);
            scroll.AddChild(rows);
            Root.AddChild(scroll);
            AddRow(rows, "name", "NAME");
            AddRow(rows, "class", "CLASS");
            AddRow(rows, "subclass", "SUBCLASS");
            AddRow(rows, "mass", "MASS");
            AddRow(rows, "hull", "HULL HP");
            AddRow(rows, "reactor", "REACTOR");
            AddRow(rows, "weapon", "BOW WEAPON");
            AddRow(rows, "shield", "SHIELD");
            AddRow(rows, "sensor", "SENSORS");
            AddRow(rows, "main", "MAIN BOOSTER");
            AddRow(rows, "reverse", "REVERSE BOOSTER");
            AddRow(rows, "side", "SIDE BOOSTER");
            AddRow(rows, "computer", "BOARD COMPUTER");
            _fields["class"].ItemSelected += _ =>
            {
                if (!_synchronizing) SetHullOptions((EnemyShipClass)_fields["class"].GetSelectedId(), null, null);
            };
            SetConfiguration(DuelLoadoutFactory.Generate(null, new HashSet<string>()));
        }

        public DuelShipSelection Generate()
        {
            DuelLoadoutConfiguration configuration = DuelLoadoutFactory.Generate(ReadConfiguration(), LockedFields());
            SetConfiguration(configuration);
            _status.Text = "GENERATED — LOCKED ROWS RETAINED";
            _status.AddThemeColorOverride("font_color", ViewSettings.Green);
            return BuildSelection();
        }

        public DuelShipSelection BuildSelection()
        {
            DuelLoadoutConfiguration configuration = ReadConfiguration();
            GeneratedShipLoadout loadout = DuelLoadoutFactory.Build(configuration);
            return new DuelShipSelection($"{_prefix} {configuration.Name}", loadout);
        }

        public DuelLoadoutConfiguration CurrentConfiguration => ReadConfiguration();

        public string[] LockedFieldNames => LockedFields().OrderBy(key => key, StringComparer.Ordinal).ToArray();

        public void Restore(DuelLoadoutConfiguration configuration, IEnumerable<string>? locks)
        {
            SetConfiguration(configuration);
            var requestedLocks = (locks ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, CheckButton toggle) in _locks)
                toggle.ButtonPressed = requestedLocks.Contains(key);
        }

        public void ShowError(string text)
        {
            _status.Text = text;
            _status.AddThemeColorOverride("font_color", new Color("ff7777"));
        }

        private void GenerateFromButton()
        {
            try { Generate(); }
            catch (InvalidOperationException error) { ShowError(error.Message); }
        }

        private void AddRow(Control parent, string key, string title)
        {
            var row = new HBoxContainer { CustomMinimumSize = new Vector2(380, 29) };
            row.AddThemeConstantOverride("separation", 5);
            var label = new Label { Text = title, CustomMinimumSize = new Vector2(88, 0), VerticalAlignment = VerticalAlignment.Center };
            label.AddThemeFontSizeOverride("font_size", 11);
            label.AddThemeColorOverride("font_color", ViewSettings.Muted);
            row.AddChild(label);
            var choice = new OptionButton { SizeFlagsHorizontal = SizeFlags.ExpandFill, TooltipText = title };
            choice.AddThemeFontSizeOverride("font_size", 11);
            _fields[key] = choice;
            row.AddChild(choice);
            var locked = new CheckButton { Text = "LOCK", CustomMinimumSize = new Vector2(63, 0), TooltipText = $"Keep {title} on the next generation" };
            locked.AddThemeFontSizeOverride("font_size", 10);
            _locks[key] = locked;
            row.AddChild(locked);
            parent.AddChild(row);
        }

        private void SetConfiguration(DuelLoadoutConfiguration value)
        {
            _synchronizing = true;
            SetOptions("name", DuelLoadoutFactory.Names.Select((name, index) => (name, index)), Array.IndexOf(DuelLoadoutFactory.Names, value.Name));
            SetOptions("class", Enum.GetValues<EnemyShipClass>().Select(kind => (kind.ToString().ToUpperInvariant(), (int)kind)), (int)value.ShipClass);
            SetOptions("subclass", Enum.GetValues<ShipSubclass>().Select(kind => (kind.ToString().ToUpperInvariant(), (int)kind)), (int)value.Subclass);
            SetHullOptions(value.ShipClass, value.MassKg, value.MaximumHull);
            SetOptions("reactor", ReactorDefinitions.All.Select(item => ($"{item.Name} · {item.MaximumOutputPower:0} PU", (int)item.Type)), (int)value.Reactor);
            SetOptions("weapon", BowWeaponDefinitions.All.Select(item => ($"{item.Name} · {item.RangeMeters:0} m", (int)item.Type)), (int)value.BowWeapon);
            SetOptions("shield", ShieldDefinitions.All.Select(item => ($"{item.Name} · {item.MaximumHitPoints:0} HP", (int)item.Type)), (int)value.Shield);
            SetOptions("sensor", EnemySensorDefinitions.All.Select(item => ($"{item.Name} · {item.MaximumRangeMeters:0} m", (int)item.Type)), (int)value.Sensor);
            SetOptions("main", BoosterDefinitions.MainAll.Select(item => ($"{item.Name} · {item.MaximumSpeedMetersPerSecond:0} m/s", (int)item.Type)), (int)value.MainBooster);
            SetOptions("reverse", BoosterDefinitions.ReverseAll.Select(item => ($"{item.Name} · {item.MaximumSpeedMetersPerSecond:0} m/s", (int)item.Type)), (int)value.ReverseBooster);
            SetOptions("side", BoosterDefinitions.SideAll.Select(item => ($"{item.Name} · {item.MaximumRotationDegreesPerSecond:0}°/s", (int)item.Type)), (int)value.SideBooster);
            SetOptions("computer", Enum.GetValues<BoardComputerClass>().SelectMany(kind => Enumerable.Range(1, 5)
                .Select(mark => ($"{kind.ToString().ToUpperInvariant()} MK {Roman(mark)}", (int)kind * 10 + mark))), (int)value.BoardComputerClass * 10 + value.BoardComputerMark);
            _synchronizing = false;
        }

        private void SetHullOptions(EnemyShipClass shipClass, float? mass, float? hull)
        {
            _hullOptions = DuelLoadoutFactory.HullOptions(shipClass);
            int selectedMass = mass is null ? 0 : ClosestIndex(_hullOptions.Select(option => option.MassKg), mass.Value);
            int selectedHull = hull is null ? 0 : ClosestIndex(_hullOptions.Select(option => option.Hull), hull.Value);
            SetOptions("mass", _hullOptions.Select((option, index) => ($"{option.MassKg / 1000f:0.00} t", index)), selectedMass);
            SetOptions("hull", _hullOptions.Select((option, index) => ($"{option.Hull:0} HP", index)), selectedHull);
        }

        private DuelLoadoutConfiguration ReadConfiguration()
        {
            int board = _fields["computer"].GetSelectedId();
            int massIndex = Math.Clamp(_fields["mass"].GetSelectedId(), 0, _hullOptions.Count - 1);
            int hullIndex = Math.Clamp(_fields["hull"].GetSelectedId(), 0, _hullOptions.Count - 1);
            return new DuelLoadoutConfiguration(
                DuelLoadoutFactory.Names[Math.Clamp(_fields["name"].GetSelectedId(), 0, DuelLoadoutFactory.Names.Length - 1)],
                (EnemyShipClass)_fields["class"].GetSelectedId(), (ShipSubclass)_fields["subclass"].GetSelectedId(),
                _hullOptions[massIndex].MassKg, _hullOptions[hullIndex].Hull,
                (ReactorType)_fields["reactor"].GetSelectedId(), (BowWeaponType)_fields["weapon"].GetSelectedId(),
                (ShieldType)_fields["shield"].GetSelectedId(), (EnemySensorType)_fields["sensor"].GetSelectedId(),
                (MainBoosterType)_fields["main"].GetSelectedId(), (ReverseBoosterType)_fields["reverse"].GetSelectedId(),
                (SideBoosterType)_fields["side"].GetSelectedId(), (BoardComputerClass)(board / 10), board % 10);
        }

        private HashSet<string> LockedFields() => _locks.Where(pair => pair.Value.ButtonPressed)
            .Select(pair => pair.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        private void SetOptions(string key, IEnumerable<(string Text, int Id)> options, int selectedId)
        {
            OptionButton field = _fields[key];
            field.Clear();
            int selected = 0;
            int index = 0;
            foreach ((string text, int id) in options)
            {
                field.AddItem(text, id);
                if (id == selectedId) selected = index;
                index++;
            }
            field.Selected = selected;
        }

        private static int ClosestIndex(IEnumerable<float> values, float target) => values.Select((value, index) => (index, delta: MathF.Abs(value - target)))
            .OrderBy(item => item.delta).First().index;

        private static string Roman(int value) => value switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => value.ToString() };
    }

    private void ShowOptionsFromBoosters() => ShowOptions(_returnToPause);

    private void SyncBoosterFields()
    {
        _synchronizingBoosterFields = true;
        BoosterConfiguration value = BoosterConfiguration.Clamp();
        _mainBoosterPower.Value = value.MainBoosterKilonewtons;
        _mainBoosterRamp.Value = value.MainRampUpSeconds;
        _mainBoosterSpeed.Value = value.MaximumForwardSpeedMetersPerSecond;
        _reverseBoosterPower.Value = value.ReverseBoosterKilonewtons;
        _reverseBoosterSpeed.Value = value.MaximumReverseSpeedMetersPerSecond;
        _sideBoosterPower.Value = value.SideBoosterKilonewtons;
        _sideBoosterRotationSpeed.Value = value.MaximumRotationDegreesPerSecond;
        _synchronizingBoosterFields = false;
    }

    private void PublishBoosterConfiguration()
    {
        if (_synchronizingBoosterFields) return;
        BoosterConfiguration = new BoosterConfiguration((float)_mainBoosterPower.Value, (float)_mainBoosterRamp.Value,
            (float)_mainBoosterSpeed.Value, (float)_reverseBoosterPower.Value, (float)_reverseBoosterSpeed.Value,
            (float)_sideBoosterPower.Value, (float)_sideBoosterRotationSpeed.Value).Clamp();
        BoosterConfigurationChanged?.Invoke(BoosterConfiguration);
    }

    private static Label Title(string text)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 38);
        label.AddThemeColorOverride("font_color", ViewSettings.Cyan);
        return label;
    }

    private static Label Subtitle(string text)
    {
        var label = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeFontSizeOverride("font_size", 14);
        label.AddThemeColorOverride("font_color", ViewSettings.Muted);
        return label;
    }

    private static Label OptionTitle(string text)
    {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 17);
        label.AddThemeColorOverride("font_color", ViewSettings.Text);
        return label;
    }

    private static CheckButton OptionToggle(string text)
    {
        var button = new CheckButton { Text = text, FocusMode = FocusModeEnum.None };
        button.AddThemeFontSizeOverride("font_size", 16);
        button.AddThemeColorOverride("font_color", ViewSettings.Text);
        return button;
    }

    private static SpinBox BoosterValue(string label, double minimum, double maximum, double step)
    {
        var value = new SpinBox { MinValue = minimum, MaxValue = maximum, Step = step, Rounded = false };
        value.CustomMinimumSize = new Vector2(360, 34);
        value.Prefix = label + ": ";
        value.AddThemeFontSizeOverride("font_size", 16);
        return value;
    }

    private static Button MenuButton(string text, Action action)
    {
        Button button = CockpitButton.Create(text);
        button.CustomMinimumSize = new Vector2(360, 46);
        button.Pressed += action;
        return button;
    }
}
