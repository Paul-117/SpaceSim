using System.Numerics;
using SpaceSim.Core.Ships;

namespace SpaceSim.Core.AI;

/// <summary>
/// Bridge flight assistance using the same movement thresholds as the enemy AI.
/// It only emits regular flight commands and never produces a weapon command.
/// </summary>
public sealed class AutopilotController
{
    private readonly EnemyAiSettings _settings;
    private readonly float _nominalReverseAcceleration;
    private FlightContext _context;

    public AutopilotState CurrentState { get; private set; } = AutopilotState.Approach;
    public float TimeInState { get; private set; }

    public AutopilotController(EnemyAiSettings settings, float nominalReverseAcceleration)
    {
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        if (!float.IsFinite(nominalReverseAcceleration) || nominalReverseAcceleration <= 0f)
            throw new ArgumentOutOfRangeException(nameof(nominalReverseAcceleration));
        _nominalReverseAcceleration = nominalReverseAcceleration;
    }

    public ShipCommand Tick(ShipState ship, ShipState target)
    {
        ArgumentNullException.ThrowIfNull(ship);
        ArgumentNullException.ThrowIfNull(target);
        _context = FlightContext.Create(ship, target);
        TimeInState += 1f / SpaceSim.Core.Simulation.SimulationSettings.TickRate;
        EvaluateTransitions();
        return CurrentState switch
        {
            AutopilotState.Approach => Approach(ship, target),
            AutopilotState.Attack => AttackPosition(ship),
            AutopilotState.Reposition => Reposition(ship, target),
            _ => default
        };
    }

    private void EvaluateTransitions()
    {
        if (TimeInState < _settings.MinimumStateDuration) return;
        bool controlled = _context.RelativeVelocity.Length() <= _settings.MaximumAttackRelativeSpeed;
        bool safeFlyby = HasSafeFlybyTrajectory();
        bool inCombatRange = _context.Distance >= _settings.MinimumCombatDistance &&
                             _context.Distance <= _settings.MaximumCombatDistance;
        if (CurrentState is AutopilotState.Approach or AutopilotState.Reposition)
        {
            if (inCombatRange && (controlled || safeFlyby) && MathF.Abs(_context.AimError) < MathF.PI / 2f)
                ChangeState(AutopilotState.Attack);
            return;
        }

        if (CurrentState == AutopilotState.Attack &&
            (_context.Distance < _settings.MinimumCombatDistance ||
             _context.Distance > _settings.MaximumCombatDistance ||
             (_context.RelativeVelocity.Length() > _settings.MaximumAttackRelativeSpeed * 1.25f && !safeFlyby) ||
             MathF.Abs(_context.AimError) > MathF.PI * .7f))
            ChangeState(AutopilotState.Reposition);
    }

    private ShipCommand Approach(ShipState ship, ShipState target) =>
        IsOverspeedCollisionRisk() ? Flyby(ship, target) : VelocityControl(ship, PlannedInterceptVelocity(ship, target));

    private ShipCommand Reposition(ShipState ship, ShipState target) =>
        IsOverspeedCollisionRisk() ? Flyby(ship, target) : VelocityControl(ship, PlannedInterceptVelocity(ship, target));

    private ShipCommand AttackPosition(ShipState ship)
    {
        var turn = TurnToward(ship, _context.DirectionToTarget);
        bool aligned = MathF.Abs(_context.AimError) < _settings.ThrustAlignmentAngle;
        bool flyby = HasSafeFlybyTrajectory();
        bool main = !flyby && _context.Distance > _settings.PreferredCombatDistance + 40f &&
                    _context.ClosingSpeed < _settings.MaximumAttackRelativeSpeed && aligned;
        bool reverse = !flyby && (_context.Distance < _settings.MinimumCombatDistance + 50f ||
                                  _context.ClosingSpeed > _settings.MaximumAttackRelativeSpeed) && aligned;
        return new ShipCommand(main, reverse, turn.Left, turn.Right);
    }

    private Vector3 PlannedInterceptVelocity(ShipState ship, ShipState target)
    {
        float closingLimit = BrakingLimitedClosingSpeed(ship);
        float travelDistance = MathF.Max(1f, _context.Distance - _settings.PreferredCombatDistance);
        float leadSeconds = MathF.Max(_settings.MinimumInterceptLeadSeconds, travelDistance / MathF.Max(1f, closingLimit));
        Vector3 estimatedTargetPosition = target.Position + target.Velocity * leadSeconds;
        Vector3 toEstimatedTarget = estimatedTargetPosition - ship.Position;
        Vector3 approachDirection = toEstimatedTarget.LengthSquared() > .001f
            ? Vector3.Normalize(toEstimatedTarget) : _context.DirectionToTarget;
        Vector3 arrivalPosition = estimatedTargetPosition - approachDirection * _settings.PreferredCombatDistance;
        return (arrivalPosition - ship.Position) / leadSeconds;
    }

