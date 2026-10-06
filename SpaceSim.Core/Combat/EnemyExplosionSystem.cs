using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

/// <summary>Applies proximity damage to the player when an enemy ship is destroyed.</summary>
internal static class EnemyExplosionSystem
{
    public static void Apply(WorldState world, EnemyShipState enemy, SimulationSettings settings,
        Random random, List<SimulationEvent> events)
    {
        float distance = Vector3.Distance(world.Ship.Position, enemy.Ship.Position);
        events.Add(new EnemyExplosion(enemy.EnemyId, enemy.Ship.Position, distance));
        if (!settings.EnemyExplosion.Enabled) return;
        EnemyExplosionSettings thresholds = settings.EnemyExplosion;

        if (Within(distance, thresholds.PlayerDestructionDistanceMeters))
        {
            HullSystem.Destroy(world.Ship.Hull, WeaponOwner.Player, null, world.Ship.Position, events);
            world.GameState = GameState.GameOver;
            events.Add(new PlayerDestroyed(enemy.EnemyId, world.Ship.Position));
            return;
        }

        if (Within(distance, thresholds.ShieldDepletionDistanceMeters))
            ShieldSystem.Deplete(world.Ship.Shield, WeaponOwner.Player, null, world.Ship.Position, settings.Shield, events);

        int disabledSubsystems = Within(distance, thresholds.TwoSubsystemsDisabledDistanceMeters) ? 2 :
            Within(distance, thresholds.OneSubsystemDisabledDistanceMeters) ? 1 : 0;
        DisableSubsystems(world.Ship.Systems, disabledSubsystems, enemy.EnemyId, world.Ship.Position, random, events);
    }

    private static void DisableSubsystems(SubsystemState systems, int count, int enemyId, Vector3 position,
        Random random, List<SimulationEvent> events)
    {
        ShipSubsystem[] choices = Enum.GetValues<ShipSubsystem>().Where(system => systems.Get(system) > 0f).ToArray();
        for (int index = 0; index < count && choices.Length > 0; index++)
        {
            int choiceIndex = random.Next(choices.Length);
            ShipSubsystem system = choices[choiceIndex];
            float before = systems.Get(system);
            systems.Set(system, 0f);
            events.Add(new SubsystemDamaged(WeaponOwner.Player, null, system, before, 0f, position));
            events.Add(new SubsystemDisabled(WeaponOwner.Player, null, system, position));
            choices = choices.Where(candidate => candidate != system).ToArray();
        }
    }

    // Vector3.Distance may produce a few floating-point units of rounding at an exact design threshold.
    private static bool Within(float distance, float threshold) => distance <= threshold + 0.001f;
}
