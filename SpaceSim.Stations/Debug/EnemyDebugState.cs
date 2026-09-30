using SpaceSim.Core.AI;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Debug;

/// <summary>
/// Read-only telemetry for the active enemy. This deliberately exposes a focused debug view,
/// rather than serialising the authoritative world state to a browser.
/// </summary>
public sealed record EnemyDebugState(
    bool EnemyAvailable,
    int EnemyId,
    string Difficulty,
    bool PlayerDetected,
    string AiState,
    float DistanceToPlayer,
    float ClosingSpeed,
    float RelativeSpeed,
    float EnemySpeed,
    int Hull,
    int MaximumHull,
    float Shield,
    float MaximumShield,
    float PropulsionCondition,
    float WeaponsCondition,
    float ShieldsCondition,
    float LanceCharge,
    bool LanceReady,
    float ReactorTargetOperatingLevelPercent,
    float ReactorOperatingLevelPercent,
    float ReactorAvailablePower,
    float ReactorCurrentDraw,
    float ReactorFuel,
    float ReactorFuelCapacity,
    float PropulsionRequested,
    float WeaponsRequested,
    float ShieldsRequested,
    float PropulsionDraw,
    float WeaponsDraw,
    float ShieldsDraw,
    long SimulationTick);

public static class EnemyDebugStateBuilder
{
    public static EnemyDebugState Build(WorldState world)
    {
        var enemy = world.CurrentEnemy;
        if (enemy is null) return Empty(world.Tick);

        EnemyAiController? ai = world.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
        var ship = enemy.Ship;
        var context = ai?.LastContext ?? default;
        var power = ship.Power;
        var reactor = ship.Reactor;
        return new EnemyDebugState(
            true,
            enemy.EnemyId,
            enemy.Difficulty.ToString().ToUpperInvariant(),
            ai?.IsPlayerDetected ?? false,
            ai?.CurrentState.ToString().ToUpperInvariant() ?? "UNAVAILABLE",
            context.DistanceToPlayer,
            context.ClosingSpeed,
            context.RelativeVelocity.Length(),
            ship.Velocity.Length(),
            ship.Hull.CurrentHull,
            ship.Hull.MaximumHull,
            ship.Shield.CurrentShield,
            ship.Shield.MaximumShield,
            ship.Systems.PropulsionCondition,
            ship.Systems.WeaponsCondition,
            ship.Systems.ShieldsCondition,
            enemy.Lance.ChargeFraction,
            enemy.Lance.IsReady,
            reactor.TargetOperatingLevelPercent,
            reactor.OperatingLevelPercent,
            reactor.AvailablePower,
            reactor.CurrentDraw,
            reactor.Fuel,
            reactor.FuelCapacity,
            power.PropulsionRequested,
            power.WeaponsRequested,
            power.ShieldsRequested,
            power.PropulsionDraw,
            power.WeaponsDraw,
            power.ShieldsDraw,
            world.Tick);
    }

    private static EnemyDebugState Empty(long tick) => new(
        false, 0, "NONE", false, "NONE", 0f, 0f, 0f, 0f,
        0, 0, 0f, 0f, 0f, 0f, 0f, 0f, false,
        0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, tick);
}
