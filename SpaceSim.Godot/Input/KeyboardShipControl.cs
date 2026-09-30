using Godot;
using SpaceSim.Core.Ships;

namespace SpaceSim.GodotClient.Input;

public sealed class KeyboardShipControl : IShipControl
{
    private bool _firePressed;
    private float _mainThrottle;
    public bool IsFocused { get; set; } = true;
    public float MainThrottleRiseSeconds { get; set; } = 5f;
    public float MainThrottleFallSeconds { get; set; } = 3f;

    public void HandleInput(InputEvent input)
    {
        if (IsFocused && input is InputEventKey { Pressed: true, Echo: false } key &&
            (key.PhysicalKeycode == Key.Space || key.Keycode == Key.Space))
            _firePressed = true;
    }

    public void Clear()
    {
        _firePressed = false;
        _mainThrottle = 0f;
    }

    public ShipCommand ReadCommand()
    {
        bool fire = _firePressed;
        _firePressed = false;
        if (!IsFocused) return default;
        bool main = Godot.Input.IsPhysicalKeyPressed(Key.W);
        bool reverse = Godot.Input.IsPhysicalKeyPressed(Key.S);
        bool left = Godot.Input.IsPhysicalKeyPressed(Key.A);
        bool right = Godot.Input.IsPhysicalKeyPressed(Key.D);
        _mainThrottle = Ramp(_mainThrottle, main);
        return new ShipCommand(main || _mainThrottle > 0f, reverse, left, right, fire,
            MainThrustIntensity: _mainThrottle);
    }

    private float Ramp(float current, bool held)
    {
        float seconds = held ? MainThrottleRiseSeconds : MainThrottleFallSeconds;
        float step = seconds > 0f && float.IsFinite(seconds) ? 1f / seconds / 60f : 1f;
        return held ? Math.Min(1f, current + step) : Math.Max(0f, current - step);
    }
}
