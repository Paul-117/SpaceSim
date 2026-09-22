using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

internal static class HullSystem
{
    public static HullState Create(HullSettings settings) => new(settings.MaximumHull);
    public static bool ApplyHit(HullState hull, SubsystemState systems, WeaponOwner owner, int? enemyId,
        Vector3 position, HullSettings settings, Random random, List<SimulationEvent> events)
    {
        int before = hull.CurrentHull;
        hull.CurrentHull = Math.Max(0, before - 1);
        events.Add(new HullDamaged(owner, enemyId, before, hull.CurrentHull, position));
        var choices = Enum.GetValues<ShipSubsystem>().Where(system => systems.Get(system) > 0f).ToArray();
        if (choices.Length > 0)
        {
            var system = choices[random.Next(choices.Length)];
            float condition = systems.Get(system);
            float after = Math.Max(0f, condition - settings.SubsystemDamagePerHullHit);
            systems.Set(system, after);
            events.Add(new SubsystemDamaged(owner, enemyId, system, condition, after, position));
            if (after == 0f) events.Add(new SubsystemDisabled(owner, enemyId, system, position));
        }
        return hull.CurrentHull == 0;
    }

    public static void Destroy(HullState hull, WeaponOwner owner, int? enemyId, Vector3 position,
        List<SimulationEvent> events)
    {
        int before = hull.CurrentHull;
        if (before == 0) return;
        hull.CurrentHull = 0;
        events.Add(new HullDamaged(owner, enemyId, before, 0, position));
    }
}
