using SpaceSim.Core.Navigation;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

/// <summary>Resolves the station as a solid, lethal navigational obstacle.</summary>
internal static class ShopCollisionSystem
{
    public static bool Resolve(WorldState world, List<SimulationEvent> events)
    {
        ShopState? shop = world.CurrentEncounter.Shop;
        if (shop is null || !shop.IsColliding(world.Ship.Position)) return false;

        // A station collision is terminal regardless of the ship's remaining shield or hull.
        HullSystem.Destroy(world.Ship.Hull, WeaponOwner.Player, null, world.Ship.Position, events);
        world.GameState = GameState.GameOver;
        events.Add(new ShopCollision(shop.Position, world.Ship.Position));
        // -1 identifies a non-enemy environmental destruction to consumers that already handle PlayerDestroyed.
        events.Add(new PlayerDestroyed(-1, world.Ship.Position));
        return true;
    }
}
