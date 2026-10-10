using System.Numerics;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.AI;

/// <summary>
/// Deterministic combat FSM. It only emits regular ShipCommands; the shared ship
/// simulation applies all resulting acceleration, rotation and weapon rules.
/// </summary>
public sealed class EnemyAiController
{
    private readonly EnemyAiSettings _settings;
    private readonly float _nominalReverseAcceleration;
    private readonly EnemyAiModel _model;
    private readonly float _aegisOrbitSign;
    private readonly BoardComputerCommandProcessor _boardComputer;
    private readonly ShipSubclass _subclass;

    public EnemyAiState CurrentState { get; private set; } = EnemyAiState.Acquire;
    public float TimeInState { get; private set; }
    public EnemyAiContext LastContext { get; private set; }
    public ShipCommand LastCommand { get; private set; }
    public EnemyDifficulty Difficulty { get; }
    public EnemyAiModel Model => _model;
    public BoardComputerProfile BoardComputer { get; }
    /// <summary>Loadout role that selects the tactical doctrine while Kestrel remains the flight brain.</summary>
    public ShipSubclass Subclass => _subclass;
    /// <summary>False while the enemy follows its seeded forward patrol course.</summary>
    public bool IsPlayerDetected { get; private set; }
    public float DesiredReactorOperatingLevelPercent => IsPlayerDetected ? 100f : _settings.PatrolReactorOperatingLevelPercent;
    public float FireAimToleranceRadians => _settings.FireAimTolerance;

    internal EnemyAiController(EnemyAiSettings settings, EnemyDifficulty difficulty, float nominalReverseAcceleration,
        EnemyAiModel model = EnemyAiModel.Kestrel, int seed = 0, BoardComputerProfile? boardComputer = null,
        ShipSubclass subclass = ShipSubclass.Patrol)
    {
        BoardComputer = boardComputer ?? BoardComputerProfile.MilitaryMk5;
        BoardComputer.Validate();
        _settings = settings with { FireAimTolerance = settings.FireAimTolerance * BoardComputer.FireAimToleranceMultiplier };
        Difficulty = difficulty;
        _nominalReverseAcceleration = nominalReverseAcceleration;
        _model = model;
        _subclass = subclass;
        _aegisOrbitSign = (seed & 1) == 0 ? 1f : -1f;
        _boardComputer = new BoardComputerCommandProcessor(BoardComputer, seed);
    }

    internal ShipCommand Tick(EnemyShipState enemy, ShipState player, LanceState playerLance,
        float lanceRange, GameState gameState)
    {
        if (enemy.IsDestroyed)
        {
            if (CurrentState != EnemyAiState.Destroyed) ChangeState(EnemyAiState.Destroyed);
            return LastCommand = default;
        }
        return TickCore(enemy.Ship, enemy.Lance, player, playerLance, lanceRange, gameState, detectTarget: true, enemy.Sensor);
    }

    /// <summary>Runs this controller as a symmetric duel pilot. The owner supplies the normal
    /// ShipCommand; it does not receive any privileged simulation access.</summary>
    internal ShipCommand TickDuelPilot(ShipState controlledShip, LanceState controlledLance,
        ShipState targetShip, LanceState targetLance, float lanceRange, GameState gameState) =>
        TickCore(controlledShip, controlledLance, targetShip, targetLance, lanceRange, gameState, detectTarget: false, null);

    private ShipCommand TickCore(ShipState controlledShip, LanceState controlledLance,
        ShipState targetShip, LanceState targetLance, float lanceRange, GameState gameState, bool detectTarget,
        EnemySensorDefinition? sensor)
    {
        if (gameState == GameState.GameOver) return LastCommand = default;

        LastContext = EnemyAiContext.Create(targetShip, targetLance, controlledShip, controlledLance);
        _lastControlledWeaponRange = controlledShip.Tuning.LanceRangeMeters;
        if (!IsPlayerDetected)
        {
            if (detectTarget && LastContext.DistanceToPlayer > DetectionRangeFor(targetShip, sensor!))
                return LastCommand = _boardComputer.Apply(Patrol(controlledShip));
            IsPlayerDetected = true;
        }

        TimeInState += 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate;
        if (CurrentState == EnemyAiState.Acquire) ChangeState(EnemyAiState.Approach);
        else if (_model == EnemyAiModel.Kestrel) EvaluateKestrelTransitions();
        else if (_model == EnemyAiModel.Vanguard) EvaluateVanguardTransitions();
        else if (_model == EnemyAiModel.Aegis) EvaluateAegisTransitions();
        else EvaluateTransitions();

        ShipCommand rawCommand = CurrentState switch
        {
            EnemyAiState.Approach => _model == EnemyAiModel.Kestrel ? KestrelApproach(controlledShip, targetShip) :
                _model == EnemyAiModel.Vanguard ? VanguardApproach(controlledShip, targetShip) :
                _model == EnemyAiModel.Aegis ? AegisApproach(controlledShip, targetShip) : Approach(controlledShip, targetShip),
            EnemyAiState.Attack => _model == EnemyAiModel.Kestrel ? KestrelAttack(controlledShip, controlledLance, targetShip, lanceRange) :
                _model == EnemyAiModel.Vanguard ? VanguardAttack(controlledShip, controlledLance, targetShip, lanceRange) :
                _model == EnemyAiModel.Aegis ? AegisAttack(controlledShip, controlledLance, targetShip, lanceRange) : Attack(controlledShip, controlledLance, targetShip, lanceRange),
            EnemyAiState.Reposition => _model == EnemyAiModel.Kestrel ? KestrelReposition(controlledShip, targetShip) :
                _model == EnemyAiModel.Vanguard ? VanguardReposition(controlledShip, targetShip) :
                _model == EnemyAiModel.Aegis ? AegisReposition(controlledShip, targetShip) : Reposition(controlledShip, targetShip),
            _ => default
        };
        return LastCommand = _boardComputer.Apply(rawCommand);
    }
    internal void MarkDestroyed()
    {
        ChangeState(EnemyAiState.Destroyed);
        LastCommand = default;
    }

