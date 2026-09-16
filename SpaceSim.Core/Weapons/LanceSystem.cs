using System.Numerics;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Targets;

namespace SpaceSim.Core.Weapons;

internal static class LanceSystem
{
    public static void Step(WorldState world, bool fire, SimulationSettings settings, List<SimulationEvent> events)
    {
        var lance = world.Lance;
        lance.ChargedSeconds = Math.Min(settings.LanceChargeSeconds,
            lance.ChargedSeconds + 1.0 / SimulationSettings.TickRate);
        // Eliminate rounding residue at exact tick-aligned charge durations.
        if (lance.ChargedSeconds + 1e-10 >= settings.LanceChargeSeconds)
            lance.ChargedSeconds = settings.LanceChargeSeconds;
        lance.ChargeFraction = (float)(lance.ChargedSeconds / settings.LanceChargeSeconds);
        if (!fire || !lance.IsReady) return;

        lance.ChargedSeconds = 0;
        lance.ChargeFraction = 0f;
        Vector3 origin = world.Ship.Position;
        Vector3 direction = Vector3.Normalize(world.Ship.Forward);
        float nearestDistance = settings.LanceRangeMeters;
        TargetState? hit = null;
        foreach (var target in world.Targets)
        {
            float? distance = RaySphere(origin, direction, target);
            if (distance is not { } value || value > nearestDistance) continue;
            nearestDistance = value;
            hit = target;
        }
        events.Add(new WeaponFired(origin, origin + direction * nearestDistance, hit?.Id));
        if (hit is null) return;
        world.MutableTargets.Remove(hit);
        world.HitCount++;
        events.Add(new TargetHit(hit.Id, hit.Position));
    }

    private static float? RaySphere(Vector3 origin, Vector3 direction, TargetState target)
    {
        Vector3 offset = target.Position - origin;
        float radiusSquared = target.RadiusMeters * target.RadiusMeters;
        if (offset.LengthSquared() <= radiusSquared) return 0f;
        float alongRay = Vector3.Dot(offset, direction);
        if (alongRay < 0f) return null;
        float perpendicularSquared = MathF.Max(0f, offset.LengthSquared() - alongRay * alongRay);
        if (perpendicularSquared > radiusSquared) return null;
        return alongRay - MathF.Sqrt(radiusSquared - perpendicularSquared);
    }
}
