using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;

namespace SpaceSim.Core.Generation;

/// <summary>Explains the board computer selected for one generated hull.</summary>
public sealed record BoardComputerLoadoutSelection(
    BoardComputerProfile Profile,
    int TargetMark,
    IReadOnlyList<LoadoutScoreRule> ScoreBreakdown);

/// <summary>
/// Selects the execution-quality computer independently from Kestrel's tactical decisions.
/// The family is fixed by hull class; the Mark reflects how demanding the mounted hardware is.
/// </summary>
public static class BoardComputerLoadoutSelector
{
    public static BoardComputerLoadoutSelection Select(EnemyShipClass shipClass, ShipSubclass subclass, ShipTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(tuning);
        var rules = new List<LoadoutScoreRule>();
        int mark = BaseMark(shipClass, subclass);
        Add(rules, "board_role_base", mark, $"{shipClass} {subclass}: Basis MK {mark}");

        if (tuning.BowWeapon.RangeMeters > 1_200f)
            Adjust(rules, ref mark, "board_long_range_precision", 1, "Langstreckenwaffe über 1.200 m");
        if (tuning.BowWeapon.TurretMaximumAngleDegrees <= 5f)
            Adjust(rules, ref mark, "board_narrow_weapon_arc", 1, "Enges Waffen-Schwenkfenster von höchstens ±5°");
        if (tuning.BowWeapon.Damage >= 36f || tuning.BowWeapon.ChargeSeconds >= 10f)
            Adjust(rules, ref mark, "board_high_commitment_shot", 1, "Schwerer oder langsam ladender Schuss");
        if (tuning.MainBooster.MaximumSpeedMetersPerSecond >= 130f && tuning.ReverseBooster.ThrustNewtons <= 30_000f)
            Adjust(rules, ref mark, "board_fast_main_weak_reverse", 1, "Schneller Main Booster mit schwachem Reverse Booster");
        if (tuning.SideBooster.MaximumRotationDegreesPerSecond <= 10f && tuning.BowWeapon.TurretMaximumAngleDegrees <= 5f)
            Adjust(rules, ref mark, "board_slow_yaw_tight_arc", 1, "Langsame Rotation bei engem Waffen-Schwenkfenster");
        if (tuning.BowWeapon.RangeMeters <= 700f &&
            (tuning.BowWeapon.TurretMaximumAngleDegrees >= 12f || tuning.BowWeapon.ChargeSeconds <= 2.5f))
            Adjust(rules, ref mark, "board_forgiving_close_weapon", -1, "Verzeihende Nahkampfwaffe");
        if (tuning.MainBooster.MaximumSpeedMetersPerSecond <= 100f && tuning.ReverseBooster.ThrustNewtons >= 30_000f)
            Adjust(rules, ref mark, "board_stable_flight_profile", -1, "Stabiles Flugprofil mit brauchbarem Reverse Booster");
        if (tuning.BowWeapon.TurretMaximumAngleDegrees >= 12f && tuning.SideBooster.MaximumRotationDegreesPerSecond >= 15f)
            Adjust(rules, ref mark, "board_wide_arc_fast_yaw", -1, "Breites Waffenfenster mit schneller Rotation");

        (int minimum, int maximum) = MarkRange(shipClass);
        int unclamped = mark;
        mark = Math.Clamp(mark, minimum, maximum);
        if (mark != unclamped)
            Add(rules, "board_mark_clamp", mark - unclamped, $"MK auf Klassenbereich {minimum}-{maximum} begrenzt");

        BoardComputerProfile profile = BoardComputerProfile.Create(Family(shipClass), mark);
        return new BoardComputerLoadoutSelection(profile, mark, rules);
    }

    private static int BaseMark(EnemyShipClass shipClass, ShipSubclass subclass) => (shipClass, subclass) switch
    {
        (EnemyShipClass.Interceptor, ShipSubclass.Ranged) => 2,
        (EnemyShipClass.Interceptor, ShipSubclass.Patrol) => 3,
        (EnemyShipClass.Interceptor, ShipSubclass.Assault) => 4,
        (EnemyShipClass.Corvette, ShipSubclass.Ranged) => 4,
        (EnemyShipClass.Corvette, ShipSubclass.Patrol) => 3,
        (EnemyShipClass.Corvette, ShipSubclass.Assault) => 4,
        (EnemyShipClass.Frigate, ShipSubclass.Ranged) => 4,
        (EnemyShipClass.Frigate, ShipSubclass.Patrol) => 4,
        _ => 5
    };

    private static BoardComputerClass Family(EnemyShipClass shipClass) => shipClass switch
    {
        EnemyShipClass.Interceptor => BoardComputerClass.Standard,
        EnemyShipClass.Corvette => BoardComputerClass.Tactical,
        EnemyShipClass.Frigate => BoardComputerClass.Military,
        _ => throw new ArgumentOutOfRangeException(nameof(shipClass), shipClass, null)
    };

    private static (int Minimum, int Maximum) MarkRange(EnemyShipClass shipClass) => shipClass switch
    {
        EnemyShipClass.Interceptor => (1, 4),
        EnemyShipClass.Corvette => (2, 5),
        EnemyShipClass.Frigate => (3, 5),
        _ => throw new ArgumentOutOfRangeException(nameof(shipClass), shipClass, null)
    };

    private static void Adjust(List<LoadoutScoreRule> rules, ref int mark, string id, int points, string reason)
    {
        mark += points;
        Add(rules, id, points, reason);
    }

    private static void Add(List<LoadoutScoreRule> rules, string id, int points, string reason) => rules.Add(new(id, points, reason));
}
