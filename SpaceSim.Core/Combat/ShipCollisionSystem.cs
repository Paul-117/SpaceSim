using System.Numerics;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

/// <summary>Resolves destructive player-versus-enemy proximity collisions in the authoritative simulation.</summary>
internal static class ShipCollisionSystem
{
    public static bool Resolve(WorldState world, IEnumerable<EnemyShipState> enemies,
        SimulationSettings settings, List<SimulationEvent> events)
    {
        EnemyShipState[] collisions = enemies.Where(enemy => !enemy.IsDestroyed &&
            Vector3.Distance(world.Ship.Position, enemy.Ship.Position) < settings.ShipCollisionDistanceMeters).ToArray();
        if (collisions.Length == 0) return false;

        foreach (EnemyShipState enemy in collisions)
        {
            enemy.IsDestroyed = true;
            Vector3 impact = (world.Ship.Position + enemy.Ship.Position) / 2f;
            events.Add(new ShipCollision(enemy.EnemyId, impact));
            events.Add(new EnemyDestroyed(enemy.EnemyId, enemy.Ship.Position));
        }

        world.GameState = GameState.GameOver;
        events.Add(new PlayerDestroyed(collisions[0].EnemyId, world.Ship.Position));
        return true;
    }
}
