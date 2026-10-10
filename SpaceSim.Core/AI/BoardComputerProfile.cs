using SpaceSim.Core.Ships;

namespace SpaceSim.Core.AI;

/// <summary>Quality family for the control computer that executes Kestrel's tactical decisions.</summary>
public enum BoardComputerClass { Civilian, Standard, Tactical, Military }

/// <summary>
/// A deterministic control-quality profile. It never changes the authoritative world state;
/// it only delays and imperfectly scales the normal ShipCommands produced by Kestrel.
/// </summary>
public sealed record BoardComputerProfile(
    BoardComputerClass Class,
    int Mark,
    int CommandUpdateIntervalTicks,
    int ReactionDelayTicks,
    float FireAimToleranceMultiplier,
    float ThrustCalibrationErrorFraction,
    float YawAuthorityFactor)
{
    public string Name => $"{Class.ToString().ToUpperInvariant()} MK {ToRoman(Mark)}";

    public static BoardComputerProfile Create(BoardComputerClass @class, int mark)
    {
        mark = Math.Clamp(mark, 1, 5);
        float quality = (mark - 1) / 4f;
        return @class switch
        {
            // Civilian Mk IV can manage speed reasonably well, but still fires too early.
            BoardComputerClass.Civilian => new(@class, mark,
                RoundTicks(Lerp(6f, 2f, quality)), RoundDelayTicks(Lerp(12f, 3f, quality)),
                Lerp(2.7f, 1.8f, quality), Lerp(.30f, .08f, quality), Lerp(.55f, .88f, quality)),
            // Balanced all-rounder.
            BoardComputerClass.Standard => new(@class, mark,
                RoundTicks(Lerp(4f, 1f, quality)), RoundDelayTicks(Lerp(5f, 0f, quality)),
                Lerp(1.5f, 1f, quality), Lerp(.16f, .02f, quality), Lerp(.70f, .98f, quality)),
            // Better weapon discipline, deliberately a little less agile than Standard.
            BoardComputerClass.Tactical => new(@class, mark,
                RoundTicks(Lerp(5f, 2f, quality)), RoundDelayTicks(Lerp(7f, 1f, quality)),
                Lerp(.80f, .50f, quality), Lerp(.20f, .06f, quality), Lerp(.62f, .90f, quality)),
            // Military Mk V is the unfiltered Kestrel baseline.
            _ => new(@class, mark,
                RoundTicks(Lerp(3f, 1f, quality)), RoundDelayTicks(Lerp(3f, 0f, quality)),
                Lerp(1.20f, 1f, quality), Lerp(.08f, 0f, quality), Lerp(.82f, 1f, quality))
        };
    }

    public static BoardComputerProfile CivilianMk4 { get; } = Create(BoardComputerClass.Civilian, 4);
    public static BoardComputerProfile StandardMk3 { get; } = Create(BoardComputerClass.Standard, 3);
    public static BoardComputerProfile TacticalMk3 { get; } = Create(BoardComputerClass.Tactical, 3);
    public static BoardComputerProfile TacticalMk4 { get; } = Create(BoardComputerClass.Tactical, 4);
    public static BoardComputerProfile MilitaryMk4 { get; } = Create(BoardComputerClass.Military, 4);
    public static BoardComputerProfile MilitaryMk5 { get; } = Create(BoardComputerClass.Military, 5);

    internal void Validate()
    {
        if (Mark is < 1 or > 5) throw new ArgumentOutOfRangeException(nameof(Mark));
        if (CommandUpdateIntervalTicks <= 0 || ReactionDelayTicks < 0)
            throw new ArgumentOutOfRangeException(nameof(CommandUpdateIntervalTicks));
        foreach (float value in new[] { FireAimToleranceMultiplier, YawAuthorityFactor })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(value));
        if (!float.IsFinite(ThrustCalibrationErrorFraction) || ThrustCalibrationErrorFraction < 0f || ThrustCalibrationErrorFraction >= 1f)
            throw new ArgumentOutOfRangeException(nameof(ThrustCalibrationErrorFraction));
    }

    private static float Lerp(float from, float to, float fraction) => from + (to - from) * fraction;
    private static int RoundTicks(float value) => Math.Max(1, (int)MathF.Round(value));
    private static int RoundDelayTicks(float value) => Math.Max(0, (int)MathF.Round(value));
    private static string ToRoman(int value) => value switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", _ => "V" };
}

/// <summary>Central selection of the board computers used by normal encounters and direct duels.</summary>
public sealed record BoardComputerSettings
{
    public BoardComputerProfile EasyEnemy { get; init; } = BoardComputerProfile.StandardMk3;
    public BoardComputerProfile MediumEnemy { get; init; } = BoardComputerProfile.TacticalMk3;
    public BoardComputerProfile HardEnemy { get; init; } = BoardComputerProfile.MilitaryMk4;
    public BoardComputerProfile Duel { get; init; } = BoardComputerProfile.MilitaryMk5;

    public BoardComputerProfile For(EnemyDifficulty difficulty) => difficulty switch
    {
        EnemyDifficulty.Easy => EasyEnemy,
        EnemyDifficulty.Hard => HardEnemy,
        _ => MediumEnemy
    };

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(EasyEnemy); ArgumentNullException.ThrowIfNull(MediumEnemy);
        ArgumentNullException.ThrowIfNull(HardEnemy); ArgumentNullException.ThrowIfNull(Duel);
        EasyEnemy.Validate(); MediumEnemy.Validate(); HardEnemy.Validate(); Duel.Validate();
    }
}

/// <summary>Command-only adapter between Kestrel and shared ship physics.</summary>
internal sealed class BoardComputerCommandProcessor
{
    private readonly BoardComputerProfile _profile;
    private readonly Random _random;
    private readonly Queue<(int DueTick, ShipCommand Command)> _movementQueue = new();
    private readonly Queue<int> _fireQueue = new();
    private ShipCommand _heldMovement;
    private int _tick;

    public BoardComputerCommandProcessor(BoardComputerProfile profile, int seed)
    {
        _profile = profile;
        _random = new Random(seed + 71_029);
    }

    public ShipCommand Apply(ShipCommand command)
    {
        if (_tick % _profile.CommandUpdateIntervalTicks == 0)
        {
            int due = _tick + _profile.ReactionDelayTicks;
            _movementQueue.Enqueue((due, Distort(command) with { FireLance = false }));
            if (command.FireLance) _fireQueue.Enqueue(due);
        }

        while (_movementQueue.Count > 0 && _movementQueue.Peek().DueTick <= _tick)
            _heldMovement = _movementQueue.Dequeue().Command;

        bool fire = false;
        while (_fireQueue.Count > 0 && _fireQueue.Peek() <= _tick)
        {
            _fireQueue.Dequeue();
            fire = true;
        }
        _tick++;
        return _heldMovement with { FireLance = fire };
    }

    private ShipCommand Distort(ShipCommand command)
    {
        float calibration = 1f + ((_random.NextSingle() * 2f) - 1f) * _profile.ThrustCalibrationErrorFraction;
        return command with
        {
            MainThrustIntensity = command.MainThrust ? Math.Clamp(command.MainThrustIntensity * calibration, 0f, 1f) : 0f,
            ReverseThrustIntensity = command.ReverseThrust ? Math.Clamp(command.ReverseThrustIntensity * calibration, 0f, 1f) : 0f,
            YawIntensity = (command.YawLeft || command.YawRight) ? Math.Clamp(command.YawIntensity * _profile.YawAuthorityFactor, 0f, 1f) : 0f
        };
    }
}
