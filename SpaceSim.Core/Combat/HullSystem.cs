using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

internal static class HullSystem
{
    public static HullState Create(HullSettings settings) => new(settings.MaximumHull);
    public static HullState Create(float maximumHull) => new(maximumHull);
    /// <summary>Applies the lance damage that was not absorbed by the shield.</summary>
    public static bool ApplyDamage(HullState hull, SubsystemState systems, WeaponOwner owner, int? enemyId,
        Vector3 position, float damage, HullSettings settings, Random random, List<SimulationEvent> events)
    {
        if (damage <= 0f) return hull.CurrentHull <= 0f;
        float before = hull.CurrentHull;
        hull.CurrentHull = Math.Max(0f, before - damage);
        events.Add(new HullDamaged(owner, enemyId, before, hull.CurrentHull, position));
        var choices = settings.EnableSubsystemDamage && random.NextDouble() < settings.SubsystemDamageChancePerHullHit
            ? SubsystemState.DamageableSubsystems.Where(system => systems.Get(system) >= 1f).ToArray()
            : Array.Empty<ShipSubsystem>();
        if (choices.Length > 0)
        {
            var system = choices[random.Next(choices.Length)];
            float condition = systems.Get(system);
            float after = systems.ApplyDebuff(system);
            events.Add(new SubsystemDamaged(owner, enemyId, system, condition, after, position));
            if (after == 0f) events.Add(new SubsystemDisabled(owner, enemyId, system, position));
        }
        return hull.CurrentHull <= 0f;
    }

    public static void Destroy(HullState hull, WeaponOwner owner, int? enemyId, Vector3 position,
        List<SimulationEvent> events)
    {
        float before = hull.CurrentHull;
        if (before <= 0f) return;
        hull.CurrentHull = 0f;
        events.Add(new HullDamaged(owner, enemyId, before, 0, position));
    }
}
