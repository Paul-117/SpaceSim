using Godot;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;
using SpaceSim.Core.Power;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.GodotClient.Input;
using SpaceSim.GodotClient.Rendering;
using SpaceSim.GodotClient.UI;
using SpaceSim.GodotClient.Testing;
using SpaceSim.GodotClient.Audio;
using SpaceSim.GodotClient.Logging;
using SpaceSim.Stations;
using SpaceSim.Stations.Armarium;
using SpaceSim.Stations.Debug;
using SpaceSim.Stations.Voltarium;
using SpaceSim.Stations.Sensorium;
using NVector3 = System.Numerics.Vector3;
using NQuaternion = System.Numerics.Quaternion;

namespace SpaceSim.GodotClient;

public partial class Flight : Node
{
    private const string BridgePreferencesPath = "user://bridge_preferences.cfg";
    private const float NomadSideThrusterLeverArmMeters = 11.53f;
    private Simulation _simulation = null!;
    private readonly KeyboardShipControl _keyboard = new();
    private readonly ArenaView _arena = new();
    private readonly ShipView _ship = new();
    private readonly Dictionary<int, EnemyShipView> _enemyShips = new();
    private readonly Camera2D _camera = new();
    private readonly Starfield _stars = new();
    private readonly FlightHud _hud = new();
    private readonly ThrusterStatusPanel _thrusterPanel = new();
    private BridgeUi _bridgeUi = null!;
    private readonly StarMap _starMap = new();
    private readonly MainMenuOverlay _mainMenu = new();
    private readonly GameOverOverlay _gameOver = new();
    private readonly SoundEffects _sounds = new();
    private readonly ArmariumCommandBuffer _armariumCommands = new();
    private readonly VoltariumCommandBuffer _voltariumCommands = new();
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
    private bool _sensoriumEnabled = true;
    private bool _quickStartEnabled;
    private bool _startDuelDirect;
    private bool _duelMode;
    private bool _duelFinished;
    private EnemyShipClass _duelEnemyShipClass = EnemyShipClass.Corvette;
    private DuelShipSelection? _duelPlayerLoadout;
    private DuelShipSelection? _duelEnemyLoadout;
    private DuelAiLogger? _duelLogger;
    private BoosterConfiguration _boosterConfiguration = BoosterConfiguration.Default;

