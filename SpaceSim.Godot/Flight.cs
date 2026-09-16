using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;
using SpaceSim.GodotClient.Input;
using SpaceSim.GodotClient.Rendering;
using SpaceSim.GodotClient.UI;
using SpaceSim.GodotClient.Testing;
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
    private readonly StarMap _starMap = new();
    private NavigationCommand _pendingNavigation;
    private NVector3 _previousPosition;
    private NQuaternion _previousRotation;
    private ShipCommand _lastCommand;
    private bool _smokeTest;
    private bool _warpSmokeTest;
    private WarpSmokeScenario? _warpScenario;
    private bool _smokeSawShot;
    private bool _smokeSawHit;
    private float _visualTime;
    private string? _capturePath;
    private bool _capturing;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = SimulationSettings.TickRate;
        _smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        _warpSmokeTest = OS.GetCmdlineUserArgs().Contains("--warp-smoke-test");
        _capturePath = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--capture="))?[10..];
        int testTargetCount = _warpSmokeTest ? 10 : 1;
        _simulation = _smokeTest || _warpSmokeTest
            ? new Simulation(new SimulationSettings { TargetCount = testTargetCount },
                initialTargets: Enumerable.Range(0, testTargetCount).Select(i =>
                    i == 0 ? new NVector3(0, 0, -300) : new NVector3(200 + 40 * i, 0, 200)))
            : new Simulation();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _arena.World = _simulation.World;
        _hud.World = _simulation.World;
        _starMap.World = _simulation.World;
        _hud.WarpMapRequested += () => _starMap.Open();
        _starMap.JumpRequested += id => _pendingNavigation = new NavigationCommand(id);
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
        cockpit.AddChild(_starMap);
        if (_warpSmokeTest) _warpScenario = new WarpSmokeScenario(_simulation.World, _hud, _starMap);
        GD.Print("SpaceSim 1.1 | Core 60 Hz | W/S thrust, A/D torque, Space lance | Warp Drive: star map");
    }

    public override void _Input(InputEvent input)
    {
        if (_starMap.Visible && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _starMap.Close();
            GetViewport().SetInputAsHandled();
        }
        else _keyboard.HandleInput(input);
    }

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
        if (_warpScenario is not null)
        {
            try { _lastCommand = _warpScenario.BeforeTick(); }
            catch (Exception exception)
            {
                GD.Print($"WARP SMOKE FAIL at tick {_simulation.World.Tick}: {exception.Message}");
                SetPhysicsProcess(false);
                GetTree().Quit(1);
                return;
            }
        }
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        if (_warpScenario is null) _lastCommand = _smokeTest
            ? new ShipCommand(MainThrust: _simulation.World.Tick >= 181, YawLeft: _simulation.World.Tick >= 200,
                FireLance: _simulation.World.Tick == 180)
            : _keyboard.ReadCommand();
        _simulation.Step(_lastCommand, _pendingNavigation);
        _pendingNavigation = default;
        if (_simulation.Events.OfType<EncounterChanged>().Any())
        {
            // Do not interpolate across different local coordinate systems.
            _previousPosition = _simulation.World.Ship.Position;
            _previousRotation = _simulation.World.Ship.Rotation;
            _starMap.Close();
        }
        _arena.ShowEvents(_simulation.Events);
        if (_warpSmokeTest && _simulation.World.Tick >= 1205)
        {
            GD.Print("WARP SMOKE PASS: mouse controls, live map, charge gate, jumps both ways, persistent targets.");
            GetTree().Quit();
        }
        if (_smokeTest)
        {
            _smokeSawShot |= _simulation.Events.OfType<WeaponFired>().Any();
            _smokeSawHit |= _simulation.Events.OfType<TargetHit>().Any();
            if (_simulation.World.Tick >= 240)
            {
                bool passed = _smokeSawShot && _smokeSawHit && _simulation.World.HitCount == 1 &&
                              _simulation.World.Ship.Velocity.Length() > 1f &&
                              _simulation.World.Ship.AngularVelocity.Y > 0f;
                passed &= _simulation.World.Targets.Count == 0;
                GD.Print(passed ? "SMOKE PASS: scene, fixed ticks, thrust, rotation, lance, hit, no respawn." : "SMOKE FAIL");
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
        if (_capturePath is not null && !_capturing && _simulation.World.Tick >= (_warpSmokeTest ? 580 : 182))
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
        if ((!_smokeTest && !_warpSmokeTest) || result != Error.Ok) GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
