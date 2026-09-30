using System.Numerics;
using SpaceSim.Core.Combat;
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

    public EnemyAiState CurrentState { get; private set; } = EnemyAiState.Acquire;
    public float TimeInState { get; private set; }
    public EnemyAiContext LastContext { get; private set; }
    public ShipCommand LastCommand { get; private set; }
    public EnemyDifficulty Difficulty { get; }
    /// <summary>False while the enemy follows its seeded forward patrol course.</summary>
    public bool IsPlayerDetected { get; private set; }
    public float DesiredReactorOperatingLevelPercent => IsPlayerDetected ? 100f : _settings.PatrolReactorOperatingLevelPercent;
    public float FireAimToleranceRadians => _settings.FireAimTolerance;

    internal EnemyAiController(EnemyAiSettings settings, EnemyDifficulty difficulty, float nominalReverseAcceleration)
    {
        _settings = settings;
        Difficulty = difficulty;
        _nominalReverseAcceleration = nominalReverseAcceleration;
    }

    internal ShipCommand Tick(EnemyShipState enemy, ShipState player, LanceState playerLance,
        float lanceRange, GameState gameState)
    {
        if (enemy.IsDestroyed)
        {
            if (CurrentState != EnemyAiState.Destroyed) ChangeState(EnemyAiState.Destroyed);
            return LastCommand = default;
        }
        if (gameState == GameState.GameOver) return LastCommand = default;

        LastContext = EnemyAiContext.Create(player, playerLance, enemy);
        if (!IsPlayerDetected)
        {
            if (LastContext.DistanceToPlayer > _settings.DetectionRangeMeters)
                return LastCommand = Patrol(enemy.Ship);
            IsPlayerDetected = true;
        }

        TimeInState += 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate;
        if (CurrentState == EnemyAiState.Acquire) ChangeState(EnemyAiState.Approach);
        else EvaluateTransitions();

        return LastCommand = CurrentState switch
        {
            EnemyAiState.Approach => Approach(enemy.Ship, player),
            EnemyAiState.Attack => Attack(enemy, player, lanceRange),
            EnemyAiState.Reposition => Reposition(enemy.Ship, player),
            _ => default
        };
    }

    internal void MarkDestroyed()
    {
        ChangeState(EnemyAiState.Destroyed);
        LastCommand = default;
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

    private ShipCommand Patrol(ShipState enemy)
    {
        // Spawn velocity and nose are aligned. The patrol only restores its intended forward cruise speed.
        float forwardSpeed = Vector3.Dot(enemy.Velocity, enemy.Forward);
        return new ShipCommand(MainThrust: forwardSpeed < _settings.PatrolCruiseSpeedMetersPerSecond);
    }

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

    private float BrakingLimitedClosingSpeed(ShipState? enemy)
    {
        float availableDistance = MathF.Max(0f, LastContext.DistanceToPlayer - _settings.MaximumCombatDistance);
        float reverseFactor = enemy is null ? 1f : enemy.Power.PropulsionPowerFactor * enemy.Systems.PropulsionCondition;
        float brakingAcceleration = MathF.Max(0.01f, _nominalReverseAcceleration * reverseFactor);
        float speedSquared = _settings.MaximumAttackRelativeSpeed * _settings.MaximumAttackRelativeSpeed + 2f * brakingAcceleration * availableDistance;
        return MathF.Min(_settings.MaximumApproachClosingSpeed, MathF.Sqrt(speedSquared));
    }

    private bool IsOverspeedCollisionRisk()
    {
        if (LastContext.ClosingSpeed <= _settings.MaximumAttackRelativeSpeed) return false;
        return ClosestApproachDistance() < _settings.FlybySafetyDistanceMeters;
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
        float flybySpeed = MathF.Max(_settings.MaximumAttackRelativeSpeed, enemyRelativeVelocity.Length());
        // Keep moving past the player on a tangent, with a small outward component.
        Vector3 desiredVelocity = player.Velocity + side * flybySpeed - LastContext.DirectionToPlayer * 10f;
        return VelocityControl(enemy, desiredVelocity);
    }

    private ShipCommand Attack(EnemyShipState enemy, ShipState player, float lanceRange)
    {
        var turn = TurnToward(enemy.Ship, LastContext.DirectionToPlayer);
        bool aligned = MathF.Abs(LastContext.EnemyAimError) < _settings.ThrustAlignmentAngle;
        bool flyby = HasSafeFlybyTrajectory();
        bool main = !flyby && LastContext.DistanceToPlayer > _settings.PreferredCombatDistance + 40f &&
                    LastContext.ClosingSpeed < _settings.MaximumAttackRelativeSpeed && aligned;
        bool reverse = !flyby && (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance + 50f ||
                                  LastContext.ClosingSpeed > _settings.MaximumAttackRelativeSpeed) && aligned;
        bool fire = enemy.Lance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Ship.Forward, LastContext.DirectionToPlayer) > 0f &&
                    MathF.Abs(LastContext.EnemyAimError) <= _settings.FireAimTolerance;
        return new ShipCommand(main, reverse, turn.Left, turn.Right, fire);
    }

    private ShipCommand VelocityControl(ShipState enemy, Vector3 desiredVelocity)
    {
        Vector3 error = desiredVelocity - enemy.Velocity;
        if (error.LengthSquared() < 4f) return TurnTowardCommand(enemy, LastContext.DirectionToPlayer);
        Vector3 desiredThrust = Vector3.Normalize(error);
        var turn = TurnToward(enemy, desiredThrust);
        float angle = MathF.Abs(EnemyAiContext.SignedPlanarAngle(enemy.Forward, desiredThrust));
        bool main = angle <= _settings.ThrustAlignmentAngle;
        bool reverse = MathF.Abs(MathF.PI - angle) <= _settings.ThrustAlignmentAngle;
        return new ShipCommand(main, reverse, turn.Left, turn.Right);
    }

    private ShipCommand TurnTowardCommand(ShipState ship, Vector3 direction)
    {
        var turn = TurnToward(ship, direction);
        return new ShipCommand(YawLeft: turn.Left, YawRight: turn.Right);
    }

    private (bool Left, bool Right) TurnToward(ShipState ship, Vector3 direction)
    {
        float error = EnemyAiContext.SignedPlanarAngle(ship.Forward, direction);
        float signal = _settings.RotationKp * error - _settings.RotationKd * ship.AngularVelocity.Y;
        return (signal > _settings.TurnCommandThreshold, signal < -_settings.TurnCommandThreshold);
    }

    private void ChangeState(EnemyAiState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        TimeInState = 0f;
    }
}