    public override void _Ready()
    {
        Engine.PhysicsTicksPerSecond = SimulationSettings.TickRate;
        _smokeTest = OS.GetCmdlineUserArgs().Contains("--smoke-test");
        _warpSmokeTest = OS.GetCmdlineUserArgs().Contains("--warp-smoke-test");
        _enemySmokeTest = OS.GetCmdlineUserArgs().Contains("--enemy-smoke-test");
        _startDuelDirect = OS.GetCmdlineUserArgs().Contains("--duel");
        _capturePath = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--capture="))?[10..];
        _quickStartEnabled = LoadQuickStartEnabled();
        _boosterConfiguration = LoadBoosterConfiguration();
        _duelMode = _startDuelDirect;
        _simulation = !_smokeTest && !_warpSmokeTest && !_enemySmokeTest && _startDuelDirect
            ? CreateDuelSimulation()
            : !_smokeTest && !_warpSmokeTest && !_enemySmokeTest && _quickStartEnabled
                ? CreateQuickStartSimulation()
                : CreateSimulation();
        _bridgeUi = GD.Load<PackedScene>("res://UI/Bridge/BridgeUI.tscn").Instantiate<BridgeUi>();
        _sensoriumEnabled = LoadSensoriumEnabled();
        _keyboard.MainThrottleRiseSeconds = _simulation.Settings.Power.BridgeMainThrottleRiseSeconds;
        _keyboard.MainThrottleFallSeconds = _simulation.Settings.Power.BridgeMainThrottleFallSeconds;
        BindWorld();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _hud.WarpMapRequested += () =>
        {
            if (!_duelMode) _pendingNavigation = new NavigationCommand(EnterHyperspace: true);
        };
        _hud.HyperspaceJumpRequested += ConfirmHyperspaceEntry;
        _bridgeUi.WarpMapRequested += () =>
        {
            if (!_duelMode) _pendingNavigation = new NavigationCommand(EnterHyperspace: true);
        };
        _starMap.JumpRequested += id => _pendingNavigation = new NavigationCommand(id);
        _mainMenu.SensoriumEnabled = _sensoriumEnabled;
        _mainMenu.SensoriumEnabledChanged += SetSensoriumEnabled;
        _mainMenu.QuickStartEnabled = _quickStartEnabled;
        _mainMenu.QuickStartEnabledChanged += SetQuickStartEnabled;
        _mainMenu.BoosterConfiguration = _boosterConfiguration;
        _mainMenu.BoosterConfigurationChanged += SetBoosterConfiguration;
        _mainMenu.StartRequested += StartFromMainMenu;
        _mainMenu.DuelRequested += StartDuel;
        _mainMenu.ResumeRequested += ResumeGame;
        _mainMenu.MainMenuRequested += ReturnToMainMenu;
        _mainMenu.QuitRequested += () => GetTree().Quit();
        _gameOver.MainMenuRequested += ReturnToMainMenu;
        _gameOver.RestartRequested += RestartCurrentMode;
        _gameOver.QuitRequested += () => GetTree().Quit();
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
        cockpit.AddChild(_bridgeUi);
        cockpit.AddChild(_starMap);
        cockpit.AddChild(_gameOver);
        cockpit.AddChild(_mainMenu);
        StartStationServer();
        _lastHyperspacePhase = _simulation.World.HyperspacePhase;
        StartDuelLogIfNeeded();
        if (!_smokeTest && !_warpSmokeTest && !_enemySmokeTest && !_quickStartEnabled && !_startDuelDirect)
            _mainMenu.ShowMain();
        if (_warpSmokeTest) _warpScenario = new WarpSmokeScenario(_simulation.World, _hud, _starMap);
        GD.Print("SpaceSim " + ProjectSettings.GetSetting("application/config/version", "2.3.0").AsString() +
            " | Core 60 Hz | Armarium, Voltarium and Sensorium station server enabled");
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
            : new Simulation(CreateGameplaySettings(startInHyperspace: true), randomSeed: Random.Shared.Next());
    }

    private Simulation CreateQuickStartSimulation()
    {
        var simulation = new Simulation(CreateGameplaySettings(startInHyperspace: true), randomSeed: Random.Shared.Next());
        simulation.Step(default, new NavigationCommand(QuickStartEncounterId: 2, QuickStartDistanceMeters: 3_000f));
        return simulation;
    }

    private Simulation CreateDuelSimulation()
    {
        var random = new Random(unchecked((int)Time.GetTicksMsec()));
        float separationBearing = (float)(random.NextDouble() * MathF.Tau);
        float playerCourse = (float)(random.NextDouble() * MathF.Tau);
        float enemyCourse = (float)(random.NextDouble() * MathF.Tau);
        ShipInitialState player = new(NVector3.Zero, CourseVector(playerCourse) * (float)(random.NextDouble() * 100d), -playerCourse);
        ShipInitialState enemy = new(CourseVector(separationBearing) * 2_000f,
            CourseVector(enemyCourse) * (float)(random.NextDouble() * 100d), -enemyCourse);
        var settings = new SimulationSettings
        {
            TargetCount = 0,
            EncounterTwoTargetCount = 0,
            EncounterThreeTargetCount = 0,
            EncounterFourTargetCount = 0,
            Power = new PowerSettings { ReactorSimulationEnabled = false },
            Hull = new HullSettings { EnableSubsystemDamage = false },
            EnemyExplosion = new EnemyExplosionSettings { Enabled = false }
        };
        ApplyBoosterConfiguration(settings, _boosterConfiguration);
        GeneratedShipLoadout? playerLoadout = _duelPlayerLoadout?.Loadout;
        GeneratedShipLoadout? enemyLoadout = _duelEnemyLoadout?.Loadout;
        return new Simulation(settings, player, random.Next(), enemyInitial: enemy, duelMode: true,
            duelAiModel: EnemyAiModel.Kestrel, playerTuning: playerLoadout?.Tuning, enemyTuning: enemyLoadout?.Tuning,
            duelBoardComputer: enemyLoadout?.BoardComputer, duelEnemyShipClass: enemyLoadout?.ShipClass ?? _duelEnemyShipClass,
            duelPlayerMassKg: playerLoadout?.Hull.MassKg, duelPlayerMaximumHull: playerLoadout?.Hull.MaximumHull,
            duelEnemyLoadout: enemyLoadout, duelEnemyName: _duelEnemyLoadout?.Name);
    }

