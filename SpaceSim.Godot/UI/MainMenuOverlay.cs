using Godot;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>One generated loadout prepared for a direct duel. The player manually flies ship one.</summary>
public sealed record DuelShipSelection(string Name, GeneratedShipLoadout Loadout);

/// <summary>Modal start and pause menu. It owns only presentation and user intent.</summary>
public sealed partial class MainMenuOverlay : Control
{
    public event Action? StartRequested;
    public event Action<DuelShipSelection, DuelShipSelection>? DuelRequested;
    public event Action? ResumeRequested;
    public event Action? MainMenuRequested;
    public event Action? QuitRequested;
    public event Action<bool>? SensoriumEnabledChanged;
    public event Action<bool>? QuickStartEnabledChanged;
    public event Action<BoosterConfiguration>? BoosterConfigurationChanged;

    public bool IsOpen => Visible;
    public bool SensoriumEnabled { get; set; } = true;
    public bool QuickStartEnabled { get; set; }
    public BoosterConfiguration BoosterConfiguration { get; set; } = BoosterConfiguration.Default;

    private VBoxContainer _mainActions = null!;
    private VBoxContainer _pauseActions = null!;
    private VBoxContainer _options = null!;
    private VBoxContainer _boosters = null!;
    private VBoxContainer _duelSelection = null!;
    private OptionButton _duelPlayerClass = null!;
    private OptionButton _duelEnemyClass = null!;
    private Label _duelPlayerSummary = null!;
    private Label _duelEnemySummary = null!;
    private DuelShipSelection? _playerDuelLoadout;
    private DuelShipSelection? _enemyDuelLoadout;
    private Label _title = null!;
    private Label _subtitle = null!;
    private CheckButton _sensoriumToggle = null!;
    private CheckButton _quickStartToggle = null!;
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
        (VBoxContainer playerCard, _duelPlayerClass, _duelPlayerSummary) = DuelShipCard("SCHIFF 1 / SPIELER", GeneratePlayerDuelLoadout);
        (VBoxContainer enemyCard, _duelEnemyClass, _duelEnemySummary) = DuelShipCard("SCHIFF 2 / KESTREL", GenerateEnemyDuelLoadout);
        duelShips.AddChild(playerCard);
        duelShips.AddChild(enemyCard);
        _duelSelection.AddChild(duelShips);
        _duelSelection.AddChild(MenuButton("START DUEL", StartSelectedDuel));
        _duelSelection.AddChild(MenuButton("BACK", ShowMain));
        panel.AddChild(_duelSelection);
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

    private (VBoxContainer Card, OptionButton ClassSelector, Label Summary) DuelShipCard(string heading, Action generate)
    {
        var card = new VBoxContainer { CustomMinimumSize = new Vector2(395, 0), SizeFlagsHorizontal = SizeFlags.ExpandFill };
        card.AddThemeConstantOverride("separation", 7);
        card.AddChild(OptionTitle(heading));
        var selector = new OptionButton { CustomMinimumSize = new Vector2(395, 32) };
        selector.AddItem("INTERCEPTOR  |  90 - 120 PU", (int)EnemyShipClass.Interceptor);
        selector.AddItem("CORVETTE     |  110 - 150 PU", (int)EnemyShipClass.Corvette);
        selector.AddItem("FRIGATE      |  150 - 250 PU", (int)EnemyShipClass.Frigate);
        selector.Selected = (int)EnemyShipClass.Corvette;
        card.AddChild(selector);
        Button button = MenuButton("GENERATE", generate);
        button.CustomMinimumSize = new Vector2(395, 38);
        card.AddChild(button);
        var summary = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(395, 264) };
        summary.AddThemeFontSizeOverride("font_size", 13);
        summary.AddThemeColorOverride("font_color", ViewSettings.Text);
        card.AddChild(summary);
        return (card, selector, summary);
    }

    private void GeneratePlayerDuelLoadout()
    {
        _playerDuelLoadout = GenerateDuelLoadout((EnemyShipClass)_duelPlayerClass.GetSelectedId(), "SCHIFF 1");
        _duelPlayerSummary.Text = DescribeDuelLoadout(_playerDuelLoadout);
    }

    private void GenerateEnemyDuelLoadout()
    {
        _enemyDuelLoadout = GenerateDuelLoadout((EnemyShipClass)_duelEnemyClass.GetSelectedId(), "SCHIFF 2");
        _duelEnemySummary.Text = DescribeDuelLoadout(_enemyDuelLoadout);
    }

    private void StartSelectedDuel()
    {
        if (_playerDuelLoadout is null) GeneratePlayerDuelLoadout();
        if (_enemyDuelLoadout is null) GenerateEnemyDuelLoadout();
        DuelRequested?.Invoke(_playerDuelLoadout!, _enemyDuelLoadout!);
    }

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
