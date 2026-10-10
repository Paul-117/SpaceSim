using SpaceSim.Core.Ships;

namespace SpaceSim.Core.Generation;

/// <summary>Mass and structural integrity selected for one generated hull.</summary>
public sealed record ShipHullLoadoutSelection(
    float MassKg,
    float MaximumHull,
    IReadOnlyList<LoadoutScoreRule> ScoreBreakdown);

/// <summary>
/// Converts class limits, combat role and installed hardware into physical mass and hull integrity.
/// Mass changes only linear acceleration; hull integrity changes the actual damage capacity.
/// </summary>
public static class ShipHullLoadoutSelector
{
    private const int Tiers = 4;

    public static ShipHullLoadoutSelection Select(ShipClassGenerationProfile profile, ShipSubclass subclass, ShipTuning tuning)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(tuning);
        profile.Validate();
        var rules = new List<LoadoutScoreRule>();

        int massTier = RoleTier(subclass);
        Add(rules, "mass_role_base", massTier, $"{subclass}: Basis-Masseklasse {massTier}");
        if (tuning.Shield.MaximumHitPoints >= 35f)
            Adjust(rules, ref massTier, "mass_heavy_shield", 1, "Schildgenerator mit mindestens 35 HP");
        if (tuning.BowWeapon.Damage >= 28f)
            Adjust(rules, ref massTier, "mass_heavy_weapon", 1, "Bugwaffe mit mindestens 28 Schaden");
        if (tuning.Reactor.MaximumOutputPower >= 175f)
            Adjust(rules, ref massTier, "mass_heavy_reactor", 1, "Reaktor mit mindestens 175 PU");
        if (tuning.MainBooster.ThrustNewtons >= 150_000f)
            Adjust(rules, ref massTier, "mass_heavy_main_booster", 1, "Main Booster mit mindestens 150 kN");
        if (tuning.BowWeapon.RangeMeters >= 1_400f)
            Adjust(rules, ref massTier, "mass_long_range_lightweight", -1, "Langstreckenwaffe ab 1.400 m");
        if (tuning.MainBooster.MaximumSpeedMetersPerSecond >= 130f)
            Adjust(rules, ref massTier, "mass_fast_main_lightweight", -1, "Main-Speed ab 130 m/s");
        if (tuning.Shield.MaximumHitPoints <= 18f)
            Adjust(rules, ref massTier, "mass_light_shield", -1, "Leichter Schildgenerator mit höchstens 18 HP");

        int hullTier = RoleTier(subclass);
        Add(rules, "hull_role_base", hullTier, $"{subclass}: Basis-Hüllenklasse {hullTier}");
        if (tuning.Shield.MaximumHitPoints >= 35f)
            Adjust(rules, ref hullTier, "hull_heavy_shield", 1, "Schwerer Schildgenerator unterstützt ein robustes Schiff");
        if (tuning.BowWeapon.Damage >= 28f)
            Adjust(rules, ref hullTier, "hull_heavy_weapon", 1, "Schwere Bugwaffe benötigt einen robusten Rumpf");
        if (massTier >= 3)
            Adjust(rules, ref hullTier, "hull_heavy_mass", 1, "Hohe gewählte Masse erhöht die strukturelle Reserve");
        if (tuning.Shield.MaximumHitPoints <= 15f)
            Adjust(rules, ref hullTier, "hull_fragile_shield", -1, "Leichter Schildgenerator");
        if (tuning.MainBooster.MaximumSpeedMetersPerSecond >= 130f)
            Adjust(rules, ref hullTier, "hull_fast_main", -1, "Sehr schneller Main Booster bevorzugt einen leichteren Rumpf");
        if (tuning.BowWeapon.RangeMeters >= 1_400f)
            Adjust(rules, ref hullTier, "hull_long_range", -1, "Langstreckenrolle bevorzugt Distanz statt Panzerung");

        float massTons = Interpolate(profile.MinimumMassTons, profile.MaximumMassTons, ClampTier(rules, ref massTier, "mass", profile));
        float hull = MathF.Round(Interpolate(profile.MinimumHull, profile.MaximumHull, ClampTier(rules, ref hullTier, "hull", profile)));
        return new ShipHullLoadoutSelection(massTons * 1_000f, hull, rules);
    }

    private static int RoleTier(ShipSubclass subclass) => subclass switch
    {
        ShipSubclass.Ranged => 1,
        ShipSubclass.Patrol => 2,
        ShipSubclass.Assault => 3,
        _ => 2
    };

    private static int ClampTier(List<LoadoutScoreRule> rules, ref int tier, string system, ShipClassGenerationProfile profile)
    {
        int unclamped = tier;
        tier = Math.Clamp(tier, 0, Tiers);
        if (tier != unclamped)
            Add(rules, $"{system}_tier_clamp", tier - unclamped,
                $"{system.ToUpperInvariant()}-Klasse auf Bereich 0-{Tiers} begrenzt ({profile.ShipClass})");
        return tier;
    }

    private static float Interpolate(float minimum, float maximum, int tier) =>
        minimum + (maximum - minimum) * tier / Tiers;

    private static void Adjust(List<LoadoutScoreRule> rules, ref int value, string id, int points, string reason)
    {
        value += points;
        Add(rules, id, points, reason);
    }

    private static void Add(List<LoadoutScoreRule> rules, string id, int points, string reason) => rules.Add(new(id, points, reason));
}
