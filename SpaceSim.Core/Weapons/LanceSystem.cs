using System.Numerics;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Combat;

namespace SpaceSim.Core.Weapons;

internal static class LanceSystem
{
    public static void Charge(LanceState lance, SimulationSettings settings, float weaponsPowerFactor)
    {
        lance.ChargedSeconds = Math.Min(settings.LanceChargeSeconds,
            lance.ChargedSeconds + weaponsPowerFactor / SimulationSettings.TickRate);
        // Eliminate rounding residue at exact tick-aligned charge durations.
        if (lance.ChargedSeconds + 1e-10 >= settings.LanceChargeSeconds)
            lance.ChargedSeconds = settings.LanceChargeSeconds;
        lance.ChargeFraction = (float)(lance.ChargedSeconds / settings.LanceChargeSeconds);
    }

    public static void FirePlayer(WorldState world, bool fire, SimulationSettings settings, Random random,
        List<SimulationEvent> events)
    {
        var lance = world.Lance;
        if (!fire || !lance.IsReady) return;

        Discharge(lance);
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
        EnemyShipState? enemyHit = null;
        foreach (EnemyShipState enemy in world.CurrentEnemies)
        {
            float? distance = RaySphere(origin, direction, enemy.Ship.Position, settings.EnemyAi.ShipHitRadiusMeters);
            if (distance is not { } value || value > nearestDistance) continue;
            nearestDistance = value;
            hit = null;
            enemyHit = enemy;
        }
        WeaponHitKind kind = enemyHit is not null ? WeaponHitKind.Enemy :
            hit is not null ? WeaponHitKind.Target : WeaponHitKind.None;
        int? hitId = enemyHit?.EnemyId ?? hit?.Id;
        events.Add(new WeaponFired(origin, origin + direction * nearestDistance, hitId, WeaponOwner.Player, kind));
        if (enemyHit is not null)
        {
            if (ShieldSystem.ApplyLanceDamage(enemyHit.Ship.Shield, WeaponOwner.Enemy, enemyHit.EnemyId,
                enemyHit.Ship.Position, settings.Shield, events) && HullSystem.ApplyHit(enemyHit.Ship.Hull,
                enemyHit.Ship.Systems, WeaponOwner.Enemy, enemyHit.EnemyId, enemyHit.Ship.Position,
                settings.Hull, random, events))
            {
                enemyHit.IsDestroyed = true;
                world.HitCount++;
                events.Add(new EnemyDestroyed(enemyHit.EnemyId, enemyHit.Ship.Position));
            }
        }
        else if (hit is not null)
        {
            world.MutableTargets.Remove(hit);
            world.HitCount++;
            events.Add(new TargetHit(hit.Id, hit.Position));
        }
    }

    public static void FireEnemy(WorldState world, EnemyShipState enemy, bool fire,
        SimulationSettings settings, Random random, List<SimulationEvent> events)
    {
        if (!fire || !enemy.Lance.IsReady || enemy.IsDestroyed) return;
        Discharge(enemy.Lance);
        Vector3 origin = enemy.Ship.Position;
        Vector3 direction = Vector3.Normalize(enemy.Ship.Forward);
        float? hitDistance = RaySphere(origin, direction, world.Ship.Position, settings.EnemyAi.ShipHitRadiusMeters);
        bool hit = hitDistance is { } value && value <= settings.LanceRangeMeters;
        float distance = hit ? hitDistance!.Value : settings.LanceRangeMeters;
        events.Add(new WeaponFired(origin, origin + direction * distance, null,
            WeaponOwner.Enemy, hit ? WeaponHitKind.Player : WeaponHitKind.None));
        if (!hit) return;
        if (!ShieldSystem.ApplyLanceDamage(world.Ship.Shield, WeaponOwner.Player, null,
            world.Ship.Position, settings.Shield, events) || !HullSystem.ApplyHit(world.Ship.Hull,
            world.Ship.Systems, WeaponOwner.Player, null, world.Ship.Position, settings.Hull, random, events)) return;
        world.GameState = GameState.GameOver;
        events.Add(new PlayerDestroyed(enemy.EnemyId, world.Ship.Position));
    }

    private static void Discharge(LanceState lance)
    {
        lance.ChargedSeconds = 0;
        lance.ChargeFraction = 0f;
    }

    private static float? RaySphere(Vector3 origin, Vector3 direction, TargetState target) =>
        RaySphere(origin, direction, target.Position, target.RadiusMeters);

    internal static float? RaySphere(Vector3 origin, Vector3 direction, Vector3 center, float radius)
    {
        Vector3 offset = center - origin;
        float radiusSquared = radius * radius;
        if (offset.LengthSquared() <= radiusSquared) return 0f;
        float alongRay = Vector3.Dot(offset, direction);
        if (alongRay < 0f) return null;
        float perpendicularSquared = MathF.Max(0f, offset.LengthSquared() - alongRay * alongRay);
        if (perpendicularSquared > radiusSquared) return null;
        return alongRay - MathF.Sqrt(radiusSquared - perpendicularSquared);
    }
}
