using Godot;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>Modal start and pause menu. It owns only presentation and user intent.</summary>
public sealed partial class MainMenuOverlay : Control
{
    public event Action? StartRequested;
    public event Action? DuelRequested;
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
        _mainActions.AddChild(MenuButton("1VS1", () => DuelRequested?.Invoke()));
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
        _options.Show();
        Show();
    }

    public void HandleEscape()
    {
        if (_boosters.Visible) { ShowOptionsFromBoosters(); return; }
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
