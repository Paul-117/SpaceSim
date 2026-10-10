using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Power;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.Generation;

/// <summary>Combat role sampled independently from the tactical AI model.</summary>
public enum ShipSubclass { Ranged, Patrol, Assault }

/// <summary>Allowed reactor-output range for one hull class.</summary>
public sealed record ShipClassGenerationProfile(EnemyShipClass ShipClass, float MinimumReactorOutputPower,
    float MaximumReactorOutputPower, float MinimumMassTons, float MaximumMassTons,
    float MinimumHull, float MaximumHull)
{
    internal void Validate()
    {
        if (!float.IsFinite(MinimumReactorOutputPower) || !float.IsFinite(MaximumReactorOutputPower) ||
            MinimumReactorOutputPower <= 0f || MaximumReactorOutputPower < MinimumReactorOutputPower)
            throw new ArgumentOutOfRangeException(nameof(ShipClassGenerationProfile));
        if (!float.IsFinite(MinimumMassTons) || !float.IsFinite(MaximumMassTons) || MinimumMassTons <= 0f ||
            MaximumMassTons < MinimumMassTons || !float.IsFinite(MinimumHull) || !float.IsFinite(MaximumHull) ||
            MinimumHull <= 0f || MaximumHull < MinimumHull)
            throw new ArgumentOutOfRangeException(nameof(ShipClassGenerationProfile));
    }
}

/// <summary>Central hull classes supported by the procedural loadout generator.</summary>
public static class ShipClassGenerationProfiles
{
    public static ShipClassGenerationProfile Interceptor { get; } = new(EnemyShipClass.Interceptor, 90f, 120f, 5f, 8f, 10f, 25f);
    public static ShipClassGenerationProfile Corvette { get; } = new(EnemyShipClass.Corvette, 110f, 150f, 7f, 15f, 20f, 35f);
    public static ShipClassGenerationProfile Frigate { get; } = new(EnemyShipClass.Frigate, 150f, 250f, 13f, 20f, 30f, 45f);

    public static ShipClassGenerationProfile Get(EnemyShipClass shipClass) => shipClass switch
    {
        EnemyShipClass.Interceptor => Interceptor,
        EnemyShipClass.Corvette => Corvette,
        EnemyShipClass.Frigate => Frigate,
        _ => throw new ArgumentOutOfRangeException(nameof(shipClass), shipClass, "No generation profile is configured.")
    };
}

/// <summary>One transparent soft-rule contribution retained with a generated loadout.</summary>
public sealed record LoadoutScoreRule(string Id, int Points, string Reason);

/// <summary>A valid generated ship configuration. The caller may pass Tuning and Sensor directly to an enemy factory.</summary>
public sealed record GeneratedShipLoadout(
    int Seed,
    EnemyShipClass ShipClass,
    ShipSubclass Subclass,
    ShipTuning Tuning,
    EnemySensorDefinition Sensor,
    float PeakPowerDemand,
    float RequiredReactorOutputAtEightyPercent,
    int Score,
    IReadOnlyList<LoadoutScoreRule> ScoreBreakdown,
    BoardComputerProfile BoardComputer,
    int BoardComputerTargetMark,
    IReadOnlyList<LoadoutScoreRule> BoardComputerScoreBreakdown,
    ShipHullLoadoutSelection Hull)
{
    public bool MeetsHardPowerRule => Tuning.Reactor.MaximumOutputPower + .001f >= RequiredReactorOutputAtEightyPercent;
}

/// <summary>
/// Deterministic generator for complete ship loadouts. It samples a reactor inside the hull range,
/// samples a role-weighted module set, rejects hard-invalid candidates, then keeps the best score.
/// </summary>
public static class ShipLoadoutGenerator
{
    public const float RequiredPowerCoverage = .80f;
    private const int CandidateAttempts = 48;
    private static readonly SimulationSettings TuningSettings = new();

    public static GeneratedShipLoadout GenerateCorvette(int seed, ShipSubclass? forcedSubclass = null) =>
        Generate(EnemyShipClass.Corvette, seed, forcedSubclass);

