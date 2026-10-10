using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.Core.Power;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.GefechtsSimulation;

/// <summary>Serializable ship manifest used by the combat-laboratory editor and saved sessions.</summary>
public sealed record CombatShipLoadout(
    string Name,
    EnemyShipClass ShipClass,
    ShipSubclass Subclass,
    float MassKg,
    float MaximumHull,
    ReactorType Reactor,
    BowWeaponType BowWeapon,
    ShieldType Shield,
    EnemySensorType Sensor,
    MainBoosterType MainBooster,
    ReverseBoosterType ReverseBooster,
    SideBoosterType SideBooster,
    BoardComputerClass BoardComputerClass,
    int BoardComputerMark);

/// <summary>Current editor values plus the fields that must survive the next random generation.</summary>
public sealed record CombatShipGenerationRequest(
    CombatShipLoadout? Current = null,
    IReadOnlyList<string>? LockedFields = null,
    int? Seed = null);

public sealed record CombatCatalogOption(int Value, string Label);
public sealed record CombatHullOption(EnemyShipClass ShipClass, float MassKg, float MaximumHull, string Label);
public sealed record CombatShipCatalog(
    IReadOnlyList<string> Names,
    IReadOnlyList<CombatCatalogOption> Classes,
    IReadOnlyList<CombatCatalogOption> Subclasses,
    IReadOnlyList<CombatCatalogOption> Reactors,
    IReadOnlyList<CombatCatalogOption> BowWeapons,
    IReadOnlyList<CombatCatalogOption> Shields,
    IReadOnlyList<CombatCatalogOption> Sensors,
    IReadOnlyList<CombatCatalogOption> MainBoosters,
    IReadOnlyList<CombatCatalogOption> ReverseBoosters,
    IReadOnlyList<CombatCatalogOption> SideBoosters,
    IReadOnlyList<CombatCatalogOption> BoardComputerClasses,
    IReadOnlyList<int> BoardComputerMarks,
    IReadOnlyList<CombatHullOption> HullOptions);

/// <summary>
/// Adapter between the general loadout generator and the laboratory's per-row locks. The core generator
/// still owns all weighted sampling; this adapter retains explicitly locked rows and rejects combinations
/// that violate a hull's reactor range or the 80-percent simultaneous-power rule.
/// </summary>
public static class CombatShipLoadoutFactory
{
    private const int CandidateAttempts = 96;
    private static readonly SimulationSettings TuningSettings = new();
    private static readonly string[] ShipNames =
    [
        "AURORA", "CALYPSO", "DAUNTLESS", "ECLIPSE", "HELIOS", "KESTREL", "MERIDIAN", "NOMAD",
        "ORION", "PEREGRINE", "RESOLUTE", "SOLACE", "VANGUARD", "WRAITH"
    ];

    public static CombatShipCatalog Catalog() => new(
        ShipNames,
        EnumOptions<EnemyShipClass>(value => value.ToString().ToUpperInvariant()),
        EnumOptions<ShipSubclass>(value => value.ToString().ToUpperInvariant()),
        ReactorDefinitions.All.Select(value => Option(value.Type, $"{value.Name} · {value.MaximumOutputPower:0} PU")).ToArray(),
        BowWeaponDefinitions.All.Select(value => Option(value.Type, $"{value.Name} · {value.Damage:0} DMG · {value.RangeMeters:0} m · {value.ChargeSeconds:0.0} s")).ToArray(),
        ShieldDefinitions.All.Select(value => Option(value.Type, $"{value.Name} · {value.MaximumHitPoints:0} HP · {value.RechargeSeconds:0.0} s")).ToArray(),
        EnemySensorDefinitions.All.Select(value => Option(value.Type, $"{value.Name} · {value.MinimumRangeMeters:0}-{value.MaximumRangeMeters:0} m")).ToArray(),
        BoosterDefinitions.MainAll.Select(value => Option(value.Type, $"{value.Name} · {value.ThrustNewtons / 1000f:0} kN · {value.MaximumSpeedMetersPerSecond:0} m/s")).ToArray(),
        BoosterDefinitions.ReverseAll.Select(value => Option(value.Type, $"{value.Name} · {value.ThrustNewtons / 1000f:0} kN · {value.MaximumSpeedMetersPerSecond:0} m/s")).ToArray(),
        BoosterDefinitions.SideAll.Select(value => Option(value.Type, $"{value.Name} · {value.ThrustNewtons / 1000f:0.0} kN · {value.MaximumRotationDegreesPerSecond:0}°/s")).ToArray(),
        EnumOptions<BoardComputerClass>(value => value.ToString().ToUpperInvariant()),
        [1, 2, 3, 4, 5],
        Enum.GetValues<EnemyShipClass>().SelectMany(HullOptions).ToArray());

