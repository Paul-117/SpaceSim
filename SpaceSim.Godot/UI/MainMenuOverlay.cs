using Godot;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>Modal start and pause menu. It owns only presentation and user intent.</summary>
public sealed partial class MainMenuOverlay : Control
{
    public event Action? StartRequested;
    public event Action? MainMenuRequested;
    public event Action? QuitRequested;
    public event Action<bool>? SensoriumEnabledChanged;
    public event Action<bool>? QuickStartEnabledChanged;

    public bool IsOpen => Visible;
    public bool SensoriumEnabled { get; set; } = true;
    public bool QuickStartEnabled { get; set; }

    private VBoxContainer _mainActions = null!;
    private VBoxContainer _pauseActions = null!;
    private VBoxContainer _options = null!;
    private Label _title = null!;
    private Label _subtitle = null!;
    private CheckButton _sensoriumToggle = null!;
    private CheckButton _quickStartToggle = null!;
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
        _mainActions.AddChild(MenuButton("OPTIONS", () => ShowOptions(false)));
        _mainActions.AddChild(MenuButton("QUIT", () => QuitRequested?.Invoke()));
        panel.AddChild(_mainActions);

        _pauseActions = new VBoxContainer { Visible = false };
        _pauseActions.AddThemeConstantOverride("separation", 9);
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
        _options.AddChild(MenuButton("BACK", () =>
        {
            if (_returnToPause) ShowPause();
            else ShowMain();
        }));
        panel.AddChild(_options);
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
        _options.Show();
        Show();
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

    private static Button MenuButton(string text, Action action)
    {
        Button button = CockpitButton.Create(text);
        button.CustomMinimumSize = new Vector2(360, 46);
        button.Pressed += action;
        return button;
    }
}
