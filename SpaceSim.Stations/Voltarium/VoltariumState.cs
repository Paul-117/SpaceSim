using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Voltarium;

/// <summary>Minimal authoritative Voltarium snapshot.</summary>
public sealed record VoltariumState(float TargetOperatingLevelPercent, float OperatingLevelPercent,
    float OutputPower, float MaximumOutputPower, float CurrentDraw, float Fuel, float FuelCapacity,
    float FuelUsagePerMinute, float BridgePercent, float ShieldsPercent, float ArmariumPercent,
    float BridgePower, float ShieldsPower, float ArmariumPower, float BridgeMaximumPower,
    float ShieldsMaximumPower, float ArmariumMaximumPower, long SimulationTick);

public static class VoltariumStateBuilder
{
    public static VoltariumState Build(WorldState world)
    {
        var reactor = world.Ship.Reactor;
        var power = world.Ship.Power;
        return new VoltariumState(reactor.TargetOperatingLevelPercent, reactor.OperatingLevelPercent,
            reactor.AvailablePower, reactor.MaximumOutputPower, reactor.CurrentDraw, reactor.Fuel,
            reactor.FuelCapacity, reactor.FuelUsagePerMinute, power.PropulsionAllocationPercent,
            power.ShieldsAllocationPercent, power.WeaponsAllocationPercent, power.PropulsionAvailable,
            power.ShieldsAvailable, power.WeaponsAvailable, power.MaximumPropulsionDraw,
            power.MaximumShieldsDraw, power.MaximumWeaponsDraw, world.Tick);
    }
}