    private static NVector3 CourseVector(float course) => new(MathF.Sin(course), 0f, -MathF.Cos(course));

    private SimulationSettings CreateGameplaySettings(bool startInHyperspace) =>
        CreateGameplaySettings(_boosterConfiguration, startInHyperspace);

    private static SimulationSettings CreateGameplaySettings(BoosterConfiguration configuration, bool startInHyperspace)
    {
        var settings = new SimulationSettings { StartInHyperspace = startInHyperspace };
        ApplyBoosterConfiguration(settings, configuration);
        return settings;
    }

    private static void ApplyBoosterConfiguration(SimulationSettings settings, BoosterConfiguration configuration)
    {
        BoosterConfiguration value = configuration.Clamp();
        settings.MainThrustNewtons = value.MainBoosterKilonewtons * 1_000f;
        settings.ReverseThrustNewtons = value.ReverseBoosterKilonewtons * 1_000f;
        settings.YawTorqueNewtonMeters = value.SideBoosterKilonewtons * 1_000f * NomadSideThrusterLeverArmMeters;
        settings.MaximumYawAngularVelocityRadiansPerSecond = value.MaximumRotationDegreesPerSecond * MathF.PI / 180f;
        settings.MaximumNominalSpeedMetersPerSecond = value.MaximumForwardSpeedMetersPerSecond;
        settings.MaximumReverseSpeedMetersPerSecond = value.MaximumReverseSpeedMetersPerSecond;
        settings.Power.BridgeMainThrottleRiseSeconds = value.MainRampUpSeconds;
    }

