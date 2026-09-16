using Godot;
using SpaceSim.Core.Power;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.Rendering;

namespace SpaceSim.GodotClient.UI;

/// <summary>Small functional development panel; all validation remains in the simulation core.</summary>
public partial class PowerDistributionPanel : Control
{
    public WorldState World { get; set; } = null!;
    public event Action<PowerAllocationCommand>? AdjustmentRequested;
    public Button PropulsionMinus { get; } = CockpitButton.Create("-");
    public Button PropulsionPlus { get; } = CockpitButton.Create("+");
    public Button WeaponsMinus { get; } = CockpitButton.Create("-");
    public Button WeaponsPlus { get; } = CockpitButton.Create("+");
    public Button ShieldsMinus { get; } = CockpitButton.Create("-");
    public Button ShieldsPlus { get; } = CockpitButton.Create("+");
    private readonly Label _propulsion = ValueLabel();
    private readonly Label _weapons = ValueLabel();
    private readonly Label _shields = ValueLabel();
    private readonly Label _total = ValueLabel();
    private readonly Label _shield = ValueLabel();

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        AddChild(Title("POWER DISTRIBUTION", 0));
        AddRow("ENGINES", 30, PropulsionMinus, _propulsion, PropulsionPlus);
        AddRow("WEAPONS", 64, WeaponsMinus, _weapons, WeaponsPlus);
        AddRow("SHIELDS", 98, ShieldsMinus, _shields, ShieldsPlus);
        AddChild(Title("TOTAL", 136));
        _total.Position = new Vector2(95, 132); _total.Size = new Vector2(160, 28); AddChild(_total);
        _shield.Position = new Vector2(10, 166); _shield.Size = new Vector2(250, 30); AddChild(_shield);
        PropulsionMinus.Pressed += () => Request(-1, 0, 0);
        PropulsionPlus.Pressed += () => Request(1, 0, 0);
        WeaponsMinus.Pressed += () => Request(0, -1, 0);
        WeaponsPlus.Pressed += () => Request(0, 1, 0);
        ShieldsMinus.Pressed += () => Request(0, 0, -1);
        ShieldsPlus.Pressed += () => Request(0, 0, 1);
    }

    public override void _Process(double delta)
    {
        Vector2 viewport = GetViewportRect().Size;
        Position = new Vector2(viewport.X - 292, 164);
        Size = new Vector2(262, 202);
        Refresh();
    }

    public override void _Draw()
    {
        DrawRect(new Rect2(Vector2.Zero, Size), new Color(0.0196f, 0.0314f, 0.0549f, 0.94f));
        DrawRect(new Rect2(Vector2.Zero, Size), ViewSettings.Line, false, 1);
    }

    private void Request(int propulsion, int weapons, int shields)
    {
        float step = World.Ship.Power.AdjustmentStep;
        AdjustmentRequested?.Invoke(new PowerAllocationCommand(propulsion * step, weapons * step, shields * step));
    }

    private void Refresh()
    {
        if (World is null) return;
        var power = World.Ship.Power;
        float step = power.AdjustmentStep;
        _propulsion.Text = $"{power.PropulsionAllocation:0}";
        _weapons.Text = $"{power.WeaponsAllocation:0}";
        _shields.Text = $"{power.ShieldsAllocation:0}";
        _total.Text = $"{power.AllocatedPower:0} / {power.AvailablePower:0}";
        var shield = World.Ship.Shield;
        string shieldStatus = shield.IsRechargeDelayed ? "RECHARGE DELAY" :
            shield.CurrentShield >= shield.MaximumShield ? "SHIELD FULL" : "RECHARGING";
        _shield.Text = $"SHIELD  {shield.CurrentShield:0} / {shield.MaximumShield:0}  {shieldStatus}";
        bool capacity = power.UnallocatedPower >= step - 0.0001f;
        PropulsionMinus.Disabled = power.PropulsionAllocation < step;
        WeaponsMinus.Disabled = power.WeaponsAllocation < step;
        ShieldsMinus.Disabled = power.ShieldsAllocation < step;
        PropulsionPlus.Disabled = !capacity;
        WeaponsPlus.Disabled = !capacity;
        ShieldsPlus.Disabled = !capacity;
    }

    private void AddRow(string name, float y, Button minus, Label value, Button plus)
    {
        AddChild(Title(name, y));
        minus.Position = new Vector2(94, y); minus.Size = new Vector2(35, 28); AddChild(minus);
        value.Position = new Vector2(136, y + 2); value.Size = new Vector2(38, 25); AddChild(value);
        plus.Position = new Vector2(180, y); plus.Size = new Vector2(35, 28); AddChild(plus);
    }

    private static Label Title(string text, float y)
    {
        var label = new Label { Text = text, Position = new Vector2(10, y + 5), Size = new Vector2(85, 25) };
        label.AddThemeColorOverride("font_color", ViewSettings.Muted);
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private static Label ValueLabel()
    {
        var label = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        label.AddThemeColorOverride("font_color", ViewSettings.Text);
        label.AddThemeFontSizeOverride("font_size", 14);
        return label;
    }
}