    public static CombatShipLoadout Generate(CombatShipGenerationRequest? request = null)
    {
        CombatShipLoadout? current = request?.Current;
        var locks = new HashSet<string>(request?.LockedFields ?? [], StringComparer.OrdinalIgnoreCase);
        int seed = request?.Seed ?? Random.Shared.Next(1, int.MaxValue - CandidateAttempts - 1);
        var random = new Random(seed);

        for (int attempt = 0; attempt < CandidateAttempts; attempt++)
        {
            EnemyShipClass shipClass = Locked("class") && current is not null
                ? current.ShipClass
                : Enum.GetValues<EnemyShipClass>()[random.Next(Enum.GetValues<EnemyShipClass>().Length)];
            ShipSubclass? subclass = Locked("subclass") && current is not null ? current.Subclass : null;
            GeneratedShipLoadout baseline;
            try
            {
                baseline = ShipLoadoutGenerator.Generate(shipClass, random.Next(), subclass);
            }
            catch (InvalidOperationException)
            {
                // A sampled class can have no valid weighted candidate for this seed. Try the next
                // deterministic candidate instead of failing the editor's Generate button.
                continue;
            }
            CombatShipLoadout candidate = FromBaseline(baseline, current, locks, random);
            if (TryBuild(candidate, out _)) return candidate;
        }

        throw new InvalidOperationException("Die gesperrten Einträge ergeben kein gültiges Schiff: Der Reaktor muss alle Systeme mit mindestens 80 % versorgen und zur gewählten Klasse passen.");

        bool Locked(string field) => locks.Contains(field);
    }

    public static GeneratedShipLoadout Build(CombatShipLoadout loadout)
    {
        if (!TryBuild(loadout, out GeneratedShipLoadout? generated, out string? error))
            throw new InvalidOperationException(error);
        return generated!;
    }

    public static bool TryBuild(CombatShipLoadout loadout, out GeneratedShipLoadout? generated) =>
        TryBuild(loadout, out generated, out _);

    private static bool TryBuild(CombatShipLoadout loadout, out GeneratedShipLoadout? generated, out string? error)
    {
        generated = null;
        error = null;
        if (string.IsNullOrWhiteSpace(loadout.Name)) { error = "Ein Schiffsname fehlt."; return false; }
        if (loadout.BoardComputerMark is < 1 or > 5) { error = "Boardcomputer-MK muss zwischen I und V liegen."; return false; }

        ShipClassGenerationProfile profile;
        try { profile = ShipClassGenerationProfiles.Get(loadout.ShipClass); }
        catch (ArgumentOutOfRangeException) { error = "Die Schiffsklasse ist ungültig."; return false; }

        if (loadout.MassKg < profile.MinimumMassTons * 1000f - .01f || loadout.MassKg > profile.MaximumMassTons * 1000f + .01f ||
            loadout.MaximumHull < profile.MinimumHull - .01f || loadout.MaximumHull > profile.MaximumHull + .01f)
        {
            error = $"Masse und Hülle müssen innerhalb der Grenzen der Klasse {loadout.ShipClass} liegen.";
            return false;
        }

        ReactorDefinition reactor;
        BowWeaponDefinition weapon;
        ShieldDefinition shield;
        EnemySensorDefinition sensor;
        MainBoosterDefinition main;
        ReverseBoosterDefinition reverse;
        SideBoosterDefinition side;
        try
        {
            reactor = ReactorDefinitions.Get(loadout.Reactor);
            weapon = BowWeaponDefinitions.Get(loadout.BowWeapon);
            shield = ShieldDefinitions.Get(loadout.Shield);
            sensor = EnemySensorDefinitions.Get(loadout.Sensor);
            main = BoosterDefinitions.MainAll.Single(item => item.Type == loadout.MainBooster);
            reverse = BoosterDefinitions.ReverseAll.Single(item => item.Type == loadout.ReverseBooster);
            side = BoosterDefinitions.SideAll.Single(item => item.Type == loadout.SideBooster);
        }
        catch (InvalidOperationException)
        {
            error = "Mindestens ein ausgewähltes Modul existiert nicht mehr im Katalog.";
            return false;
        }

        if (reactor.MaximumOutputPower < profile.MinimumReactorOutputPower || reactor.MaximumOutputPower > profile.MaximumReactorOutputPower)
        {
            error = $"{reactor.Name} liegt außerhalb der Reaktorgrenze von {loadout.ShipClass} ({profile.MinimumReactorOutputPower:0}-{profile.MaximumReactorOutputPower:0} PU).";
            return false;
        }

        float peakDemand = ShipLoadoutGenerator.PeakPowerDemand(main, reverse, side, weapon, shield, sensor);
        float requiredOutput = peakDemand * ShipLoadoutGenerator.RequiredPowerCoverage;
        if (reactor.MaximumOutputPower + .001f < requiredOutput)
        {
            error = $"{reactor.Name} liefert {reactor.MaximumOutputPower:0} PU, benötigt werden mindestens {requiredOutput:0} PU für 80 % Systemabdeckung.";
            return false;
        }

        ShipTuning tuning = ShipTuning.From(TuningSettings, weapon, shield, reactor, main, reverse, side);
        BoardComputerProfile board = BoardComputerProfile.Create(loadout.BoardComputerClass, loadout.BoardComputerMark);
        generated = new GeneratedShipLoadout(0, loadout.ShipClass, loadout.Subclass, tuning, sensor, peakDemand, requiredOutput,
            0, [], board, loadout.BoardComputerMark, [], new ShipHullLoadoutSelection(loadout.MassKg, loadout.MaximumHull, []));
        return true;
    }

