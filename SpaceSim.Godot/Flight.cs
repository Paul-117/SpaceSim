using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Power;
using SpaceSim.GodotClient.Input;
using SpaceSim.GodotClient.Rendering;
using SpaceSim.GodotClient.UI;
using SpaceSim.GodotClient.Testing;
using SpaceSim.GodotClient.Audio;
using SpaceSim.Stations;
using SpaceSim.Stations.Armarium;
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
    private readonly PowerDistributionPanel _powerPanel = new();
    private readonly StarMap _starMap = new();
    private readonly GameOverOverlay _gameOver = new();
    private readonly SoundEffects _sounds = new();
    private const float ArmariumYawIntensity = 0.5f;
    private readonly ArmariumCommandBuffer _armariumCommands = new();
    private StationServer? _stationServer;
    private bool _armariumControlsActive;
    private NavigationCommand _pendingNavigation;
    private PowerAllocationCommand _pendingPower;
    private NVector3 _previousPosition;
    private NQuaternion _previousRotation;
    private ShipCommand _lastCommand;
    private bool _smokeTest;
    private bool _warpSmokeTest;
    private bool _enemySmokeTest;
    private int _enemyGameOverFrames;
    private bool _enemySmokeRestarted;
    private WarpSmokeScenario? _warpScenario;
    private bool _smokeSawShot;
    private bool _smokeSawHit;
    private bool _powerSmokeAdjusted;
    private float _visualTime;
    private string? _capturePath;
    private bool _capturing;
    private float _cameraZoom = 1f;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = SimulationSettings.TickRate;
        _smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        _warpSmokeTest = OS.GetCmdlineUserArgs().Contains("--warp-smoke-test");
        _enemySmokeTest = OS.GetCmdlineUserArgs().Contains("--enemy-smoke-test");
        _capturePath = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--capture="))?[10..];
        _simulation = CreateSimulation();
        BindWorld();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _hud.WarpMapRequested += () => _starMap.Open();
        _powerPanel.AdjustmentRequested += command => _pendingPower = _pendingPower.Combine(command);
        _starMap.JumpRequested += id => _pendingNavigation = new NavigationCommand(id);
        _gameOver.RestartRequested += RestartGame;
        var backdrop = new CanvasLayer { Layer = -10 };
        AddChild(backdrop);
        backdrop.AddChild(_stars);
        AddChild(_arena);
        AddChild(_ship);
        _ship.ZIndex = 2;
        AddChild(_camera);
        _camera.Enabled = true;
        AddChild(_sounds);
        var cockpit = new CanvasLayer { Layer = 10 };
        AddChild(cockpit);
        cockpit.AddChild(_hud);
        cockpit.AddChild(_powerPanel);
        cockpit.AddChild(_starMap);
        cockpit.AddChild(_gameOver);
        StartStationServer();
        if (_warpSmokeTest) _warpScenario = new WarpSmokeScenario(_simulation.World, _hud, _starMap);
        GD.Print("SpaceSim 1.7.1 | Core 60 Hz | Armarium fine-control station server enabled");
    }

    private Simulation CreateSimulation()
    {
        if (_enemySmokeTest)
            return new Simulation(new SimulationSettings { TargetCount = 0, EncounterTwoTargetCount = 0 },
                enemyInitial: new ShipInitialState(new NVector3(900, 0, 0), YawRadians: MathF.PI / 2));
        int testTargetCount = _warpSmokeTest ? 10 : 1;
        return _smokeTest || _warpSmokeTest
            ? new Simulation(new SimulationSettings { TargetCount = testTargetCount },
                initialTargets: Enumerable.Range(0, testTargetCount).Select(i =>
                    i == 0 ? new NVector3(0, 0, -300) : new NVector3(200 + 40 * i, 0, 200)), spawnEnemy: false)
            : new Simulation();
    }

    private void BindWorld()
    {
        _arena.World = _simulation.World;
        _hud.World = _simulation.World;
        _hud.Settings = _simulation.Settings;
        _powerPanel.World = _simulation.World;
        _starMap.World = _simulation.World;
    }

    private void StartStationServer()
    {
        try
        {
            var assets = new Dictionary<string, string>
            {
                ["index.html"] = Godot.FileAccess.GetFileAsString("res://Armarium/index.html"),
                ["armarium.css"] = Godot.FileAccess.GetFileAsString("res://Armarium/armarium.css"),
                ["armarium.js"] = Godot.FileAccess.GetFileAsString("res://Armarium/armarium.js")
            };
            _stationServer = new StationServer(new StationServerOptions(), assets, _armariumCommands, GD.Print);
            _stationServer.Start();
            _stationServer.UpdateState(ArmariumStateBuilder.Build(_simulation.World, _armariumControlsActive));
            GD.Print($"Armarium available at {_stationServer.ArmariumUrl}");
        }
        catch (Exception exception)
        {
            GD.PushWarning($"Armarium station server did not start: {exception.Message}");
        }
    }

    public override void _ExitTree()
    {
        _stationServer?.Dispose();
        _stationServer = null;
    }

    public override void _Input(InputEvent input)
    {
        if (_gameOver.Visible) return;
        if (input is InputEventKey { Pressed: true, Echo: false } controlKey &&
            (controlKey.PhysicalKeycode == Key.T || controlKey.Keycode == Key.T))
        {
            _armariumControlsActive = !_armariumControlsActive;
            _armariumCommands.Clear();
            _stationServer?.UpdateState(ArmariumStateBuilder.Build(_simulation.World, _armariumControlsActive));
            GD.Print(_armariumControlsActive ? "Armarium fine control active." : "Bridge flight control active.");
            GetViewport().SetInputAsHandled();
            return;
        }
        if (input is InputEventMouseButton { Pressed: true } mouse &&
            (mouse.ButtonIndex == MouseButton.WheelUp || mouse.ButtonIndex == MouseButton.WheelDown))
        {
            float direction = mouse.ButtonIndex == MouseButton.WheelUp ? 1f : -1f;
            float candidate = _cameraZoom * Mathf.Pow(TacticalCameraSettings.MouseWheelZoomFactor, direction);
            // Keep Camera2D input finite, without imposing a design-time zoom limit.
            if (float.IsFinite(candidate) && candidate > 0f)
            {
                _cameraZoom = candidate;
                _camera.Zoom = Vector2.One * _cameraZoom;
            }
            GetViewport().SetInputAsHandled();
            return;
        }
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
        if (_enemySmokeRestarted)
        {
            bool passed = _simulation.World.GameState == SpaceSim.Core.Combat.GameState.Running &&
                          _simulation.World.CurrentEncounter.Id == 1 && !_gameOver.Visible;
            GD.Print(passed
                ? "ENEMY SMOKE PASS: AI lance, Game Over overlay, frozen simulation and restart."
                : "ENEMY SMOKE FAIL: restart state is invalid.");
            GetTree().Quit(passed ? 0 : 1);
            return;
        }
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
        if (_smokeTest)
        {
            if (_simulation.World.Tick == 0) _powerPanel.PropulsionMinus.EmitSignal(Button.SignalName.Pressed);
            if (_simulation.World.Tick == 1) _powerPanel.WeaponsPlus.EmitSignal(Button.SignalName.Pressed);
            if (_simulation.World.Tick == 3)
            {
                var power = _simulation.World.Ship.Power;
                _powerSmokeAdjusted = power.PropulsionAllocation == 30f && power.WeaponsAllocation == 40f &&
                                      power.ShieldsAllocation == 30f;
            }
        }
        if (_warpScenario is null) _lastCommand = _smokeTest
            ? new ShipCommand(MainThrust: _simulation.World.Tick >= 181, YawLeft: _simulation.World.Tick >= 451,
                FireLance: _simulation.World.Tick == 450)
            : _keyboard.ReadCommand();
        if (_enemySmokeTest)
        {
            _lastCommand = default;
            if (_simulation.World.Tick == 600) _pendingNavigation = new NavigationCommand(3);
        }
        ApplyArmariumControl();
        _simulation.Step(_lastCommand, _pendingNavigation, _pendingPower);
        _pendingNavigation = default;
        _pendingPower = default;
        _stationServer?.UpdateState(ArmariumStateBuilder.Build(_simulation.World, _armariumControlsActive));
        if (_simulation.Events.OfType<EncounterChanged>().Any())
        {
            // Do not interpolate across different local coordinate systems.
            _previousPosition = _simulation.World.Ship.Position;
            _previousRotation = _simulation.World.Ship.Rotation;
            _starMap.Close();
        }
        _arena.ShowEvents(_simulation.Events);
        _sounds.Update(_simulation.World, _simulation.Settings, _lastCommand, _simulation.Events);
        if (_simulation.Events.OfType<PlayerDestroyed>().Any())
        {
            _starMap.Close();
            _gameOver.Show();
        }
        if (_enemySmokeTest && _simulation.World.GameState == SpaceSim.Core.Combat.GameState.GameOver)
        {
            _enemyGameOverFrames++;
            if (_enemyGameOverFrames >= 20)
            {
                if (!_gameOver.Visible)
                {
                    GD.Print("ENEMY SMOKE FAIL: Game Over overlay is hidden.");
                    GetTree().Quit(1);
                    return;
                }
                _gameOver.RestartButton.EmitSignal(Button.SignalName.Pressed);
            }
        }
        if (_warpSmokeTest && _simulation.World.Tick >= 1205)
        {
            GD.Print("WARP SMOKE PASS: mouse controls, live map, charge gate, jumps both ways, persistent targets.");
            GetTree().Quit();
        }
        if (_smokeTest)
        {
            _smokeSawShot |= _simulation.Events.OfType<WeaponFired>().Any();
            _smokeSawHit |= _simulation.Events.OfType<TargetHit>().Any();
            if (_simulation.World.Tick >= 510)
            {
                bool passed = _smokeSawShot && _smokeSawHit && _simulation.World.HitCount == 1 &&
                              _simulation.World.Ship.Velocity.Length() > 1f &&
                              _simulation.World.Ship.AngularVelocity.Y > 0f;
                passed &= _simulation.World.Targets.Count == 0 && _powerSmokeAdjusted && _powerPanel.GetChildCount() > 0;
                GD.Print(passed ? "SMOKE PASS: power UI, shields, fixed ticks, thrust, rotation, lance, hit, no respawn." : "SMOKE FAIL");
                GetTree().Quit(passed ? 0 : 1);
            }
        }
    }

    private void ApplyArmariumControl()
    {
        if (!_armariumControlsActive)
        {
            _armariumCommands.Clear();
            return;
        }

        ArmariumCommand command = _armariumCommands.ReadCommand();
        // The bridge still owns thrust, reverse thrust and its local lance key. Only yaw is delegated.
        _lastCommand = _lastCommand with
        {
            YawLeft = command.YawLeft,
            YawRight = command.YawRight,
            YawIntensity = ArmariumYawIntensity,
            FireLance = _lastCommand.FireLance || command.FireLance
        };
    }

    private void RestartGame()
    {
        _simulation = CreateSimulation();
        BindWorld();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _lastCommand = default;
        _pendingNavigation = default;
        _pendingPower = default;
        _arena.ResetVisuals();
        _sounds.ResetForNewSession();
        _starMap.Close();
        _gameOver.Hide();
        _enemyGameOverFrames = 0;
        _powerSmokeAdjusted = false;
        _armariumCommands.Clear();
        _armariumControlsActive = false;
        if (_enemySmokeTest) _enemySmokeRestarted = true;
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
        _arena.CameraZoom = _cameraZoom;
        _hud.CameraZoom = _cameraZoom;
        _hud.Command = _lastCommand;
        _hud.ArmariumControlsActive = _armariumControlsActive;
        _hud.IsFocused = _keyboard.IsFocused;
        bool captureReady = _enemySmokeTest
            ? _simulation.World.GameState == SpaceSim.Core.Combat.GameState.GameOver
            : _simulation.World.Tick >= (_warpSmokeTest ? 580 : 452);
        if (_capturePath is not null && !_capturing && captureReady)
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
        if ((!_smokeTest && !_warpSmokeTest && !_enemySmokeTest) || result != Error.Ok)
            GetTree().Quit(result == Error.Ok ? 0 : 1);
    }
}
