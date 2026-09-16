using System.Numerics;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.AI;

/// <summary>Deterministic FSM. Its only output is a regular ShipCommand.</summary>
public sealed class EnemyAiController
{
    private readonly EnemyAiSettings _settings;
    private readonly Random _random;
    public EnemyAiState CurrentState { get; private set; } = EnemyAiState.Acquire;
    public float TimeInState { get; private set; }
    public EvadeDirection? CurrentEvadeDirection { get; private set; }
    public EnemyAiContext LastContext { get; private set; }
    public ShipCommand LastCommand { get; private set; }

    internal EnemyAiController(EnemyAiSettings settings, int seed)
    {
        _settings = settings;
        _random = new Random(seed);
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
        TimeInState += 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate;
        bool threatened = IsThreatenedByPlayerLance(LastContext, lanceRange,
            CurrentState == EnemyAiState.Evade ? _settings.ThreatExitAimAngle : _settings.ThreatAimAngle);

        if (CurrentState == EnemyAiState.Acquire)
            ChangeState(EnemyAiState.Approach);
        else if (CurrentState != EnemyAiState.Evade && threatened)
            ChangeState(EnemyAiState.Evade);
        else
            EvaluateTransitions(threatened);

        return LastCommand = CurrentState switch
        {
            EnemyAiState.Approach => Approach(enemy.Ship, player),
            EnemyAiState.Attack => Attack(enemy, player, lanceRange),
            EnemyAiState.Evade => Evade(enemy.Ship),
            EnemyAiState.Reposition => Reposition(enemy.Ship, player),
            _ => default
        };
    }

    internal void MarkDestroyed()
    {
        ChangeState(EnemyAiState.Destroyed);
        LastCommand = default;
    }

    public bool IsThreatenedByPlayerLance(EnemyAiContext context, float lanceRange, float? aimAngle = null) =>
        context.PlayerLanceCharge >= _settings.ThreatChargeThreshold &&
        context.DistanceToPlayer <= lanceRange &&
        MathF.Abs(context.PlayerAimError) <= (aimAngle ?? _settings.ThreatAimAngle);

    private void EvaluateTransitions(bool threatened)
    {
        if (CurrentState == EnemyAiState.Evade)
        {
            if (TimeInState >= _settings.MaximumEvadeDuration ||
                (TimeInState >= _settings.MinimumEvadeDuration && !threatened))
                ChangeState(EnemyAiState.Reposition);
            return;
        }
        if (TimeInState < _settings.MinimumStateDuration) return;

        bool controlled = LastContext.RelativeVelocity.Length() <= _settings.AttackRelativeSpeed;
        bool inEntryRange = LastContext.DistanceToPlayer >= _settings.MinimumCombatDistance &&
                            LastContext.DistanceToPlayer <= _settings.AttackEnterDistance;
        if (CurrentState is EnemyAiState.Approach or EnemyAiState.Reposition)
        {
            if (inEntryRange && controlled && MathF.Abs(LastContext.EnemyAimError) < MathF.PI / 3f)
                ChangeState(EnemyAiState.Attack);
        }
        else if (CurrentState == EnemyAiState.Attack &&
                 (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance ||
                  LastContext.DistanceToPlayer > _settings.AttackExitDistance ||
                  LastContext.RelativeVelocity.Length() > _settings.MaximumDesiredRelativeSpeed ||
                  MathF.Abs(LastContext.EnemyAimError) > MathF.PI * 0.7f))
            ChangeState(EnemyAiState.Reposition);
    }

    private ShipCommand Approach(ShipState enemy, ShipState player)
    {
        float desiredClosing = Math.Clamp((LastContext.DistanceToPlayer - _settings.PreferredCombatDistance) * 0.12f,
            -_settings.MaximumDesiredClosingSpeed, _settings.MaximumDesiredClosingSpeed);
        return VelocityControl(enemy, player.Velocity + LastContext.DirectionToPlayer * desiredClosing);
    }

    private ShipCommand Reposition(ShipState enemy, ShipState player)
    {
        float radial = Math.Clamp((LastContext.DistanceToPlayer - _settings.PreferredCombatDistance) * 0.16f,
            -_settings.MaximumDesiredClosingSpeed, _settings.MaximumDesiredClosingSpeed);
        Vector3 desiredVelocity = player.Velocity + LastContext.DirectionToPlayer * radial;
        return VelocityControl(enemy, desiredVelocity);
    }

    private ShipCommand Attack(EnemyShipState enemy, ShipState player, float lanceRange)
    {
        var turn = TurnToward(enemy.Ship, LastContext.DirectionToPlayer);
        Vector3 velocityError = player.Velocity - enemy.Ship.Velocity;
        float radialError = Vector3.Dot(velocityError, LastContext.DirectionToPlayer);
        bool main = LastContext.DistanceToPlayer > _settings.PreferredCombatDistance + 80f &&
                    LastContext.ClosingSpeed < _settings.MaximumDesiredClosingSpeed &&
                    MathF.Abs(LastContext.EnemyAimError) < _settings.ThrustAlignmentAngle;
        bool reverse = (LastContext.DistanceToPlayer < _settings.PreferredCombatDistance - 80f || radialError > 25f) &&
                       MathF.Abs(LastContext.EnemyAimError) < _settings.ThrustAlignmentAngle;
        bool fire = enemy.Lance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Ship.Forward, LastContext.DirectionToPlayer) > 0f &&
                    MathF.Abs(LastContext.EnemyAimError) <= _settings.FireAimTolerance;
        if (fire) ChangeState(EnemyAiState.Reposition);
        return new ShipCommand(main, reverse, turn.Left, turn.Right, fire);
    }

    private ShipCommand Evade(ShipState enemy)
    {
        bool left = CurrentEvadeDirection == EvadeDirection.Left;
        bool thrust = MathF.Abs(LastContext.EnemyAimError) >= _settings.EvadeThrustAngle;
        return new ShipCommand(MainThrust: thrust, YawLeft: left, YawRight: !left);
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
        CurrentEvadeDirection = state == EnemyAiState.Evade
            ? (_random.Next(2) == 0 ? EvadeDirection.Left : EvadeDirection.Right)
            : null;
    }
}
