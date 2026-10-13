using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.Core.Power;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.GodotClient.UI;

/// <summary>Editable 1VS1 manifest. It mirrors the combat-laboratory choices without making the game depend on the tool project.</summary>
internal sealed record DuelLoadoutConfiguration(
    string Name, EnemyShipClass ShipClass, ShipSubclass Subclass, float MassKg, float MaximumHull,
    ReactorType Reactor, BowWeaponType BowWeapon, ShieldType Shield, EnemySensorType Sensor,
    MainBoosterType MainBooster, ReverseBoosterType ReverseBooster, SideBoosterType SideBooster,
    BoardComputerClass BoardComputerClass, int BoardComputerMark);

internal static class DuelLoadoutFactory
{
    private const int CandidateAttempts = 96;
    private static readonly SimulationSettings Settings = new();
    internal static readonly string[] Names =
    [
        "AURORA", "CALYPSO", "DAUNTLESS", "ECLIPSE", "HELIOS", "KESTREL", "MERIDIAN", "NOMAD",
        "ORION", "PEREGRINE", "RESOLUTE", "SOLACE", "VANGUARD", "WRAITH"
    ];

    internal static DuelLoadoutConfiguration FromGenerated(string name, GeneratedShipLoadout loadout) => new(
        name, loadout.ShipClass, loadout.Subclass, loadout.Hull.MassKg, loadout.Hull.MaximumHull,
        loadout.Tuning.Reactor.Type, loadout.Tuning.BowWeapon.Type, loadout.Tuning.Shield.Type, loadout.Sensor.Type,
        loadout.Tuning.MainBooster.Type, loadout.Tuning.ReverseBooster.Type, loadout.Tuning.SideBooster.Type,
        loadout.BoardComputer.Class, loadout.BoardComputerTargetMark);

    internal static DuelLoadoutConfiguration Generate(DuelLoadoutConfiguration? current, ISet<string> locks)
    {
        var random = new Random(Random.Shared.Next());
        for (int attempt = 0; attempt < CandidateAttempts; attempt++)
        {
            EnemyShipClass shipClass = current is not null && locks.Contains("class")
                ? current.ShipClass
                : Enum.GetValues<EnemyShipClass>()[random.Next(Enum.GetValues<EnemyShipClass>().Length)];
            ShipSubclass? subclass = current is not null && locks.Contains("subclass") ? current.Subclass : null;
            GeneratedShipLoadout baseline;
            try { baseline = ShipLoadoutGenerator.Generate(shipClass, random.Next(), subclass); }
            catch (InvalidOperationException) { continue; }

            var candidate = FromBaseline(baseline, current, locks, random);
            if (TryBuild(candidate, out _)) return candidate;
        }

        throw new InvalidOperationException("Die gesperrten Werte ergeben kein gültiges Schiff. Reaktor, Klasse oder Module entsperren.");
    }

    internal static GeneratedShipLoadout Build(DuelLoadoutConfiguration configuration)
    {
        if (TryBuild(configuration, out GeneratedShipLoadout? loadout)) return loadout!;
        throw new InvalidOperationException("Das gewählte Loadout ist ungültig: Der Reaktor muss Klasse und mindestens 80 % der Systemlast abdecken.");
    }

    internal static IReadOnlyList<(float MassKg, float Hull)> HullOptions(EnemyShipClass shipClass)
    {
        ShipClassGenerationProfile profile = ShipClassGenerationProfiles.Get(shipClass);
        return Enumerable.Range(0, 5).Select(index =>
        {
            float fraction = index / 4f;
            return (MathF.Round((profile.MinimumMassTons + (profile.MaximumMassTons - profile.MinimumMassTons) * fraction) * 100f) * 10f,
                MathF.Round(profile.MinimumHull + (profile.MaximumHull - profile.MinimumHull) * fraction));
        }).ToArray();
    }