    public static GeneratedShipLoadout Generate(EnemyShipClass shipClass, int seed, ShipSubclass? forcedSubclass = null)
    {
        ShipClassGenerationProfile profile = ShipClassGenerationProfiles.Get(shipClass);
        profile.Validate();
        var random = new Random(seed);
        GeneratedShipLoadout? best = null;

        for (int attempt = 0; attempt < CandidateAttempts; attempt++)
        {
            ShipSubclass subclass = forcedSubclass ?? SampleSubclass(random);
            ReactorDefinition reactor = Pick(random, ReactorDefinitions.All.Where(reactor =>
                reactor.MaximumOutputPower >= profile.MinimumReactorOutputPower &&
                reactor.MaximumOutputPower <= profile.MaximumReactorOutputPower).ToArray(), _ => 1);
            BowWeaponDefinition weapon = Pick(random, BowWeaponDefinitions.All, weapon => WeaponWeight(subclass, weapon));
            ShieldDefinition shield = Pick(random, ShieldDefinitions.All, shield => ShieldWeight(subclass, shield));
            EnemySensorDefinition sensor = Pick(random, EnemySensorDefinitions.All, sensor => SensorWeight(subclass, sensor));
            MainBoosterDefinition main = Pick(random, BoosterDefinitions.MainAll, main => MainWeight(subclass, main));
            ReverseBoosterDefinition reverse = Pick(random, BoosterDefinitions.ReverseAll, reverse => ReverseWeight(subclass, reverse));
            SideBoosterDefinition side = Pick(random, BoosterDefinitions.SideAll, side => SideWeight(subclass, side));

            float peakDemand = PeakPowerDemand(main, reverse, side, weapon, shield, sensor);
            float requiredOutput = peakDemand * RequiredPowerCoverage;
            if (reactor.MaximumOutputPower + .001f < requiredOutput) continue;

            ShipTuning tuning = ShipTuning.From(TuningSettings, weapon, shield, reactor, main, reverse, side);
            tuning.Validate();
            var rules = Score(subclass, tuning, sensor, peakDemand);
            BoardComputerLoadoutSelection boardComputer = BoardComputerLoadoutSelector.Select(shipClass, subclass, tuning);
            ShipHullLoadoutSelection hull = ShipHullLoadoutSelector.Select(profile, subclass, tuning);
            var candidate = new GeneratedShipLoadout(seed, shipClass, subclass, tuning, sensor, peakDemand, requiredOutput,
                rules.Sum(rule => rule.Points), rules, boardComputer.Profile, boardComputer.TargetMark,
                boardComputer.ScoreBreakdown, hull);
            if (best is null || candidate.Score > best.Score) best = candidate;
        }

        return best ?? throw new InvalidOperationException(
            $"No {shipClass} loadout satisfies the {RequiredPowerCoverage:P0} reactor coverage rule.");
    }

    /// <summary>Maximum simultaneous station demand under the current shared-auxiliary-reserve model.</summary>
    public static float PeakPowerDemand(MainBoosterDefinition main, ReverseBoosterDefinition reverse,
        SideBoosterDefinition side, BowWeaponDefinition weapon, ShieldDefinition shield, EnemySensorDefinition sensor) =>
        main.PowerDraw + Math.Max(reverse.PowerDraw, side.PowerDraw) + weapon.PowerDraw + shield.PowerDraw + sensor.PowerUsage;

    private static ShipSubclass SampleSubclass(Random random) => (ShipSubclass)random.Next(0, 3);

    private static int WeaponWeight(ShipSubclass subclass, BowWeaponDefinition weapon) => subclass switch
    {
        ShipSubclass.Ranged => DetermineRangeBand(weapon) switch { RangeBand.Long => 10, RangeBand.Medium => 3, _ => 1 },
        ShipSubclass.Assault => DetermineRangeBand(weapon) switch { RangeBand.Close => 10, RangeBand.Medium => 3, _ => 1 },
        _ => DetermineRangeBand(weapon) == RangeBand.Medium ? 8 : 4
    };

    private static int ShieldWeight(ShipSubclass subclass, ShieldDefinition shield) => subclass switch
    {
        ShipSubclass.Assault when shield.MaximumHitPoints >= 30f => 8,
        ShipSubclass.Ranged when shield.RechargeSeconds <= 5f => 5,
        ShipSubclass.Patrol when shield.MaximumHitPoints is >= 18f and <= 35f => 7,
        _ => 3
    };

    private static int SensorWeight(ShipSubclass subclass, EnemySensorDefinition sensor) => subclass switch
    {
        ShipSubclass.Ranged when sensor.MaximumRangeMeters >= 2_500f => 9,
        ShipSubclass.Assault when sensor.PowerUsage <= 18f => 7,
        ShipSubclass.Patrol when sensor.MaximumRangeMeters is >= 1_800f and <= 2_700f => 7,
        _ => 3
    };

    private static int MainWeight(ShipSubclass subclass, MainBoosterDefinition main) => subclass switch
    {
        ShipSubclass.Ranged when main.MaximumSpeedMetersPerSecond <= 120f => 7,
        ShipSubclass.Assault when main.MaximumSpeedMetersPerSecond >= 120f || main.ThrustNewtons >= 150_000f => 9,
        ShipSubclass.Patrol when main.MaximumSpeedMetersPerSecond is >= 100f and <= 130f => 7,
        _ => 3
    };