    /// <summary>External active sonar has revealed the player; use the normal combat-detection path.</summary>
    internal void Alert() => IsPlayerDetected = true;

    /// <summary>Without a player ship in the encounter, an enemy only continues its normal patrol.</summary>
    internal ShipCommand TickWithoutPlayer(EnemyShipState enemy, GameState gameState)
    {
        if (enemy.IsDestroyed || gameState == GameState.GameOver) return LastCommand = default;
        return LastCommand = _boardComputer.Apply(Patrol(enemy.Ship));
    }

    private void EvaluateTransitions()
    {
        if (TimeInState < _settings.MinimumStateDuration) return;
        bool controlled = LastContext.RelativeVelocity.Length() <= _settings.MaximumAttackRelativeSpeed;
        bool safeFlyby = HasSafeFlybyTrajectory();
        bool inCombatRange = LastContext.DistanceToPlayer >= _settings.MinimumCombatDistance &&
                             LastContext.DistanceToPlayer <= _settings.MaximumCombatDistance;
        if (CurrentState is EnemyAiState.Approach or EnemyAiState.Reposition)
        {
            if (inCombatRange && (controlled || safeFlyby) && MathF.Abs(LastContext.EnemyAimError) < MathF.PI / 2f)
                ChangeState(EnemyAiState.Attack);
            return;
        }

        if (CurrentState == EnemyAiState.Attack &&
            (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance ||
             LastContext.DistanceToPlayer > _settings.MaximumCombatDistance ||
             (LastContext.RelativeVelocity.Length() > _settings.MaximumAttackRelativeSpeed * 1.25f && !safeFlyby) ||
             MathF.Abs(LastContext.EnemyAimError) > MathF.PI * 0.7f))
            ChangeState(EnemyAiState.Reposition);
    }

    /// <summary>
    /// Aegis accepts an attack as soon as it has a safe firing-range entry and then
    /// keeps the nose on the player. It does not abandon ATTACK merely because an
    /// inertial fly-by has high relative speed.
    /// </summary>
    private void EvaluateAegisTransitions()
    {
        if (TimeInState < _settings.MinimumStateDuration) return;
        bool inAttackRange = LastContext.DistanceToPlayer >= _settings.MinimumCombatDistance &&
                             LastContext.DistanceToPlayer <= _settings.MaximumCombatDistance;
        if (CurrentState is EnemyAiState.Approach or EnemyAiState.Reposition)
        {
            if (inAttackRange && MathF.Abs(LastContext.EnemyAimError) < MathF.PI * .65f)
                ChangeState(EnemyAiState.Attack);
            return;
        }

        if (CurrentState == EnemyAiState.Attack &&
            (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance ||
             LastContext.DistanceToPlayer > _settings.MaximumCombatDistance * 1.2f ||
             IsOverspeedCollisionRisk()))
            ChangeState(EnemyAiState.Reposition);
    }

    /// <summary>
    /// Vanguard separates navigation from firing geometry. Its ATTACK state means that a useful
    /// firing solution is already forming; recovery is deliberately held long enough to finish
    /// a safe lateral arc instead of oscillating between states every few simulation ticks.
    /// </summary>
    private void EvaluateVanguardTransitions()
    {
        if (TimeInState < _settings.MinimumStateDuration) return;

        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool inCombatRange = LastContext.DistanceToPlayer >= _settings.MinimumCombatDistance &&
                             LastContext.DistanceToPlayer <= _settings.MaximumCombatDistance;
        bool firingGeometryReady = inCombatRange &&
                                   aimError <= _settings.VanguardAttackEntryAimAngle &&
                                   !HasVanguardSafetyRisk();

        if (CurrentState == EnemyAiState.Approach)
        {
            if (HasVanguardSafetyRisk())
                ChangeState(EnemyAiState.Reposition);
            else if (firingGeometryReady)
                ChangeState(EnemyAiState.Attack);
            return;
        }

        if (CurrentState == EnemyAiState.Reposition)
        {
            if (TimeInState < _settings.VanguardMinimumRepositionDuration) return;
            if (firingGeometryReady)
                ChangeState(EnemyAiState.Attack);
            else if (!HasVanguardSafetyRisk() && LastContext.DistanceToPlayer > _settings.MaximumCombatDistance)
                ChangeState(EnemyAiState.Approach);
            return;
        }

        if (CurrentState == EnemyAiState.Attack &&
            (HasVanguardSafetyRisk() ||
             LastContext.DistanceToPlayer < _settings.MinimumCombatDistance ||
             LastContext.DistanceToPlayer > _settings.MaximumCombatDistance * 1.2f))
            ChangeState(EnemyAiState.Reposition);
    }