    private float BrakingLimitedClosingSpeed(ShipState ship)
    {
        float availableDistance = MathF.Max(0f, _context.Distance - _settings.MaximumCombatDistance);
        float reverseFactor = ship.Power.PropulsionPowerFactor * ship.Systems.PropulsionCondition;
        float brakingAcceleration = MathF.Max(.01f, _nominalReverseAcceleration * reverseFactor);
        float speedSquared = _settings.MaximumAttackRelativeSpeed * _settings.MaximumAttackRelativeSpeed +
                             2f * brakingAcceleration * availableDistance;
        return MathF.Min(_settings.MaximumApproachClosingSpeed, MathF.Sqrt(speedSquared));
    }

    private bool IsOverspeedCollisionRisk() =>
        _context.ClosingSpeed > _settings.MaximumAttackRelativeSpeed &&
        ClosestApproachDistance() < _settings.FlybySafetyDistanceMeters;

    private bool HasSafeFlybyTrajectory() =>
        _context.RelativeVelocity.Length() > _settings.MaximumAttackRelativeSpeed &&
        ClosestApproachDistance() >= _settings.FlybySafetyDistanceMeters;

    private float ClosestApproachDistance()
    {
        float speedSquared = _context.RelativeVelocity.LengthSquared();
        if (speedSquared < .001f) return _context.Distance;
        float time = -Vector3.Dot(_context.RelativePosition, _context.RelativeVelocity) / speedSquared;
        return time <= 0f ? _context.Distance : (_context.RelativePosition + _context.RelativeVelocity * time).Length();
    }

    private ShipCommand Flyby(ShipState ship, ShipState target)
    {
        Vector3 side = Vector3.Cross(Vector3.UnitY, _context.DirectionToTarget);
        Vector3 ownRelativeVelocity = ship.Velocity - target.Velocity;
        if (Vector3.Dot(ownRelativeVelocity, side) < 0f) side = -side;
        float flybySpeed = MathF.Max(_settings.MaximumAttackRelativeSpeed, ownRelativeVelocity.Length());
        Vector3 desiredVelocity = target.Velocity + side * flybySpeed - _context.DirectionToTarget * 10f;
        return VelocityControl(ship, desiredVelocity);
    }

    private ShipCommand VelocityControl(ShipState ship, Vector3 desiredVelocity)
    {
        Vector3 error = desiredVelocity - ship.Velocity;
        if (error.LengthSquared() < 4f) return TurnTowardCommand(ship, _context.DirectionToTarget);
        Vector3 desiredThrust = Vector3.Normalize(error);
        var turn = TurnToward(ship, desiredThrust);
        float angle = MathF.Abs(SignedPlanarAngle(ship.Forward, desiredThrust));
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
        float error = SignedPlanarAngle(ship.Forward, direction);
        float signal = _settings.RotationKp * error - _settings.RotationKd * ship.AngularVelocity.Y;
        return (signal > _settings.TurnCommandThreshold, signal < -_settings.TurnCommandThreshold);
    }

    private void ChangeState(AutopilotState state)
    {
        if (CurrentState == state) return;
        CurrentState = state;
        TimeInState = 0f;
    }

    private static float SignedPlanarAngle(Vector3 from, Vector3 to)
    {
        Vector3 a = Vector3.Normalize(new Vector3(from.X, 0f, from.Z));
        Vector3 b = Vector3.Normalize(new Vector3(to.X, 0f, to.Z));
        return MathF.Atan2(Vector3.Cross(a, b).Y, Vector3.Dot(a, b));
    }

    private readonly record struct FlightContext(Vector3 RelativePosition, Vector3 DirectionToTarget,
        Vector3 RelativeVelocity, float Distance, float ClosingSpeed, float AimError)
    {
        public static FlightContext Create(ShipState ship, ShipState target)
        {
            Vector3 relativePosition = target.Position - ship.Position;
            float distance = relativePosition.Length();
            Vector3 direction = distance > .0001f ? relativePosition / distance : ship.Forward;
            Vector3 relativeVelocity = target.Velocity - ship.Velocity;
            return new(relativePosition, direction, relativeVelocity, distance,
                -Vector3.Dot(relativeVelocity, direction), SignedPlanarAngle(ship.Forward, direction));
        }
    }
}

public enum AutopilotState
{
    Approach,
    Attack,
    Reposition
}