    private static bool TryBuild(DuelLoadoutConfiguration value, out GeneratedShipLoadout? generated)
    {
        generated = null;
        ShipClassGenerationProfile profile;
        try { profile = ShipClassGenerationProfiles.Get(value.ShipClass); }
        catch (ArgumentOutOfRangeException) { return false; }
        if (string.IsNullOrWhiteSpace(value.Name) || value.BoardComputerMark is < 1 or > 5 ||
            value.MassKg < profile.MinimumMassTons * 1000f - .01f || value.MassKg > profile.MaximumMassTons * 1000f + .01f ||
            value.MaximumHull < profile.MinimumHull - .01f || value.MaximumHull > profile.MaximumHull + .01f)
            return false;

        try
        {
            ReactorDefinition reactor = ReactorDefinitions.Get(value.Reactor);
            BowWeaponDefinition weapon = BowWeaponDefinitions.Get(value.BowWeapon);
            ShieldDefinition shield = ShieldDefinitions.Get(value.Shield);
            EnemySensorDefinition sensor = EnemySensorDefinitions.Get(value.Sensor);
            MainBoosterDefinition main = BoosterDefinitions.MainAll.Single(item => item.Type == value.MainBooster);
            ReverseBoosterDefinition reverse = BoosterDefinitions.ReverseAll.Single(item => item.Type == value.ReverseBooster);
            SideBoosterDefinition side = BoosterDefinitions.SideAll.Single(item => item.Type == value.SideBooster);
            if (reactor.MaximumOutputPower < profile.MinimumReactorOutputPower || reactor.MaximumOutputPower > profile.MaximumReactorOutputPower)
                return false;
            float peak = ShipLoadoutGenerator.PeakPowerDemand(main, reverse, side, weapon, shield, sensor);
            float required = peak * ShipLoadoutGenerator.RequiredPowerCoverage;
            if (reactor.MaximumOutputPower + .001f < required) return false;
            ShipTuning tuning = ShipTuning.From(Settings, weapon, shield, reactor, main, reverse, side);
            BoardComputerProfile computer = BoardComputerProfile.Create(value.BoardComputerClass, value.BoardComputerMark);
            generated = new GeneratedShipLoadout(0, value.ShipClass, value.Subclass, tuning, sensor, peak, required,
                0, [], computer, value.BoardComputerMark, [], new ShipHullLoadoutSelection(value.MassKg, value.MaximumHull, []));
            return true;
        }
        catch (InvalidOperationException) { return false; }
    }

    private static DuelLoadoutConfiguration FromBaseline(GeneratedShipLoadout baseline, DuelLoadoutConfiguration? current,
        ISet<string> locks, Random random)
    {
        bool locked(string name) => current is not null && locks.Contains(name);
        return new DuelLoadoutConfiguration(
            locked("name") ? current!.Name : Names[random.Next(Names.Length)], baseline.ShipClass, baseline.Subclass,
            locked("mass") ? current!.MassKg : baseline.Hull.MassKg,
            locked("hull") ? current!.MaximumHull : baseline.Hull.MaximumHull,
            locked("reactor") ? current!.Reactor : baseline.Tuning.Reactor.Type,
            locked("weapon") ? current!.BowWeapon : baseline.Tuning.BowWeapon.Type,
            locked("shield") ? current!.Shield : baseline.Tuning.Shield.Type,
            locked("sensor") ? current!.Sensor : baseline.Sensor.Type,
            locked("main") ? current!.MainBooster : baseline.Tuning.MainBooster.Type,
            locked("reverse") ? current!.ReverseBooster : baseline.Tuning.ReverseBooster.Type,
            locked("side") ? current!.SideBooster : baseline.Tuning.SideBooster.Type,
            locked("computer") ? current!.BoardComputerClass : baseline.BoardComputer.Class,
            locked("computer") ? current!.BoardComputerMark : baseline.BoardComputerTargetMark);
    }
}