    /// <summary>
    /// Kestrel keeps a combat posture by default. REPOSITION is only a short deflection when
    /// a genuine close collision is still predicted; it always returns to APPROACH afterwards
    /// so its nose can reacquire the target instead of orbiting indefinitely.
    /// </summary>
    private void EvaluateKestrelTransitions()
    {
        if (TimeInState < _settings.MinimumStateDuration) return;

        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool rangedDoctrine = _subclass == ShipSubclass.Ranged;
        bool assaultDoctrine = _subclass == ShipSubclass.Assault;
        float maximumCombatDistance = rangedDoctrine
            ? CurrentWeaponRange()
            : assaultDoctrine
                ? CurrentWeaponRange()
                : PatrolMaximumDistance();
        bool inCombatRange = rangedDoctrine
            ? LastContext.DistanceToPlayer <= maximumCombatDistance
            : assaultDoctrine
                ? LastContext.DistanceToPlayer <= CurrentWeaponRange()
            : LastContext.DistanceToPlayer >= PatrolMinimumDistance() &&
              LastContext.DistanceToPlayer <= maximumCombatDistance;
        bool firingGeometryReady = inCombatRange && aimError <= _settings.KestrelAttackEntryAimAngle;

        if (CurrentState == EnemyAiState.Approach)
        {
            if (HasImmediateCollisionRisk())
                ChangeState(EnemyAiState.Reposition);
            else if (firingGeometryReady)
                ChangeState(EnemyAiState.Attack);
            return;
        }

        if (CurrentState == EnemyAiState.Reposition)
        {
            if (TimeInState >= _settings.KestrelMinimumRepositionDuration && !HasImmediateCollisionRisk())
                ChangeState(EnemyAiState.Approach);
            return;
        }

        if (CurrentState == EnemyAiState.Attack &&
            (HasImmediateCollisionRisk() ||
             (!rangedDoctrine && !assaultDoctrine && LastContext.DistanceToPlayer < PatrolMinimumDistance()) ||
             LastContext.DistanceToPlayer > maximumCombatDistance * 1.05f))
            ChangeState(EnemyAiState.Reposition);
    }

    private ShipCommand Patrol(ShipState enemy)
    {
        // Spawn velocity and nose are aligned. The patrol only restores its intended forward cruise speed.
        float forwardSpeed = Vector3.Dot(enemy.Velocity, enemy.Forward);
        return new ShipCommand(MainThrust: forwardSpeed < _settings.PatrolCruiseSpeedMetersPerSecond);
    }

    /// <summary>Maps the player's actual delivered reactor output to the installed sensor's 50/100 PU anchors.</summary>
    private static float DetectionRangeFor(ShipState player, EnemySensorDefinition sensor) =>
        sensor.DetectionRangeForPlayerOutput(player.Reactor.AvailablePower);

    private ShipCommand Approach(ShipState enemy, ShipState player)
    {
        if (IsOverspeedCollisionRisk()) return Flyby(enemy, player);
        return VelocityControl(enemy, PlannedInterceptVelocity(enemy, player));
    }

    private ShipCommand Reposition(ShipState enemy, ShipState player)
    {
        if (IsOverspeedCollisionRisk()) return Flyby(enemy, player);
        return VelocityControl(enemy, PlannedInterceptVelocity(enemy, player));
    }

    private ShipCommand AegisApproach(ShipState enemy, ShipState player)
    {
        if (IsOverspeedCollisionRisk()) return AegisFlyby(enemy, player);
        return VelocityControl(enemy, AegisEntryVelocity(enemy, player));
    }

    private ShipCommand AegisReposition(ShipState enemy, ShipState player)
    {
        if (IsOverspeedCollisionRisk()) return AegisFlyby(enemy, player);
        return VelocityControl(enemy, AegisEntryVelocity(enemy, player));
    }

    private ShipCommand VanguardApproach(ShipState enemy, ShipState player)
    {
        if (HasVanguardSafetyRisk()) return VelocityControl(enemy, VanguardEscapeVelocity(enemy, player));
        return VelocityControl(enemy, VanguardEntryVelocity(enemy, player));
    }

    private ShipCommand VanguardReposition(ShipState enemy, ShipState player)
    {
        if (LastContext.DistanceToPlayer < _settings.PreferredCombatDistance || HasVanguardSafetyRisk())
            return VelocityControl(enemy, VanguardEscapeVelocity(enemy, player));
        return VelocityControl(enemy, VanguardEntryVelocity(enemy, player));
    }

    private ShipCommand KestrelApproach(ShipState enemy, ShipState player)
    {
        if (HasImmediateCollisionRisk()) return KestrelDeflect(enemy);
        if (_subclass == ShipSubclass.Ranged)
            return LastContext.DistanceToPlayer > enemy.Tuning.LanceRangeMeters
                ? VelocityControl(enemy, KestrelRangedEntryVelocity(enemy, player))
                : KestrelRangedCombatPosture(enemy);
        if (_subclass == ShipSubclass.Assault)
            return LastContext.DistanceToPlayer > enemy.Tuning.LanceRangeMeters
                ? VelocityControl(enemy, KestrelAssaultEntryVelocity(enemy, player))
                : KestrelAssaultCombatPass(enemy);
        // Pure nose pursuit can create a stable, high-lateral-speed orbit when both Kestrels
        // fly the same controller. Outside weapons range, first match the target's velocity
        // and enter on a controlled closing vector; once in range, preserve the firing posture.
        return LastContext.DistanceToPlayer > PatrolMaximumDistance()
            ? VelocityControl(enemy, KestrelEntryVelocity(enemy, player))
            : KestrelCombatPosture(enemy);
    }

