using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.GodotClient.Input;
using SpaceSim.GodotClient.Rendering;
using SpaceSim.GodotClient.UI;
using NVector3 = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace SpaceSim.GodotClient;

public partial class Flight : Node
{
    private Simulation _simulation = null!;
    private readonly KeyboardShipControl _keyboard = new();
    private readonly ArenaView _arena = new();
    private readonly ShipView _ship = new();
    private readonly Camera2D _camera = new();
    private readonly Starfield _stars = new();
    private readonly FlightHud _hud = new();
    private NVector3 _previousPosition;
    private NQuaternion _previousRotation;
    private ShipCommand _lastCommand;
    private bool _smokeTest;
    private bool _smokeSawShot;
    private bool _smokeSawHit;
    private float _visualTime;
    private string? _capturePath;
    private bool _capturing;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = SimulationSettings.TickRate;
        _smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        _capturePath = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--capture="))?[10..];
        _simulation = _smokeTest
            ? new Simulation(new SimulationSettings { TargetCount = 1 }, initialTargets: new[] { new NVector3(0, 0, -300) })
            : new Simulation();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _arena.World = _simulation.World;
        _hud.World = _simulation.World;
        var backdrop = new CanvasLayer { Layer = -10 };
        AddChild(backdrop);
        backdrop.AddChild(_stars);
        AddChild(_arena);
        AddChild(_ship);
        _ship.ZIndex = 2;
        AddChild(_camera);
        _camera.Enabled = true;
        var cockpit = new CanvasLayer { Layer = 10 };
        AddChild(cockpit);
        cockpit.AddChild(_hud);
        GD.Print("SpaceSim 1.0 | Core 60 Hz | W/S thrust, A/D torque, Space lance");
    }

    public override void _Input(InputEvent input) => _keyboard.HandleInput(input);

    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut)
        {
            _keyboard.IsFocused = false;
            _keyboard.Clear();
        }
        else if (what == NotificationApplicationFocusIn) _keyboard.IsFocused = true;
    }

    public override void _PhysicsProcess(double delta)
    {
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _lastCommand = _smokeTest
            ? new ShipCommand(MainThrust: _simulation.World.Tick >= 181, YawLeft: _simulation.World.Tick >= 200,
                FireLance: _simulation.World.Tick == 180)
            : _keyboard.ReadCommand();
        _simulation.Step(_lastCommand);
        _arena.ShowEvents(_simulation.Events);
        if (_smokeTest)
        {
            _smokeSawShot |= _simulation.Events.OfType<WeaponFired>().Any();
            _smokeSawHit |= _simulation.Events.OfType<TargetHit>().Any();
            if (_simulation.World.Tick >= 240)
            {
                bool passed = _smokeSawShot && _smokeSawHit && _simulation.World.HitCount == 1 &&
                              _simulation.World.Ship.Velocity.Length() > 1f &&
                              _simulation.World.Ship.AngularVelocity.Y > 0f;
                GD.Print(passed ? "SMOKE PASS: scene, fixed ticks, thrust, rotation, lance, hit, respawn." : "SMOKE FAIL");
                GetTree().Quit(passed ? 0 : 1);
            }
        }
    }

    public override void _Process(double delta)
    {
        _visualTime += (float)delta;
        // Interpolation belongs to presentation. It never writes back to the core.
        float fraction = (float)Engine.GetPhysicsInterpolationFraction();
        var state = _simulation.World.Ship;
        NVector3 position = NVector3.Lerp(_previousPosition, state.Position, fraction);
        NQuaternion rotation = NQuaternion.Slerp(_previousRotation, state.Rotation, fraction);
        NVector3 forward = NVector3.Transform(-NVector3.UnitZ, rotation);
        _ship.Position = ViewSettings.Project(position);
        _ship.Rotation = MathF.Atan2(forward.X, -forward.Z);
        _ship.Refresh(_lastCommand, _simulation.World.Lance.IsReady, _visualTime);
        _camera.Position = _ship.Position;
        _stars.CameraPosition = _ship.Position;
        _arena.ShipPosition = _ship.Position;
        _arena.ShipForward = new Vector2(forward.X, forward.Z).Normalized();
        _hud.Command = _lastCommand;
        _hud.IsFocused = _keyboard.IsFocused;
        if (_capturePath is not null && !_capturing && _simulation.World.Tick >= 182)
        {
            _capturing = true;
            CaptureFrame();
        }
    }

    private async void CaptureFrame()
    {
        await ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        using var image = GetViewport().GetTexture().GetImage();
        Error result = image.SavePng(_capturePath!);
        GD.Print($"CAPTURE {result}: {_capturePath}");
        if (!_smokeTest || result != Error.Ok) GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