    private void BindWorld()
    {
        _simulation.World.RequireSensoriumConfirmationForBridgeContacts = !_duelMode && _sensoriumEnabled;
        _arena.World = _simulation.World;
        _hud.World = _simulation.World;
        _hud.Settings = _simulation.Settings;
        _thrusterPanel.World = _simulation.World;
        _bridgeUi.World = _simulation.World;
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
                ["voltarium/index.html"] = Godot.FileAccess.GetFileAsString("res://Voltarium/index.html"),
                ["voltarium/voltarium.css"] = Godot.FileAccess.GetFileAsString("res://Voltarium/voltarium.css"),
                ["voltarium/voltarium.js"] = Godot.FileAccess.GetFileAsString("res://Voltarium/voltarium.js"),
                ["sensorium/index.html"] = Godot.FileAccess.GetFileAsString("res://Sensorium/index.html"),
                ["sensorium/sensorium.css"] = Godot.FileAccess.GetFileAsString("res://Sensorium/sensorium.css"),
                ["sensorium/sensorium.js"] = Godot.FileAccess.GetFileAsString("res://Sensorium/sensorium.js"),
                ["debug/index.html"] = Godot.FileAccess.GetFileAsString("res://Debug/index.html"),
                ["debug/debug.css"] = Godot.FileAccess.GetFileAsString("res://Debug/debug.css"),
                ["debug/debug.js"] = Godot.FileAccess.GetFileAsString("res://Debug/debug.js")
            };
            _stationServer = new StationServer(new StationServerOptions(), assets, _armariumCommands, _voltariumCommands,
                _sensoriumCommands, GD.Print);
            _stationServer.Start();
            PublishArmariumState();
            _stationServer.UpdateVoltariumState(VoltariumStateBuilder.Build(_simulation.World));
            _stationServer.UpdateSensoriumState(SensoriumStateBuilder.Build(_simulation.World));
            _stationServer.UpdateEnemyDebugState(EnemyDebugStateBuilder.Build(_simulation.World, _simulation.Settings));
            GD.Print($"Armarium available at {_stationServer.ArmariumUrl}");
            GD.Print($"Voltarium available at {_stationServer.VoltariumUrl}");
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
        CompleteDuelLog("aborted");
        _stationServer?.Dispose();
        _stationServer = null;
    }

    public override void _Input(InputEvent input)
    {
        if (_gameOver.Visible) return;
        if (_mainMenu.IsOpen)
        {
            if (input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
            {
                _mainMenu.HandleEscape();
                GetViewport().SetInputAsHandled();
            }
            return;
        }
        if (_simulation.World.IsPlayerInRealSpace && !_starMap.Visible &&
            input is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Escape })
        {
            _keyboard.Clear();
            _mainMenu.ShowPause();
            GetViewport().SetInputAsHandled();
            return;
        }
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
        if (_duelFinished) return;
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
        if (_mainMenu.IsOpen) return;
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
        float? reactorLevel = _voltariumCommands.TryReadOperatingLevel(out float requestedLevel) ? requestedLevel : null;
        PowerAllocation? allocation = _voltariumCommands.TryReadAllocation(out PowerAllocation requestedAllocation)
            ? requestedAllocation : null;
        ReactorCommand reactorCommand = new(reactorLevel, allocation);
        SensoriumCommand sensoriumCommand = _sensoriumCommands.ReadCommand();
        _simulation.Step(_lastCommand, _pendingNavigation, reactorCommand,
            new SensorCommand(sensoriumCommand.ActiveSonarPing, sensoriumCommand.ConfirmedEnemyId));
        _pendingNavigation = default;
        if (_duelMode && _duelLogger is not null)
            _duelLogger.WriteSnapshot(_simulation.World, _simulation.Settings, _lastCommand, _simulation.Events);
        RecordArmariumTargetHit();
        PublishArmariumState();
        _stationServer?.UpdateVoltariumState(VoltariumStateBuilder.Build(_simulation.World));
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
        foreach (WeaponFired shot in _simulation.Events.OfType<WeaponFired>().Where(shot => shot.Owner == WeaponOwner.Player))
            _ship.PlayLanceShot(_simulation.World.LanceAim.YawOffsetDegrees);
        foreach (WeaponFired shot in _simulation.Events.OfType<WeaponFired>().Where(shot => shot.Owner == WeaponOwner.Enemy))
            PlayEnemyLanceShot(shot);
        _sounds.Update(_simulation.World, _simulation.Settings, _lastCommand, _simulation.Events);
        bool playerDestroyed = _simulation.Events.OfType<PlayerDestroyed>().Any();
        bool enemyDestroyed = _simulation.Events.OfType<EnemyDestroyed>().Any();
        if (_duelMode && (playerDestroyed || enemyDestroyed))
        {
            _starMap.Close();
            bool playerWon = enemyDestroyed && !playerDestroyed;
            _duelLogger?.WriteSnapshot(_simulation.World, _simulation.Settings, _lastCommand, _simulation.Events);
            CompleteDuelLog(playerWon ? "player_victory" : "player_destroyed");
            _duelFinished = true;
            _gameOver.ShowResult(playerWon);
        }
        else if (playerDestroyed)
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
                _gameOver.MainMenuButton.EmitSignal(Button.SignalName.Pressed);
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
                passed &= _simulation.World.Targets.Count == 0 && _hud.GetParent() is not null;
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

    private static bool LoadSensoriumEnabled()
    {
        return LoadBridgePreferences().GetValue("bridge", "sensorium_enabled", true).AsBool();
    }

    private static bool LoadQuickStartEnabled()
    {
        return LoadBridgePreferences().GetValue("bridge", "quick_start_enabled", false).AsBool();
    }

    private static BoosterConfiguration LoadBoosterConfiguration()
    {
        ConfigFile preferences = LoadBridgePreferences();
        BoosterConfiguration defaults = BoosterConfiguration.Default;
        return new BoosterConfiguration(
            preferences.GetValue("boosters", "main_power_kn", defaults.MainBoosterKilonewtons).AsSingle(),
            preferences.GetValue("boosters", "main_ramp_seconds", defaults.MainRampUpSeconds).AsSingle(),
            preferences.GetValue("boosters", "main_max_speed", defaults.MaximumForwardSpeedMetersPerSecond).AsSingle(),
            preferences.GetValue("boosters", "reverse_power_kn", defaults.ReverseBoosterKilonewtons).AsSingle(),
            preferences.GetValue("boosters", "reverse_max_speed", defaults.MaximumReverseSpeedMetersPerSecond).AsSingle(),
            preferences.GetValue("boosters", "side_power_kn", defaults.SideBoosterKilonewtons).AsSingle(),
            preferences.GetValue("boosters", "max_rotation_degrees_per_second", defaults.MaximumRotationDegreesPerSecond).AsSingle()).Clamp();
    }

    private void SetSensoriumEnabled(bool enabled)
    {
        _sensoriumEnabled = enabled;
        _simulation.World.RequireSensoriumConfirmationForBridgeContacts = enabled;
        var preferences = LoadBridgePreferences();
        preferences.SetValue("bridge", "sensorium_enabled", enabled);
        if (preferences.Save(BridgePreferencesPath) != Error.Ok)
            GD.PushWarning("Could not save bridge preferences.");
    }

    private void SetQuickStartEnabled(bool enabled)
    {
        _quickStartEnabled = enabled;
        var preferences = LoadBridgePreferences();
        preferences.SetValue("bridge", "quick_start_enabled", enabled);
        if (preferences.Save(BridgePreferencesPath) != Error.Ok)
            GD.PushWarning("Could not save bridge preferences.");
    }

    private void SetBoosterConfiguration(BoosterConfiguration configuration)
    {
        _boosterConfiguration = configuration.Clamp();
        ApplyBoosterConfiguration(_simulation.Settings, _boosterConfiguration);
        _keyboard.MainThrottleRiseSeconds = _boosterConfiguration.MainRampUpSeconds;
        var preferences = LoadBridgePreferences();
        preferences.SetValue("boosters", "main_power_kn", _boosterConfiguration.MainBoosterKilonewtons);
        preferences.SetValue("boosters", "main_ramp_seconds", _boosterConfiguration.MainRampUpSeconds);
        preferences.SetValue("boosters", "main_max_speed", _boosterConfiguration.MaximumForwardSpeedMetersPerSecond);
        preferences.SetValue("boosters", "reverse_power_kn", _boosterConfiguration.ReverseBoosterKilonewtons);
        preferences.SetValue("boosters", "reverse_max_speed", _boosterConfiguration.MaximumReverseSpeedMetersPerSecond);
        preferences.SetValue("boosters", "side_power_kn", _boosterConfiguration.SideBoosterKilonewtons);
        preferences.SetValue("boosters", "max_rotation_degrees_per_second", _boosterConfiguration.MaximumRotationDegreesPerSecond);
        if (preferences.Save(BridgePreferencesPath) != Error.Ok)
            GD.PushWarning("Could not save booster configuration.");
    }

    private static ConfigFile LoadBridgePreferences()
    {
        var preferences = new ConfigFile();
        preferences.Load(BridgePreferencesPath);
        return preferences;
    }

    private void StartFromMainMenu()
    {
        _duelMode = false;
        if (_quickStartEnabled)
        {
            RestartGame(quickStart: true);
            _mainMenu.Hide();
            return;
        }

        _mainMenu.Hide();
        _starMap.Open();
    }

    private void StartDuel(DuelShipSelection playerLoadout, DuelShipSelection enemyLoadout)
    {
        _duelPlayerLoadout = playerLoadout;
        _duelEnemyLoadout = enemyLoadout;
        _duelEnemyShipClass = enemyLoadout.Loadout.ShipClass;
        RestartGame(duel: true);
        _mainMenu.Hide();
    }

    private void ResumeGame()
    {
        _keyboard.Clear();
        _mainMenu.Hide();
    }

    private void ReturnToMainMenu()
    {
        RestartGame();
        _mainMenu.ShowMain();
    }

    private void RestartCurrentMode()
    {
        if (_duelMode)
        {
            _gameOver.Hide();
            _mainMenu.ShowDuelSelection();
            return;
        }
        RestartGame();
    }

    private void RestartGame(bool quickStart = false, bool duel = false)
    {
        CompleteDuelLog("aborted");
        _duelMode = duel;
        _duelFinished = false;
        _simulation = duel ? CreateDuelSimulation() : quickStart ? CreateQuickStartSimulation() : CreateSimulation();
        _keyboard.MainThrottleRiseSeconds = _simulation.Settings.Power.BridgeMainThrottleRiseSeconds;
        _keyboard.MainThrottleFallSeconds = _simulation.Settings.Power.BridgeMainThrottleFallSeconds;
        BindWorld();
        _previousPosition = _simulation.World.Ship.Position;
        _previousRotation = _simulation.World.Ship.Rotation;
        _lastCommand = default;
        _pendingNavigation = default;
        StartDuelLogIfNeeded();
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

    private void CompleteDuelLog(string outcome)
    {
        if (_duelLogger is null) return;
        _duelLogger.Complete(_simulation.World, outcome);
        _duelLogger = null;
    }

    private void StartDuelLogIfNeeded()
    {
        if (!_duelMode || _duelLogger is not null) return;
        EnemyAiModel model = _simulation.World.CurrentEncounter.EnemyAi?.Model ?? EnemyAiModel.Kestrel;
        _duelLogger = new DuelAiLogger(model, _duelPlayerLoadout?.Name ?? "SCHIFF 1", _duelEnemyLoadout?.Name ?? "SCHIFF 2");
        _duelLogger.WriteSnapshot(_simulation.World, _simulation.Settings, _lastCommand, Array.Empty<SimulationEvent>());
        GD.Print($"1VS1 AI log: {_duelLogger.Path}");
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
        float effectiveMainThrust = _lastCommand.MainThrust
            ? Math.Clamp(_lastCommand.MainThrustIntensity, 0f, 1f) * state.Power.MainThrusterPowerFactor *
              state.Systems.MainBoosterCondition
            : 0f;
        _ship.Refresh(_lastCommand, _visualTime, effectiveMainThrust);
        _ship.Visible = _simulation.World.IsPlayerInRealSpace;
        RefreshEnemyShips();
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
        _hud.Visible = !_simulation.World.IsPlayerInRealSpace;
        _thrusterPanel.Visible = false;
        _hud.Command = _lastCommand;
        _thrusterPanel.Command = _lastCommand;
        _bridgeUi.IsBridgeActive = _simulation.World.IsPlayerInRealSpace;
        _bridgeUi.Command = _lastCommand;
        _bridgeUi.AutopilotActive = _autopilot is not null;
        _bridgeUi.AutopilotTargetName = CurrentAutopilotTarget()?.Name ?? "-";
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

    private void RefreshEnemyShips()
    {
        HashSet<int> activeVisualEnemies = _simulation.World.VisibleEnemies
            .Where(enemy => EnemyShipView.HasVisual(enemy.ShipClass) && !enemy.IsDestroyed)
            .Select(enemy => enemy.EnemyId)
            .ToHashSet();

        foreach (int staleId in _enemyShips.Keys.Where(id => !activeVisualEnemies.Contains(id)).ToArray())
        {
            _enemyShips[staleId].QueueFree();
            _enemyShips.Remove(staleId);
        }

        foreach (EnemyShipState enemy in _simulation.World.VisibleEnemies.Where(enemy => activeVisualEnemies.Contains(enemy.EnemyId)))
        {
            if (!_enemyShips.TryGetValue(enemy.EnemyId, out EnemyShipView? view))
            {
                view = new EnemyShipView { ShipClass = enemy.ShipClass, ZIndex = 1 };
                _enemyShips.Add(enemy.EnemyId, view);
                AddChild(view);
            }

            NVector3 forward = enemy.Ship.Forward;
            view.Position = ViewSettings.Project(enemy.Ship.Position);
            view.Rotation = MathF.Atan2(forward.X, -forward.Z);
            ShipCommand command = _simulation.World.CurrentEncounter.GetEnemyAi(enemy.EnemyId)?.LastCommand ?? default;
            float mainThrust = command.MainThrust
                ? Math.Clamp(command.MainThrustIntensity, 0f, 1f) * enemy.Ship.Power.MainThrusterPowerFactor *
                  enemy.Ship.Systems.MainBoosterCondition
                : 0f;
            view.Refresh(command, mainThrust);
            view.Visible = _simulation.World.IsPlayerInRealSpace;
        }
    }

    private void PlayEnemyLanceShot(WeaponFired shot)
    {
        EnemyShipState? firingEnemy = _simulation.World.VisibleEnemies
            .Where(enemy => EnemyShipView.HasVisual(enemy.ShipClass) && !enemy.IsDestroyed)
            .OrderBy(enemy => NVector3.DistanceSquared(enemy.Ship.Position, shot.Origin))
            .FirstOrDefault();
        if (firingEnemy is not null && _enemyShips.TryGetValue(firingEnemy.EnemyId, out EnemyShipView? view))
            view.PlayLanceShot();
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