    private ShipCommand KestrelReposition(ShipState enemy, ShipState player)
    {
        if (HasImmediateCollisionRisk()) return KestrelDeflect(enemy);
        if (_subclass == ShipSubclass.Ranged)
            return LastContext.DistanceToPlayer > enemy.Tuning.LanceRangeMeters
                ? VelocityControl(enemy, KestrelRangedEntryVelocity(enemy, player))
                : KestrelRangedCombatPosture(enemy);
        if (_subclass == ShipSubclass.Assault)
            return LastContext.DistanceToPlayer > enemy.Tuning.LanceRangeMeters
                ? VelocityControl(enemy, KestrelAssaultEntryVelocity(enemy, player))
                : KestrelAssaultCombatPass(enemy);
        return LastContext.DistanceToPlayer > PatrolMaximumDistance()
            ? VelocityControl(enemy, KestrelEntryVelocity(enemy, player))
            : KestrelCombatPosture(enemy);
    }

    /// <summary>
    /// Velocity matching removes lateral relative motion before firing range. This is deliberately
    /// radial rather than an orbit target: two Kestrels must close the gap instead of perpetually
    /// following the other ship around a wide circle.
    /// </summary>
    private Vector3 KestrelEntryVelocity(ShipState enemy, ShipState player)
    {
        float radialError = LastContext.DistanceToPlayer - PatrolPreferredDistance();
        float desiredClosing = Math.Clamp(radialError * .12f, 10f,
            BrakingLimitedClosingSpeed(enemy, PatrolMaximumDistance()));
        float leadSeconds = Math.Clamp(radialError / MathF.Max(desiredClosing, 10f), 1f, 5f);
        Vector3 predictedPosition = player.Position + player.Velocity * leadSeconds;
        Vector3 toPredicted = predictedPosition - enemy.Position;
        Vector3 direction = toPredicted.LengthSquared() > .001f
            ? Vector3.Normalize(toPredicted)
            : LastContext.DirectionToPlayer;
        return player.Velocity + direction * desiredClosing;
    }

    /// <summary>
    /// Ranged Kestrel plans a velocity match just inside its own weapon envelope. The braking
    /// calculation uses the 80%-of-range reverse threshold rather than generic combat range, so
    /// a long-range ship does not arrive at maximum range with an unmanageable closing speed.
    /// </summary>
    private Vector3 KestrelRangedEntryVelocity(ShipState enemy, ShipState player)
    {
        float weaponRange = enemy.Tuning.LanceRangeMeters;
        float preferredDistance = weaponRange * _settings.RangedPreferredRangeFraction;
        float reverseStartDistance = weaponRange * _settings.RangedReverseStartRangeFraction;
        float radialError = LastContext.DistanceToPlayer - preferredDistance;
        float closingLimit = MathF.Min(_settings.RangedMaximumApproachClosingSpeed,
            BrakingLimitedClosingSpeed(enemy, reverseStartDistance));
        float desiredClosing = Math.Clamp(radialError * .10f, 4f, closingLimit);
        float leadSeconds = Math.Clamp(radialError / MathF.Max(desiredClosing, 4f), 1f, 6f);
        Vector3 predictedPosition = player.Position + player.Velocity * leadSeconds;
        Vector3 toPredicted = predictedPosition - enemy.Position;
        Vector3 direction = toPredicted.LengthSquared() > .001f
            ? Vector3.Normalize(toPredicted)
            : LastContext.DirectionToPlayer;
        return player.Velocity + direction * desiredClosing;
    }

