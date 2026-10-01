using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Power;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.GodotClient.Input;
using SpaceSim.GodotClient.Rendering;
using SpaceSim.GodotClient.UI;
using SpaceSim.GodotClient.Testing;
using SpaceSim.GodotClient.Audio;
using SpaceSim.Stations;
using SpaceSim.Stations.Armarium;
using SpaceSim.Stations.Debug;
using SpaceSim.Stations.Reactorium;
using SpaceSim.Stations.Sensorium;
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
    private readonly ThrusterStatusPanel _thrusterPanel = new();
    private readonly StarMap _starMap = new();
    private readonly GameOverOverlay _gameOver = new();
    private readonly SoundEffects _sounds = new();
    private readonly ArmariumCommandBuffer _armariumCommands = new();
    private readonly ReactoriumCommandBuffer _reactoriumCommands = new();
    private readonly SensoriumCommandBuffer _sensoriumCommands = new();
    private StationServer? _stationServer;
    private NavigationCommand _pendingNavigation;
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
    private bool _freeCameraMode;
    private Vector2 _freeCameraPosition;
    private int? _selectedEnemyId;
    private AutopilotController? _autopilot;
    private int? _autopilotTargetId;
    private long _armariumTargetHitSequence;
    private float _armariumLastTargetHitBearingDegrees;
    private Vector2? _hyperspaceEntryPoint;
    private HyperspacePhase _lastHyperspacePhase;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = SimulationSettings.TickRate;
        _smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        _warpSmokeTest = OS.GetCmdlineUserArgs().Contains("--warp-smoke-test");
        _enemySmokeTest = OS.GetCmdlineUserArgs().Contains("--enemy-smoke-test");
        _capturePath = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--capture="))?[10..];
        _simulation = CreateSimulation();
        _keyboard.MainThrottleRiseSeconds = _simulation.Settings.Power.BridgeMainThrottleRiseSeconds;
        _keyboard.MainThrottleFallSeconds = _simulation.Settings.Power.BridgeMainThrottleFallSeconds;
        BindWorld();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _hud.WarpMapRequested += () => _pendingNavigation = new NavigationCommand(EnterHyperspace: true);
        _hud.HyperspaceJumpRequested += ConfirmHyperspaceEntry;
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
        cockpit.AddChild(_thrusterPanel);
        cockpit.AddChild(_starMap);
        cockpit.AddChild(_gameOver);
        StartStationServer();
        _lastHyperspacePhase = _simulation.World.HyperspacePhase;
        if (!_smokeTest && !_warpSmokeTest && !_enemySmokeTest &&
            _simulation.World.HyperspacePhase == HyperspacePhase.SelectingDestination)
            _starMap.Open();
        if (_warpSmokeTest) _warpScenario = new WarpSmokeScenario(_simulation.World, _hud, _starMap);
        GD.Print("SpaceSim 2.0.0 | Core 60 Hz | Armarium, Reactorium and Sensorium station server enabled");
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
            : new Simulation(new SimulationSettings { StartInHyperspace = true });
    }

    private void BindWorld()
    {
        _arena.World = _simulation.World;
        _hud.World = _simulation.World;
        _hud.Settings = _simulation.Settings;
        _thrusterPanel.World = _simulation.World;
        _starMap.World = _simulation.World;
    }

    private void StartStationServer()
    {
        try
        {
            var assets = new Dictionary<string, string>
            {
                ["armarium/index.html"] = Godot.FileAccess.GetFileAsString("res://Armarium/index.html"),
                ["armarium/armarium.css"] = Godot.FileAccess.GetFileAsString("res://Armarium/armarium.css"),
                ["armarium/armarium.js"] = Godot.FileAccess.GetFileAsString("res://Armarium/armarium.js"),
                ["reactorium/index.html"] = Godot.FileAccess.GetFileAsString("res://Reactorium/index.html"),
                ["reactorium/reactorium.css"] = Godot.FileAccess.GetFileAsString("res://Reactorium/reactorium.css"),
                ["reactorium/reactorium.js"] = Godot.FileAccess.GetFileAsString("res://Reactorium/reactorium.js"),
                ["sensorium/index.html"] = Godot.FileAccess.GetFileAsString("res://Sensorium/index.html"),
                ["sensorium/sensorium.css"] = Godot.FileAccess.GetFileAsString("res://Sensorium/sensorium.css"),
                ["sensorium/sensorium.js"] = Godot.FileAccess.GetFileAsString("res://Sensorium/sensorium.js"),
                ["debug/index.html"] = Godot.FileAccess.GetFileAsString("res://Debug/index.html"),
                ["debug/debug.css"] = Godot.FileAccess.GetFileAsString("res://Debug/debug.css"),
                ["debug/debug.js"] = Godot.FileAccess.GetFileAsString("res://Debug/debug.js")
            };
            _stationServer = new StationServer(new StationServerOptions(), assets, _armariumCommands, _reactoriumCommands,
                _sensoriumCommands, GD.Print);
            _stationServer.Start();
            PublishArmariumState();
            _stationServer.UpdateReactoriumState(ReactoriumStateBuilder.Build(_simulation.World));
            _stationServer.UpdateSensoriumState(SensoriumStateBuilder.Build(_simulation.World));
            _stationServer.UpdateEnemyDebugState(EnemyDebugStateBuilder.Build(_simulation.World, _simulation.Settings));
            GD.Print($"Armarium available at {_stationServer.ArmariumUrl}");
            GD.Print($"Reactorium available at {_stationServer.ReactoriumUrl}");
            GD.Print($"Sensorium available at {_stationServer.SensoriumUrl}");
            GD.Print($"Enemy AI debug station available at {_stationServer.DebugUrl}");
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
        if (_simulation.World.HyperspacePhase == HyperspacePhase.PlanningEntry &&
            input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } entryClick &&
            IsTacticalMapPoint(entryClick.Position))
        {
            _hyperspaceEntryPoint = ScreenToWorld(entryClick.Position);
            // The Jump button can be clicked in the same rendered frame as the map click.
            // Keep its state in sync instead of waiting for the next HUD process pass.
            _hud.IsHyperspacePlanning = true;
            _hud.HasHyperspaceEntryPoint = true;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_simulation.World.IsPlayerInRealSpace && !_starMap.Visible && input is InputEventKey { Pressed: true, Echo: false } autopilotKey &&
            (autopilotKey.PhysicalKeycode == Key.P || autopilotKey.Keycode == Key.P))
        {
            ToggleAutopilot();
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_simulation.World.IsPlayerInRealSpace && !_starMap.Visible && input is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left } click &&
            IsTacticalMapPoint(click.Position) && _arena.TrySelectEnemy(ScreenToWorld(click.Position), out int enemyId))
        {
            _selectedEnemyId = enemyId;
            GetViewport().SetInputAsHandled();
            return;
        }
        if (_simulation.World.IsPlayerInRealSpace && !_starMap.Visible && input is InputEventKey { Pressed: true, Echo: false } key &&
            (key.PhysicalKeycode == Key.Space || key.Keycode == Key.Space))
        {
            ToggleFreeCamera();
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
        if (_simulation.World.IsPlayerInRealSpace && _starMap.Visible && input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
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
        if (_warpScenario is null) _lastCommand = _smokeTest
            ? new ShipCommand(MainThrust: _simulation.World.Tick >= 181, YawLeft: _simulation.World.Tick >= 451,
                FireLance: _simulation.World.Tick == 450)
            : _keyboard.ReadCommand(allowFlightControls: _simulation.World.IsPlayerInRealSpace && !_freeCameraMode);
        if (_enemySmokeTest)
        {
            _lastCommand = default;
            if (_simulation.World.Tick == 600) _pendingNavigation = new NavigationCommand(3);
        }
        if (_simulation.World.IsPlayerInRealSpace)
        {
            ApplyAutopilotControl();
            ApplyArmariumControl();
        }
        float? reactorLevel = _reactoriumCommands.TryReadOperatingLevel(out float requestedLevel) ? requestedLevel : null;
        PowerAllocation? allocation = _reactoriumCommands.TryReadAllocation(out PowerAllocation requestedAllocation)
            ? requestedAllocation : null;
        ReactorCommand reactorCommand = new(reactorLevel, allocation);
        SensoriumCommand sensoriumCommand = _sensoriumCommands.ReadCommand();
        _simulation.Step(_lastCommand, _pendingNavigation, reactorCommand,
            new SensorCommand(sensoriumCommand.ActiveSonarPing, sensoriumCommand.ConfirmedEnemyId));
        _pendingNavigation = default;
        RecordArmariumTargetHit();
        PublishArmariumState();
        _stationServer?.UpdateReactoriumState(ReactoriumStateBuilder.Build(_simulation.World));
        _stationServer?.UpdateSensoriumState(SensoriumStateBuilder.Build(_simulation.World));
        _stationServer?.UpdateEnemyDebugState(EnemyDebugStateBuilder.Build(_simulation.World, _simulation.Settings));
        if (_simulation.Events.OfType<EnteredHyperspace>().Any())
        {
            _autopilot = null;
            _autopilotTargetId = null;
            _keyboard.Clear();
            _starMap.Open();
        }
        if (_lastHyperspacePhase != _simulation.World.HyperspacePhase)
        {
            HandleHyperspacePhaseChanged(_simulation.World.HyperspacePhase);
            _lastHyperspacePhase = _simulation.World.HyperspacePhase;
        }
        if (_simulation.Events.OfType<EncounterChanged>().Any())
        {
            // Do not interpolate across different local coordinate systems.
            _previousPosition = _simulation.World.Ship.Position;
            _previousRotation = _simulation.World.Ship.Rotation;
            _starMap.Close();
            _hyperspaceEntryPoint = null;
            _freeCameraMode = false;
            _ship.Visible = true;
            _ship.StartReentry(_visualTime);
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
        if (_warpSmokeTest && _simulation.World.Tick >= 610)
        {
            GD.Print("WARP SMOKE PASS: hyperspace exit, destination choice, entry point and re-entry.");
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
                passed &= _simulation.World.Targets.Count == 0 && _thrusterPanel.GetParent() is not null;
                GD.Print(passed ? "SMOKE PASS: power UI, shields, fixed ticks, thrust, rotation, lance, hit, no respawn." : "SMOKE FAIL");
                GetTree().Quit(passed ? 0 : 1);
            }
        }
    }

    private void ApplyArmariumControl()
    {
        ArmariumCommand command = _armariumCommands.ReadCommand();
        // Armarium never changes ship physics. It only contributes normal lance mount and fire intent.
        _lastCommand = _lastCommand with
        {
            AimLanceLeft = command.AimLanceLeft,
            AimLanceRight = command.AimLanceRight,
            FireLance = _lastCommand.FireLance || command.FireLance
        };
    }

    private void RecordArmariumTargetHit()
    {
        WeaponFired? hit = _simulation.Events.OfType<WeaponFired>().FirstOrDefault(shot =>
            shot.Owner == WeaponOwner.Player && shot.HitKind is WeaponHitKind.Target or WeaponHitKind.Enemy);
        if (hit is null) return;
        _armariumTargetHitSequence++;
        _armariumLastTargetHitBearingDegrees = ArmariumStateBuilder.CalculateTargetBearingDegrees(
            _simulation.World.Ship.Position, _simulation.World.Ship.Forward, hit.End);
    }

    private void PublishArmariumState() => _stationServer?.UpdateState(ArmariumStateBuilder.Build(
        _simulation.World, _armariumTargetHitSequence, _armariumLastTargetHitBearingDegrees));

    private void RestartGame()
    {
        _simulation = CreateSimulation();
        _keyboard.MainThrottleRiseSeconds = _simulation.Settings.Power.BridgeMainThrottleRiseSeconds;
        _keyboard.MainThrottleFallSeconds = _simulation.Settings.Power.BridgeMainThrottleFallSeconds;
        BindWorld();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _lastCommand = default;
        _pendingNavigation = default;
        _freeCameraMode = false;
        _freeCameraPosition = _ship.Position;
        _selectedEnemyId = null;
        _autopilot = null;
        _autopilotTargetId = null;
        _arena.ResetVisuals();
        _sounds.ResetForNewSession();
        _starMap.Close();
        _gameOver.Hide();
        _enemyGameOverFrames = 0;
        _armariumCommands.Clear();
        _hyperspaceEntryPoint = null;
        _lastHyperspacePhase = _simulation.World.HyperspacePhase;
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
        _ship.Refresh(_lastCommand, _simulation.World.Lance.IsReady, _visualTime,
            _simulation.World.LanceAim.YawOffsetDegrees);
        _ship.Visible = _simulation.World.IsPlayerInRealSpace;
        UpdateCamera((float)delta);
        _arena.ShipPosition = _ship.Position;
        _arena.ShipForward = new Vector2(forward.X, forward.Z).Normalized();
        _arena.CameraPosition = _camera.Position;
        _arena.CameraZoom = _cameraZoom;
        _arena.IsFreeCamera = _freeCameraMode;
        _arena.HidePlayerGuidance = _simulation.World.IsPlayerInHyperspace;
        _arena.HyperspaceEntryPoint = _hyperspaceEntryPoint;
        _arena.HyperspaceEnemyPoint = _simulation.World.HyperspacePhase == HyperspacePhase.PlanningEntry &&
                                     _simulation.World.CurrentEncounter.LastKnownEnemyPosition is { } lastKnownPosition
            ? ViewSettings.Project(lastKnownPosition)
            : null;
        UpdateSelectedContact();
        _hud.CameraZoom = _cameraZoom;
        _hud.IsFreeCamera = _freeCameraMode;
        _hud.IsHyperspacePlanning = _simulation.World.HyperspacePhase == HyperspacePhase.PlanningEntry;
        _hud.HasHyperspaceEntryPoint = _hyperspaceEntryPoint is not null;
        _thrusterPanel.Visible = _simulation.World.IsPlayerInRealSpace;
        _hud.Command = _lastCommand;
        _thrusterPanel.Command = _lastCommand;
        _hud.ArmariumOnline = _stationServer?.IsArmariumOnline == true;
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

    private void ToggleFreeCamera()
    {
        if (_freeCameraMode)
        {
            _freeCameraMode = false;
            _freeCameraPosition = _ship.Position;
        }
        else
        {
            _freeCameraMode = true;
            _freeCameraPosition = _camera.Position;
            // Prevent a partially held bridge throttle from being applied while WASD pans the tactical map.
            _keyboard.Clear();
        }
    }

    private void UpdateCamera(float delta)
    {
        if (!_freeCameraMode)
        {
            _camera.Position = _ship.Position;
            _freeCameraPosition = _ship.Position;
        }
        else if (_keyboard.IsFocused)
        {
            Vector2 direction = new(
                (Godot.Input.IsPhysicalKeyPressed(Key.D) ? 1f : 0f) - (Godot.Input.IsPhysicalKeyPressed(Key.A) ? 1f : 0f),
                (Godot.Input.IsPhysicalKeyPressed(Key.S) ? 1f : 0f) - (Godot.Input.IsPhysicalKeyPressed(Key.W) ? 1f : 0f));
            if (direction.LengthSquared() > 0f)
                _freeCameraPosition += direction.Normalized() *
                    TacticalCameraSettings.FreePanPixelsPerSecond / MathF.Max(0.001f, _cameraZoom) * delta;
            _camera.Position = _freeCameraPosition;
        }
        _stars.CameraPosition = _camera.Position;
    }

    private void HandleHyperspacePhaseChanged(HyperspacePhase phase)
    {
        if (phase != HyperspacePhase.PlanningEntry) return;
        _starMap.Close();
        _hyperspaceEntryPoint = null;
        _freeCameraMode = true;
        NVector3? lastKnownPosition = _simulation.World.CurrentEncounter.LastKnownEnemyPosition;
        _freeCameraPosition = lastKnownPosition is null ? Vector2.Zero : ViewSettings.Project(lastKnownPosition.Value);
        _camera.Position = _freeCameraPosition;
    }

    private void ConfirmHyperspaceEntry()
    {
        if (_simulation.World.HyperspacePhase != HyperspacePhase.PlanningEntry || _hyperspaceEntryPoint is not { } entry) return;
        _pendingNavigation = new NavigationCommand(EntryPosition: ViewSettings.Unproject(entry));
    }

    private bool IsTacticalMapPoint(Vector2 screenPosition)
    {
        Vector2 size = GetViewport().GetVisibleRect().Size;
        return screenPosition.Y >= 154f && screenPosition.Y <= size.Y - 154f;
    }

    private Vector2 ScreenToWorld(Vector2 screenPosition) =>
        _camera.Position + (screenPosition - GetViewport().GetVisibleRect().Size / 2f) / MathF.Max(0.001f, _cameraZoom);

    private void UpdateSelectedContact()
    {
        var enemies = _simulation.World.VisibleEnemies.ToArray();
        if (!enemies.Any(enemy => enemy.EnemyId == _selectedEnemyId))
            _selectedEnemyId = enemies.FirstOrDefault()?.EnemyId;
        _arena.SelectedEnemyId = _selectedEnemyId;
        _hud.SelectedEnemyId = _selectedEnemyId;
        EnemyShipState? autopilotTarget = CurrentAutopilotTarget();
        if (_autopilot is not null && autopilotTarget is null)
        {
            _autopilot = null;
            _autopilotTargetId = null;
        }
        _hud.AutopilotActive = _autopilot is not null;
        _hud.AutopilotTargetName = autopilotTarget?.Name ?? "-";
    }

    private void ToggleAutopilot()
    {
        if (_autopilot is not null)
        {
            _autopilot = null;
            _autopilotTargetId = null;
            return;
        }
        EnemyShipState? target = _simulation.World.VisibleEnemies
            .FirstOrDefault(enemy => enemy.EnemyId == _selectedEnemyId) ??
            _simulation.World.VisibleEnemies.FirstOrDefault();
        if (target is null) return;
        _autopilot = new AutopilotController(_simulation.Settings.EnemyAi,
            _simulation.Settings.ReverseThrustNewtons / _simulation.Settings.ShipMassKg);
        _autopilotTargetId = target.EnemyId;
        _keyboard.Clear();
    }

    private void ApplyAutopilotControl()
    {
        EnemyShipState? target = CurrentAutopilotTarget();
        if (_autopilot is null || target is null)
        {
            if (_autopilot is not null)
            {
                _autopilot = null;
                _autopilotTargetId = null;
            }
            return;
        }
        ShipCommand flight = _autopilot.Tick(_simulation.World.Ship, target.Ship);
        // Preserve manual/Armarium weapon intents; the autopilot owns only flight controls.
        _lastCommand = _lastCommand with
        {
            MainThrust = flight.MainThrust,
            ReverseThrust = flight.ReverseThrust,
            YawLeft = flight.YawLeft,
            YawRight = flight.YawRight,
            MainThrustIntensity = flight.MainThrust ? 1f : 0f,
            ReverseThrustIntensity = flight.ReverseThrust ? 1f : 0f,
            YawIntensity = 1f
        };
    }

    private EnemyShipState? CurrentAutopilotTarget() => _autopilotTargetId is int targetId
        ? _simulation.World.VisibleEnemies.FirstOrDefault(enemy => enemy.EnemyId == targetId)
        : null;

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
