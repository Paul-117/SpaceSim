using System.Numerics;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.Power;

namespace SpaceSim.Core.AI;

/// <summary>Deterministic FSM. Its only output is a regular ShipCommand.</summary>
public sealed class EnemyAiController
{
    private readonly EnemyAiSettings _settings;
    private readonly PowerSettings _powerSettings;
    private readonly Random _random;
    public EnemyAiState CurrentState { get; private set; } = EnemyAiState.Acquire;
    public float TimeInState { get; private set; }
    public EvadeDirection? CurrentEvadeDirection { get; private set; }
    public EnemyAiContext LastContext { get; private set; }
    public ShipCommand LastCommand { get; private set; }
    public PowerProfile CurrentPowerProfile { get; private set; }
    public float TimeInPowerProfile { get; private set; }
    public EnemyRiskLevel CurrentRiskLevel { get; private set; } = EnemyRiskLevel.Aggressive;
    public float EvadeCooldownRemaining { get; private set; }
    public EnemyDifficulty Difficulty { get; }

    internal EnemyAiController(EnemyAiSettings settings, PowerSettings powerSettings, EnemyDifficulty difficulty, int seed)
    {
        _settings = settings;
        _powerSettings = powerSettings;
        Difficulty = difficulty;
        _random = new Random(seed);
        CurrentPowerProfile = powerSettings.DefaultProfile;
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
        CurrentRiskLevel = EnemyRiskAssessment.Calculate(enemy, _settings);
        EvadeCooldownRemaining = MathF.Max(0f, EvadeCooldownRemaining - 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate);
        TimeInState += 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate;
        bool threatened = IsRiskAwareThreat(LastContext, enemy, lanceRange);

        if (CurrentState == EnemyAiState.Acquire)
            ChangeState(EnemyAiState.Approach);
        else if (CurrentState != EnemyAiState.Evade && EvadeCooldownRemaining <= 0f && threatened)
            ChangeState(EnemyAiState.Evade);
        else
            EvaluateTransitions(threatened);

        UpdatePowerProfile(enemy);

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

    private bool IsRiskAwareThreat(EnemyAiContext context, EnemyShipState enemy, float lanceRange)
    {
        if (context.DistanceToPlayer > lanceRange) return false;
        float shieldFraction = enemy.Ship.Shield.MaximumShield <= 0f ? 0f :
            enemy.Ship.Shield.CurrentShield / enemy.Ship.Shield.MaximumShield;
        (float charge, float aim) = CurrentRiskLevel switch
        {
            EnemyRiskLevel.Aggressive => (shieldFraction >= 0.999f ? 1.01f : _settings.AggressiveThreatChargeThreshold,
                _settings.AggressiveThreatAimAngle),
            EnemyRiskLevel.Normal => (_settings.NormalThreatChargeThreshold, _settings.NormalThreatAimAngle),
            EnemyRiskLevel.Defensive => (_settings.DefensiveThreatChargeThreshold, _settings.DefensiveThreatAimAngle),
            _ => (_settings.CriticalThreatChargeThreshold, _settings.CriticalThreatAimAngle)
        };
        if (CurrentState == EnemyAiState.Evade) aim *= 1.35f;
        return context.PlayerLanceCharge >= charge && MathF.Abs(context.PlayerAimError) <= aim;
    }

    private void EvaluateTransitions(bool threatened)
    {
        if (CurrentState == EnemyAiState.Evade)
        {
            if (TimeInState >= _settings.MaximumEvadeDuration ||
                (TimeInState >= _settings.MinimumEvadeDuration && !threatened))
                ChangeState(CanAttack() ? EnemyAiState.Attack : EnemyAiState.Reposition);
            return;
        }
        if (TimeInState < _settings.MinimumStateDuration) return;

        // The close approach is deliberately allowed to carry modest inertial speed;
        // ATTACK continues to brake it instead of bouncing into REPOSITION at the edge.
        bool controlled = LastContext.RelativeVelocity.Length() <= _settings.MaximumDesiredRelativeSpeed * 1.2f;
        bool inEntryRange = LastContext.DistanceToPlayer >= _settings.MinimumCombatDistance &&
                            LastContext.DistanceToPlayer <= _settings.AttackEnterDistance;
        if (CurrentState is EnemyAiState.Approach or EnemyAiState.Reposition)
        {
            if (inEntryRange && controlled && MathF.Abs(LastContext.EnemyAimError) < MathF.PI / 2f)
                ChangeState(EnemyAiState.Attack);
        }
        else if (CurrentState == EnemyAiState.Attack &&
                 (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance ||
                  LastContext.DistanceToPlayer > _settings.AttackExitDistance ||
                  LastContext.RelativeVelocity.Length() > _settings.MaximumDesiredRelativeSpeed * 1.35f ||
                  MathF.Abs(LastContext.EnemyAimError) > MathF.PI * 0.7f))
            ChangeState(EnemyAiState.Reposition);
    }

    private bool CanAttack() =>
        LastContext.DistanceToPlayer >= _settings.MinimumCombatDistance &&
        LastContext.DistanceToPlayer <= _settings.AttackEnterDistance &&
        LastContext.RelativeVelocity.Length() <= _settings.MaximumDesiredRelativeSpeed * 1.15f;

    private ShipCommand Approach(ShipState enemy, ShipState player)
    {
        float desiredClosing = Math.Clamp((LastContext.DistanceToPlayer - PreferredCombatDistance) * 0.15f,
            -_settings.MaximumDesiredClosingSpeed, _settings.MaximumDesiredClosingSpeed);
        return VelocityControl(enemy, player.Velocity + LastContext.DirectionToPlayer * desiredClosing);
    }

    private ShipCommand Reposition(ShipState enemy, ShipState player)
    {
        float radial = Math.Clamp((LastContext.DistanceToPlayer - PreferredCombatDistance) * 0.12f,
            -_settings.MaximumDesiredClosingSpeed, _settings.MaximumDesiredClosingSpeed);
        Vector3 desiredVelocity = player.Velocity + LastContext.DirectionToPlayer * radial;
        return VelocityControl(enemy, desiredVelocity);
    }

    private ShipCommand Attack(EnemyShipState enemy, ShipState player, float lanceRange)
    {
        var turn = TurnToward(enemy.Ship, LastContext.DirectionToPlayer);
        Vector3 velocityError = player.Velocity - enemy.Ship.Velocity;
        float radialError = Vector3.Dot(velocityError, LastContext.DirectionToPlayer);
        bool main = LastContext.DistanceToPlayer > PreferredCombatDistance + 60f &&
                    LastContext.ClosingSpeed < _settings.MaximumDesiredClosingSpeed &&
                    MathF.Abs(LastContext.EnemyAimError) < _settings.ThrustAlignmentAngle;
        bool reverse = (LastContext.DistanceToPlayer < _settings.MinimumCombatDistance || radialError > 35f) &&
                       MathF.Abs(LastContext.EnemyAimError) < _settings.ThrustAlignmentAngle;
        bool fire = enemy.Lance.IsReady && LastContext.DistanceToPlayer <= lanceRange &&
                    Vector3.Dot(enemy.Ship.Forward, LastContext.DirectionToPlayer) > 0f &&
                    MathF.Abs(LastContext.EnemyAimError) <= _settings.FireAimTolerance;
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
        if (CurrentState == EnemyAiState.Evade && state != EnemyAiState.Evade)
            EvadeCooldownRemaining = CurrentRiskLevel switch
            {
                EnemyRiskLevel.Aggressive => _settings.AggressiveEvadeCooldown,
                EnemyRiskLevel.Normal => _settings.NormalEvadeCooldown,
                EnemyRiskLevel.Defensive => _settings.DefensiveEvadeCooldown,
                _ => _settings.CriticalEvadeCooldown
            };
        CurrentState = state;
        TimeInState = 0f;
        CurrentEvadeDirection = state == EnemyAiState.Evade
            ? (_random.Next(2) == 0 ? EvadeDirection.Left : EvadeDirection.Right)
            : null;
    }

    private float PreferredCombatDistance => CurrentRiskLevel switch
    {
        EnemyRiskLevel.Aggressive => _settings.PreferredCombatDistanceAggressive,
        EnemyRiskLevel.Normal => _settings.PreferredCombatDistanceNormal,
        EnemyRiskLevel.Defensive => _settings.PreferredCombatDistanceDefensive,
        _ => _settings.PreferredCombatDistanceCritical
    };

    private void UpdatePowerProfile(EnemyShipState enemy)
    {
        TimeInPowerProfile += 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate;
        bool defend = enemy.Ship.Shield.CurrentShield / enemy.Ship.Shield.MaximumShield <=
                      _powerSettings.DefendShieldThresholdFraction;
        PowerProfile desired = defend ? _powerSettings.DefendProfile : CurrentState switch
        {
            EnemyAiState.Attack => _powerSettings.AttackProfile,
            EnemyAiState.Evade => _powerSettings.EvadeProfile,
            EnemyAiState.Approach or EnemyAiState.Reposition => _powerSettings.RepositionProfile,
            _ => _powerSettings.DefaultProfile
        };
        if (desired == CurrentPowerProfile || (!defend && TimeInPowerProfile < _powerSettings.MinimumProfileDuration)) return;
        CurrentPowerProfile = desired;
        TimeInPowerProfile = 0f;
    }
}