    /// <summary>
    /// Assault ships deliberately enter on a lateral offset. The pass speed is capped by the
    /// slower of ship yaw and the installed weapon's tracking rate, so the ship is as fast as
    /// possible without outrunning its own ability to keep a firing solution.
    /// </summary>
    private Vector3 KestrelAssaultEntryVelocity(ShipState enemy, ShipState player)
    {
        float passDistance = AssaultPassDistance(enemy);
        float passSpeed = AssaultPassSpeed(enemy, passDistance);
        float leadSeconds = Math.Clamp(LastContext.DistanceToPlayer / MathF.Max(passSpeed, 1f), 1f, 6f);
        Vector3 predictedPosition = player.Position + player.Velocity * leadSeconds;
        Vector3 directionToTarget = predictedPosition - enemy.Position;
        Vector3 direction = directionToTarget.LengthSquared() > .001f
            ? Vector3.Normalize(directionToTarget)
            : LastContext.DirectionToPlayer;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)) * _aegisOrbitSign;
        Vector3 passPoint = predictedPosition + tangent * passDistance;
        Vector3 toPassPoint = passPoint - enemy.Position;
        Vector3 passDirection = toPassPoint.LengthSquared() > .001f ? Vector3.Normalize(toPassPoint) : direction;
        return player.Velocity + passDirection * passSpeed;
    }

    /// <summary>
    /// Plans a finite lateral component before combat range. That creates a safe arc
    /// for a fly-by instead of a nose-to-tail overshoot and a late main-engine turn.
    /// </summary>
    private Vector3 AegisEntryVelocity(ShipState enemy, ShipState player)
    {
        float closingLimit = BrakingLimitedClosingSpeed(enemy);
        float radialError = LastContext.DistanceToPlayer - _settings.PreferredCombatDistance;
        float desiredClosing = Math.Clamp(radialError * .09f, 8f, closingLimit);
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, LastContext.DirectionToPlayer)) * _aegisOrbitSign;
        float lateralSpeed = LastContext.DistanceToPlayer > _settings.MaximumCombatDistance ? 18f : 10f;
        return player.Velocity + LastContext.DirectionToPlayer * desiredClosing + tangent * lateralSpeed;
    }

    /// <summary>
    /// A lead pursuit target prevents a slow tail chase when the player keeps moving away. The
    /// tangential component is chosen once per enemy and remains deterministic for a supplied seed.
    /// </summary>
    private Vector3 VanguardEntryVelocity(ShipState enemy, ShipState player)
    {
        float closingLimit = BrakingLimitedClosingSpeed(enemy);
        float radialError = LastContext.DistanceToPlayer - _settings.PreferredCombatDistance;
        float desiredClosing = Math.Clamp(radialError * .12f, 12f, closingLimit);
        float leadSeconds = Math.Clamp(radialError / MathF.Max(desiredClosing, 15f), 1.5f, 7f);
        Vector3 predictedPlayerPosition = player.Position + player.Velocity * leadSeconds;
        Vector3 toPredictedPlayer = predictedPlayerPosition - enemy.Position;
        Vector3 direction = toPredictedPlayer.LengthSquared() > .001f
            ? Vector3.Normalize(toPredictedPlayer)
            : LastContext.DirectionToPlayer;
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)) * _aegisOrbitSign;
        float lateralSpeed = LastContext.DistanceToPlayer > _settings.MaximumCombatDistance
            ? _settings.VanguardLateralSpeedMetersPerSecond
            : _settings.VanguardLateralSpeedMetersPerSecond * .5f;
        return player.Velocity + direction * desiredClosing + tangent * lateralSpeed;
    }

    private Vector3 VanguardEscapeVelocity(ShipState enemy, ShipState player)
    {
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, LastContext.DirectionToPlayer)) * _aegisOrbitSign;
        float separationSpeed = LastContext.DistanceToPlayer < _settings.PreferredCombatDistance ? 24f : 0f;
        return player.Velocity + tangent * _settings.VanguardLateralSpeedMetersPerSecond -
               LastContext.DirectionToPlayer * separationSpeed;
    }

    /// <summary>
    /// Combat steering keeps the nose dedicated to the instantaneous firing solution.
    /// Translation only corrects range along that already-aimed nose; it never turns
    /// away from the player just to chase a velocity vector.
    /// </summary>
    private ShipCommand AegisAttack(ShipState enemy, LanceState enemyLance, ShipState player, float lanceRange)
    {
        if (IsOverspeedCollisionRisk()) return AegisFlyby(enemy, player);

        var turn = AegisTurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool thrustAligned = aimError <= _settings.ThrustAlignmentAngle;
        float desiredClosing = Math.Clamp((LastContext.DistanceToPlayer - _settings.PreferredCombatDistance) * .11f, -22f, 26f);
        float closingError = desiredClosing - LastContext.ClosingSpeed;
        bool main = thrustAligned && closingError > 3f;
        bool reverse = thrustAligned && closingError < -3f;
        bool fire = enemyLance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Forward, LastContext.DirectionToPlayer) > 0f &&
                    aimError <= _settings.FireAimTolerance;
        return new ShipCommand(main, reverse, turn.Left, turn.Right, fire);
    }

    private ShipCommand VanguardAttack(ShipState enemy, LanceState enemyLance, ShipState player, float lanceRange)
    {
        if (HasVanguardSafetyRisk()) return VelocityControl(enemy, VanguardEscapeVelocity(enemy, player));

        var turn = AegisTurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool thrustAligned = aimError <= _settings.ThrustAlignmentAngle;
        float desiredClosing = Math.Clamp((LastContext.DistanceToPlayer - _settings.PreferredCombatDistance) * .10f, -18f, 22f);
        float closingError = desiredClosing - LastContext.ClosingSpeed;
        bool main = thrustAligned && closingError > 3f;
        bool reverse = thrustAligned && closingError < -3f;
        bool fire = enemyLance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Forward, LastContext.DirectionToPlayer) > 0f &&
                    aimError <= _settings.FireAimTolerance;
        return new ShipCommand(main, reverse, turn.Left, turn.Right, fire);
    }

    private ShipCommand KestrelAttack(ShipState enemy, LanceState enemyLance, ShipState player, float lanceRange)
    {
        if (HasImmediateCollisionRisk()) return KestrelDeflect(enemy);

        ShipCommand posture = _subclass == ShipSubclass.Ranged
            ? KestrelRangedCombatPosture(enemy)
            : _subclass == ShipSubclass.Assault
                ? KestrelAssaultCombatPass(enemy)
                : KestrelCombatPosture(enemy);
        bool fire = enemyLance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Forward, LastContext.DirectionToPlayer) > 0f &&
                    MathF.Abs(LastContext.EnemyAimError) <= _settings.FireAimTolerance;
        return posture with { FireLance = fire };
    }

    /// <summary>
    /// Keep the nose on the moving target while regulating closing speed with the normal forward
    /// and reverse thrusters. Reverse braking avoids the old turn-away/main-engine brake behaviour.
    /// </summary>
    private ShipCommand KestrelCombatPosture(ShipState enemy)
    {
        var turn = AegisTurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool thrustAligned = aimError <= _settings.ThrustAlignmentAngle;
        float desiredClosing = Math.Clamp((LastContext.DistanceToPlayer - PatrolPreferredDistance()) * .10f, -20f, 28f);
        float closingError = desiredClosing - LastContext.ClosingSpeed;
        bool main = thrustAligned && closingError > 3f;
        bool reverse = thrustAligned && closingError < -3f;
        return new ShipCommand(main, reverse, turn.Left, turn.Right);
    }

    /// <summary>
    /// A Ranged ship retains a target-facing firing posture. At 80% of the actual installed
    /// weapon range it deliberately opens the distance with reverse thrust, instead of making a
    /// 180-degree turn or allowing a close-quarters opponent to dictate the engagement.
    /// </summary>
    private ShipCommand KestrelRangedCombatPosture(ShipState enemy)
    {
        var turn = AegisTurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool thrustAligned = aimError <= _settings.ThrustAlignmentAngle;
        float weaponRange = enemy.Tuning.LanceRangeMeters;
        float reverseStartDistance = weaponRange * _settings.RangedReverseStartRangeFraction;
        float preferredDistance = weaponRange * _settings.RangedPreferredRangeFraction;
        bool needsEarlyReverse = RequiresRangedReverseBraking(enemy, reverseStartDistance);

        float desiredClosing = needsEarlyReverse
            ? -_settings.RangedWithdrawalSpeed
            : LastContext.DistanceToPlayer > weaponRange
                ? Math.Min(_settings.RangedMaximumApproachClosingSpeed,
                    (LastContext.DistanceToPlayer - preferredDistance) * .10f)
                : 0f;
        float closingError = desiredClosing - LastContext.ClosingSpeed;
        bool main = thrustAligned && LastContext.DistanceToPlayer > weaponRange && closingError > 3f;
        bool reverse = thrustAligned && (needsEarlyReverse || closingError < -3f);
        return new ShipCommand(main, reverse, turn.Left, turn.Right);
    }

    /// <summary>
    /// Hold the bow on the target through the firing pass. Reverse thrust is deliberately not
    /// used here: the inherited velocity carries the ship through the offset arc, while the main
    /// thruster is only used to build enough speed before the intended pass distance is reached.
    /// </summary>
    private ShipCommand KestrelAssaultCombatPass(ShipState enemy)
    {
        var turn = AegisTurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
        float aimError = MathF.Abs(LastContext.EnemyAimError);
        bool thrustAligned = aimError <= _settings.ThrustAlignmentAngle;
        float passDistance = AssaultPassDistance(enemy);
        float passSpeed = AssaultPassSpeed(enemy, passDistance);
        bool main = thrustAligned && LastContext.DistanceToPlayer > passDistance &&
                    LastContext.ClosingSpeed < passSpeed;
        return new ShipCommand(MainThrust: main, YawLeft: turn.Left, YawRight: turn.Right);
    }

    private float AssaultPassDistance(ShipState enemy) => Math.Max(_settings.AssaultMinimumPassDistanceMeters,
        enemy.Tuning.LanceRangeMeters * _settings.AssaultPassDistanceRangeFraction);

    private float AssaultPassSpeed(ShipState enemy, float passDistance)
    {
        float trackingRate = Math.Min(enemy.Tuning.MaximumYawAngularVelocityRadiansPerSecond,
            Degrees(enemy.Tuning.BowWeapon.TurretDegreesPerSecond));
        float aimStableSpeed = passDistance * trackingRate * _settings.AssaultTrackingSafetyFactor;
        return Math.Clamp(aimStableSpeed, _settings.AssaultMinimumPassSpeedMetersPerSecond,
            _settings.AssaultMaximumPassSpeedMetersPerSecond);
    }

    private float PatrolMinimumDistance() => Math.Max(_settings.MinimumCombatDistance,
        CurrentWeaponRange() * _settings.PatrolMinimumRangeFraction);

    private float PatrolPreferredDistance() => CurrentWeaponRange() * _settings.PatrolPreferredRangeFraction;

    private float PatrolMaximumDistance() => CurrentWeaponRange() * _settings.PatrolMaximumRangeFraction;

    /// <summary>
    /// Holds a ranged fire corridor instead of waiting until the target is already close. The
    /// current radial closure is converted into the normal reverse-thruster stopping distance,
    /// then padded with a small manoeuvre margin. Low relative velocity produces no early brake.
    /// </summary>
    private bool RequiresRangedReverseBraking(ShipState enemy, float reverseStartDistance)
    {
        if (LastContext.DistanceToPlayer <= reverseStartDistance) return true;
        if (LastContext.ClosingSpeed <= 1f) return false;
        float reverseFactor = enemy.Power.PropulsionPowerFactor * enemy.Systems.ReverseBoosterCondition;
        float brakingAcceleration = MathF.Max(.01f, _nominalReverseAcceleration * reverseFactor);
        float brakingDistance = LastContext.ClosingSpeed * LastContext.ClosingSpeed / (2f * brakingAcceleration);
        return LastContext.DistanceToPlayer <= reverseStartDistance + brakingDistance + _settings.RangedReverseBrakingMarginMeters;
    }

    /// <summary>
    /// Minimal emergency manoeuvre: preserve or build lateral motion until the predicted closest
    /// pass clears the hard AI minimum. The next state returns to target-facing pursuit.
    /// </summary>
    private ShipCommand KestrelDeflect(ShipState enemy)
    {
        Vector3 tangent = Vector3.Cross(Vector3.UnitY, LastContext.DirectionToPlayer);
        // LastContext.RelativeVelocity is player minus enemy, so negate it for enemy-relative motion.
        Vector3 relativeVelocity = -LastContext.RelativeVelocity;
        if (Vector3.Dot(relativeVelocity, tangent) < 0f) tangent = -tangent;
        if (tangent.LengthSquared() < .001f) tangent = Vector3.Cross(Vector3.UnitY, enemy.Forward) * _aegisOrbitSign;
        tangent = Vector3.Normalize(tangent);
        var turn = AegisTurnToward(enemy, tangent);
        bool main = MathF.Abs(EnemyAiContext.SignedPlanarAngle(enemy.Forward, tangent)) <= _settings.ThrustAlignmentAngle;
        // A standard VECTOR S-1 needs time to rotate into the lateral escape arc. While the
        // ship still faces the incoming target, its ANCHOR reverse booster buys that time
        // without the forbidden 180-degree main-engine braking turn.
        bool facesIncomingTarget = MathF.Abs(EnemyAiContext.SignedPlanarAngle(enemy.Forward, LastContext.DirectionToPlayer)) < MathF.PI / 2f;
        bool reverse = facesIncomingTarget && LastContext.ClosingSpeed > 1f;
        return new ShipCommand(MainThrust: main, ReverseThrust: reverse, YawLeft: turn.Left, YawRight: turn.Right);
    }

    private ShipCommand AegisFlyby(ShipState enemy, ShipState player)
    {
        Vector3 tangent = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, LastContext.DirectionToPlayer)) * _aegisOrbitSign;
        var turn = AegisTurnToward(enemy, tangent);
        bool main = MathF.Abs(EnemyAiContext.SignedPlanarAngle(enemy.Forward, tangent)) <= _settings.ThrustAlignmentAngle;
        return new ShipCommand(MainThrust: main, YawLeft: turn.Left, YawRight: turn.Right);
    }

    private Vector3 PlannedInterceptVelocity(ShipState enemy, ShipState player)
    {
        float distance = LastContext.DistanceToPlayer;
        float closingLimit = BrakingLimitedClosingSpeed(enemy);
        float travelDistance = MathF.Max(1f, distance - _settings.PreferredCombatDistance);
        float leadSeconds = MathF.Max(_settings.MinimumInterceptLeadSeconds, travelDistance / MathF.Max(1f, closingLimit));
        Vector3 estimatedPlayerPosition = player.Position + player.Velocity * leadSeconds;
        Vector3 toEstimatedPlayer = estimatedPlayerPosition - enemy.Position;
        Vector3 approachDirection = toEstimatedPlayer.LengthSquared() > 0.001f
            ? Vector3.Normalize(toEstimatedPlayer) : LastContext.DirectionToPlayer;
        Vector3 arrivalPosition = estimatedPlayerPosition - approachDirection * _settings.PreferredCombatDistance;
        return (arrivalPosition - enemy.Position) / leadSeconds;
    }

    private float BrakingLimitedClosingSpeed(ShipState? enemy, float? brakingStartDistance = null)
    {
        float startDistance = brakingStartDistance ?? _settings.MaximumCombatDistance;
        float availableDistance = MathF.Max(0f, LastContext.DistanceToPlayer - startDistance);
        float reverseFactor = enemy is null ? 1f : enemy.Power.PropulsionPowerFactor * enemy.Systems.ReverseBoosterCondition;
        float brakingAcceleration = MathF.Max(0.01f, _nominalReverseAcceleration * reverseFactor);
        float speedSquared = _settings.MaximumAttackRelativeSpeed * _settings.MaximumAttackRelativeSpeed + 2f * brakingAcceleration * availableDistance;
        return MathF.Min(_settings.MaximumApproachClosingSpeed, MathF.Sqrt(speedSquared));
    }

    private bool IsOverspeedCollisionRisk()
    {
        return HasImmediateCollisionRisk();
    }

    /// <summary>
    /// Shared base-game avoidance rule: 50 m is the actual collision; AI accepts no direct
    /// course below 100 m, but begins the correction only inside the 350 m trigger range.
    /// </summary>
    private bool HasImmediateCollisionRisk()
    {
        if (LastContext.DistanceToPlayer < _settings.CollisionAvoidanceMinimumDistanceMeters) return true;
        return LastContext.DistanceToPlayer <= _settings.CollisionAvoidanceTriggerDistanceMeters &&
               LastContext.ClosingSpeed > 0f &&
               ClosestApproachDistance() < _settings.CollisionAvoidanceMinimumDistanceMeters;
    }

    /// <summary>
    /// Vanguard starts a safety arc before either the collision threshold or the nominal 250 m
    /// combat minimum is threatened. Unlike the legacy check this also catches a slow but certain
    /// close pass, which was the source of 112 m near-collisions in the Aegis logs.
    /// </summary>
    private bool HasVanguardSafetyRisk()
    {
        if (LastContext.DistanceToPlayer < _settings.VanguardSafetyDistanceMeters) return true;
        return LastContext.ClosingSpeed > 0f && ClosestApproachDistance() < _settings.VanguardSafetyDistanceMeters;
    }

    private bool HasSafeFlybyTrajectory() =>
        LastContext.RelativeVelocity.Length() > _settings.MaximumAttackRelativeSpeed &&
        ClosestApproachDistance() >= _settings.FlybySafetyDistanceMeters;

    private float ClosestApproachDistance()
    {
        float speedSquared = LastContext.RelativeVelocity.LengthSquared();
        if (speedSquared < 0.001f) return LastContext.DistanceToPlayer;
        float time = -Vector3.Dot(LastContext.RelativePosition, LastContext.RelativeVelocity) / speedSquared;
        if (time <= 0f) return LastContext.DistanceToPlayer;
        return (LastContext.RelativePosition + LastContext.RelativeVelocity * time).Length();
    }

    private ShipCommand Flyby(ShipState enemy, ShipState player)
    {
        Vector3 side = Vector3.Cross(Vector3.UnitY, LastContext.DirectionToPlayer);
        Vector3 enemyRelativeVelocity = enemy.Velocity - player.Velocity;
        if (Vector3.Dot(enemyRelativeVelocity, side) < 0f) side = -side;
        // First rotate into the tangent. Feeding the target velocity into VelocityControl here can point the
        // desired acceleration behind the ship and cause the forbidden 180 degree main-engine braking turn.
        var turn = TurnToward(enemy, side);
        float sideAngle = MathF.Abs(EnemyAiContext.SignedPlanarAngle(enemy.Forward, side));
        bool main = sideAngle <= _settings.ThrustAlignmentAngle;
        return new ShipCommand(MainThrust: main, YawLeft: turn.Left, YawRight: turn.Right);
    }

    private ShipCommand Attack(ShipState enemy, LanceState enemyLance, ShipState player, float lanceRange)
    {
        var turn = TurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
        bool aligned = MathF.Abs(LastContext.EnemyAimError) < _settings.ThrustAlignmentAngle;
        bool flyby = HasSafeFlybyTrajectory();
        bool main = !flyby && LastContext.DistanceToPlayer > _settings.PreferredCombatDistance + 40f &&
                    LastContext.ClosingSpeed < _settings.MaximumAttackRelativeSpeed && aligned;
        bool reverse = !flyby && (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance + 50f ||
                                  LastContext.ClosingSpeed > _settings.MaximumAttackRelativeSpeed) && aligned;
        bool fire = enemyLance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Forward, LastContext.DirectionToPlayer) > 0f &&
                    MathF.Abs(LastContext.EnemyAimError) <= _settings.FireAimTolerance;
        return new ShipCommand(main, reverse, turn.Left, turn.Right, fire);
    }

    private ShipCommand VelocityControl(ShipState enemy, Vector3 desiredVelocity)
    {
        Vector3 error = desiredVelocity - enemy.Velocity;
        if (error.LengthSquared() < 4f) return TurnTowardCommand(enemy, LastContext.DirectionToPlayer);
        bool protectedCombatDistance = LastContext.DistanceToPlayer <= _settings.NoMainEngineTurnDistanceMeters;
        bool brakingTowardTarget = LastContext.ClosingSpeed > _settings.MaximumAttackRelativeSpeed &&
                                  Vector3.Dot(error, enemy.Forward) < 0f;
        if (protectedCombatDistance && brakingTowardTarget)
        {
            // Keep the nose in the fight and use the dedicated reverse thruster; do not expose the stern
            // by turning around to brake with the main engine.
            var aimTurn = TurnToward(enemy, LastContext.DirectionToPlayer, LastContext.LineOfSightAngularVelocity);
            return new ShipCommand(ReverseThrust: true, YawLeft: aimTurn.Left, YawRight: aimTurn.Right);
        }
        Vector3 desiredThrust = Vector3.Normalize(error);
        var turn = TurnToward(enemy, desiredThrust);
        float angle = MathF.Abs(EnemyAiContext.SignedPlanarAngle(enemy.Forward, desiredThrust));
        bool main = angle <= _settings.ThrustAlignmentAngle;
        bool reverse = MathF.Abs(MathF.PI - angle) <= _settings.ThrustAlignmentAngle;
        return new ShipCommand(main, reverse, turn.Left, turn.Right);
    }

    private ShipCommand TurnTowardCommand(ShipState ship, Vector3 direction)
    {
        var turn = TurnToward(ship, direction, LastContext.LineOfSightAngularVelocity);
        return new ShipCommand(YawLeft: turn.Left, YawRight: turn.Right);
    }

    private (bool Left, bool Right) TurnToward(ShipState ship, Vector3 direction, float targetAngularVelocity = 0f)
    {
        float error = EnemyAiContext.SignedPlanarAngle(ship.Forward, direction);
        // PD controller on the moving line of sight: rate error must be relative to the target bearing,
        // otherwise a ship that tracks a lateral fly-by retains a permanent angular offset.
        float signal = _settings.RotationKp * error +
                       _settings.RotationKd * (targetAngularVelocity - ship.AngularVelocity.Y);
        return (signal > _settings.TurnCommandThreshold, signal < -_settings.TurnCommandThreshold);
    }

    /// <summary>Rate-targeting controller used by Aegis to remove the persistent lateral aim offset.</summary>
    private (bool Left, bool Right) AegisTurnToward(ShipState ship, Vector3 direction, float targetAngularVelocity = 0f)
    {
        float error = EnemyAiContext.SignedPlanarAngle(ship.Forward, direction);
        float desiredRate = Math.Clamp(targetAngularVelocity + error * 3.6f, -1.15f, 1.15f);
        float rateError = desiredRate - ship.AngularVelocity.Y;
        if (MathF.Abs(error) < Degrees(.3f) && MathF.Abs(rateError) < .015f) return default;
        return (rateError > .012f, rateError < -.012f);
    }

    private static float Degrees(float value) => value * MathF.PI / 180f;

    private float CurrentWeaponRange() => _lastControlledWeaponRange;

    private float _lastControlledWeaponRange = 1_000f;

    private void ChangeState(EnemyAiState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        TimeInState = 0f;
    }
}
