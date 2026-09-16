using Godot;
using SpaceSim.Core.Ships;

namespace SpaceSim.GodotClient.Input;

public sealed class KeyboardShipControl : IShipControl
{
    private bool _firePressed;
    public bool IsFocused { get; set; } = true;

    public void HandleInput(InputEvent input)
    {
        if (IsFocused && input is InputEventKey { Pressed: true, Echo: false } key &&
            (key.PhysicalKeycode == Key.Space || key.Keycode == Key.Space))
            _firePressed = true;
    }

    public void Clear() => _firePressed = false;

    public ShipCommand ReadCommand()
    {
        bool fire = _firePressed;
        _firePressed = false;
        if (!IsFocused) return default;
        return new ShipCommand(
            Godot.Input.IsPhysicalKeyPressed(Key.W),
            Godot.Input.IsPhysicalKeyPressed(Key.S),
            Godot.Input.IsPhysicalKeyPressed(Key.A),
            Godot.Input.IsPhysicalKeyPressed(Key.D), fire);
    }
}