    private static CombatShipLoadout FromBaseline(GeneratedShipLoadout baseline, CombatShipLoadout? current,
        HashSet<string> locks, Random random)
    {
        bool locked(string field) => current is not null && locks.Contains(field);
        ShipHullLoadoutSelection hull = baseline.Hull;
        return new CombatShipLoadout(
            locked("name") ? current!.Name : ShipNames[random.Next(ShipNames.Length)],
            baseline.ShipClass,
            baseline.Subclass,
            locked("mass") ? current!.MassKg : hull.MassKg,
            locked("hull") ? current!.MaximumHull : hull.MaximumHull,
            locked("reactor") ? current!.Reactor : baseline.Tuning.Reactor.Type,
            locked("weapon") ? current!.BowWeapon : baseline.Tuning.BowWeapon.Type,
            locked("shield") ? current!.Shield : baseline.Tuning.Shield.Type,
            locked("sensor") ? current!.Sensor : baseline.Sensor.Type,
            locked("mainBooster") ? current!.MainBooster : baseline.Tuning.MainBooster.Type,
            locked("reverseBooster") ? current!.ReverseBooster : baseline.Tuning.ReverseBooster.Type,
            locked("sideBooster") ? current!.SideBooster : baseline.Tuning.SideBooster.Type,
            locked("boardComputer") ? current!.BoardComputerClass : baseline.BoardComputer.Class,
            locked("boardComputer") ? current!.BoardComputerMark : baseline.BoardComputerTargetMark);
    }

    private static IEnumerable<CombatHullOption> HullOptions(EnemyShipClass shipClass)
    {
        ShipClassGenerationProfile profile = ShipClassGenerationProfiles.Get(shipClass);
        for (int tier = 0; tier <= 4; tier++)
        {
            float fraction = tier / 4f;
            float mass = (profile.MinimumMassTons + (profile.MaximumMassTons - profile.MinimumMassTons) * fraction) * 1000f;
            float hull = MathF.Round(profile.MinimumHull + (profile.MaximumHull - profile.MinimumHull) * fraction);
            yield return new CombatHullOption(shipClass, mass, hull, $"{mass / 1000f:0.00} t");
        }
    }

    private static CombatCatalogOption[] EnumOptions<T>(Func<T, string> label) where T : struct, Enum =>
        Enum.GetValues<T>().Select(value => Option(value, label(value))).ToArray();

    private static CombatCatalogOption Option<T>(T value, string label) where T : struct, Enum =>
        new(Convert.ToInt32(value), label);
}