    private static int ReverseWeight(ShipSubclass subclass, ReverseBoosterDefinition reverse) => subclass switch
    {
        ShipSubclass.Assault when reverse.ThrustNewtons >= 30_000f => 8,
        ShipSubclass.Ranged when reverse.PowerDraw <= 30f => 6,
        _ => 4
    };

    private static int SideWeight(ShipSubclass subclass, SideBoosterDefinition side) => subclass switch
    {
        ShipSubclass.Assault when side.MaximumRotationDegreesPerSecond >= 12f => 10,
        ShipSubclass.Ranged when side.MaximumRotationDegreesPerSecond <= 10f => 6,
        ShipSubclass.Patrol when side.MaximumRotationDegreesPerSecond is >= 7f and <= 12f => 7,
        _ => 3
    };

    private static IReadOnlyList<LoadoutScoreRule> Score(ShipSubclass subclass, ShipTuning tuning,
        EnemySensorDefinition sensor, float peakDemand)
    {
        var rules = new List<LoadoutScoreRule>();
        Add(rules, "subclass_sampled", 0, $"Subklasse: {subclass}");
        RangeBand range = DetermineRangeBand(tuning.BowWeapon);
        bool weakSensor = sensor.MaximumRangeMeters <= 1_800f;
        bool close = range == RangeBand.Close;
        bool longRange = range == RangeBand.Long;

        if (longRange && weakSensor) Add(rules, "long_range_weak_sensor", -3, "Langstreckenwaffe mit schwachem Sensor");
        if (longRange && sensor.MaximumRangeMeters >= 2_500f) Add(rules, "long_range_strong_sensor", 2, "Langstreckenwaffe mit starkem Sensor");
        if (close && tuning.MainBooster.MaximumSpeedMetersPerSecond >= 120f) Add(rules, "close_fast_main", 2, "Nahkampfwaffe mit hoher Geschwindigkeit");
        if (close && tuning.SideBooster.MaximumRotationDegreesPerSecond >= 12f) Add(rules, "close_fast_yaw", 2, "Nahkampfwaffe mit hoher Schwenkrate");
        if (tuning.BowWeapon.PowerDraw >= 50f && tuning.Reactor.MaximumOutputPower <= 125f)
            Add(rules, "high_weapon_low_reactor", -3, "Hohe Waffen-PU mit schwachem Reaktor");
        if (tuning.BowWeapon.Damage >= 36f && tuning.Shield.MaximumHitPoints >= 40f)
            Add(rules, "heavy_weapon_heavy_shield", -2, "Schwere Waffe mit hoher Schildkapazitaet");
        if (tuning.ReverseBooster.ThrustNewtons <= 30_000f && tuning.MainBooster.MaximumSpeedMetersPerSecond >= 130f)
            Add(rules, "fast_main_weak_reverse", -2, "Schneller Main Booster mit schwachem Reverse Booster");
        if (tuning.BowWeapon.TurretMaximumAngleDegrees <= 5f && tuning.SideBooster.MaximumRotationDegreesPerSecond >= 15f)
            Add(rules, "tight_arc_fast_yaw", 2, "Enger Waffenwinkel mit schneller Schiffsdrehung");
        if (peakDemand <= 145f && tuning.Reactor.MaximumFuelUsagePerMinute <= 5.5f)
            Add(rules, "efficient_loadout", 2, "Sparsame Module mit effizientem Reaktor");

        switch (subclass)
        {
            case ShipSubclass.Ranged when longRange:
                Add(rules, "ranged_role", 3, "Ranged-Subklasse mit Langstreckenwaffe");
                break;
            case ShipSubclass.Patrol when range == RangeBand.Medium:
                Add(rules, "patrol_role", 3, "Patrol-Subklasse mit ausgewogener Waffe");
                break;
            case ShipSubclass.Assault when close:
                Add(rules, "assault_role", 3, "Assault-Subklasse mit Nahkampfwaffe");
                break;
        }
        return rules;
    }

    private static void Add(List<LoadoutScoreRule> rules, string id, int points, string reason) => rules.Add(new(id, points, reason));

    private static T Pick<T>(Random random, IReadOnlyList<T> candidates, Func<T, int> weight)
    {
        if (candidates.Count == 0) throw new InvalidOperationException("No compatible module candidates are available.");
        int total = candidates.Sum(candidate => Math.Max(1, weight(candidate)));
        int roll = random.Next(total);
        foreach (T candidate in candidates)
        {
            roll -= Math.Max(1, weight(candidate));
            if (roll < 0) return candidate;
        }
        return candidates[^1];
    }

    private enum RangeBand { Close, Medium, Long }
    private static RangeBand DetermineRangeBand(BowWeaponDefinition weapon) => weapon.RangeMeters <= 700f ? RangeBand.Close :
        weapon.RangeMeters <= 1_200f ? RangeBand.Medium : RangeBand.Long;
}
