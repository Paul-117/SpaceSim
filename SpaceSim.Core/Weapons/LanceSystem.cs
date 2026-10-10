using System.Numerics;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Combat;

namespace SpaceSim.Core.Weapons;

internal static class LanceSystem
{
    public static void Charge(LanceState lance, float chargeSeconds, float weaponsPowerFactor)
    {
        lance.ChargedSeconds = Math.Min(chargeSeconds,
            lance.ChargedSeconds + weaponsPowerFactor / SimulationSettings.TickRate);
        // Eliminate rounding residue at exact tick-aligned charge durations.
        if (lance.ChargedSeconds + 1e-10 >= chargeSeconds)
            lance.ChargedSeconds = chargeSeconds;
        lance.ChargeFraction = (float)(lance.ChargedSeconds / chargeSeconds);
    }

    public static void FirePlayer(WorldState world, bool fire, SimulationSettings settings, Random random,
        List<SimulationEvent> events)
    {
        var lance = world.Lance;
        if (!fire || !lance.IsReady) return;

        Discharge(lance);
        Vector3 origin = world.Ship.Position;
        Vector3 direction = world.LanceDirection;
        float lanceRange = world.Ship.Tuning.LanceRangeMeters;
        float nearestDistance = lanceRange;
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
        events.Add(new WeaponFired(origin, origin + direction * nearestDistance, hitId, WeaponOwner.Player, kind,
            origin + direction * lanceRange, origin + direction * world.Ship.Tuning.LanceVisualRangeMeters));
        if (enemyHit is not null)
        {
            float hullDamage = ShieldSystem.ApplyLanceDamage(enemyHit.Ship.Shield, WeaponOwner.Enemy, enemyHit.EnemyId,
                enemyHit.Ship.Position, settings.Shield, events, world.Ship.Tuning.BowWeapon.Damage,
                enemyHit.Ship.Systems.ShieldsCondition, enemyHit.Ship.Tuning.Shield);
            if (hullDamage > 0f && HullSystem.ApplyDamage(enemyHit.Ship.Hull,
                enemyHit.Ship.Systems, WeaponOwner.Enemy, enemyHit.EnemyId, enemyHit.Ship.Position,
                hullDamage, settings.Hull, random, events))
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
        float lanceRange = enemy.Ship.Tuning.LanceRangeMeters;
        bool hit = hitDistance is { } value && value <= lanceRange;
        float distance = hit ? hitDistance!.Value : lanceRange;
        events.Add(new WeaponFired(origin, origin + direction * distance, null,
            WeaponOwner.Enemy, hit ? WeaponHitKind.Player : WeaponHitKind.None,
            origin + direction * lanceRange, origin + direction * world.Ship.Tuning.LanceVisualRangeMeters));
        if (!hit) return;
        float hullDamage = ShieldSystem.ApplyLanceDamage(world.Ship.Shield, WeaponOwner.Player, null,
            world.Ship.Position, settings.Shield, events, enemy.Ship.Tuning.BowWeapon.Damage,
            world.Ship.Systems.ShieldsCondition, world.Ship.Tuning.Shield);
        if (hullDamage <= 0f || !HullSystem.ApplyDamage(world.Ship.Hull,
            world.Ship.Systems, WeaponOwner.Player, null, world.Ship.Position, hullDamage, settings.Hull, random, events)) return;
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
