using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Reactorium;

/// <summary>Minimal authoritative Reactorium snapshot.</summary>
public sealed record ReactoriumState(float TargetOperatingLevelPercent, float OperatingLevelPercent,
    float OutputPower, float MaximumOutputPower, float CurrentDraw, float Fuel, float FuelCapacity,
    float FuelUsagePerMinute, float BridgePercent, float ShieldsPercent, float ArmariumPercent,
    float BridgePower, float ShieldsPower, float ArmariumPower, float BridgeMaximumPower,
    float ShieldsMaximumPower, float ArmariumMaximumPower, long SimulationTick);

public static class ReactoriumStateBuilder
{
    public static ReactoriumState Build(WorldState world)
    {
        var reactor = world.Ship.Reactor;
        var power = world.Ship.Power;
        return new ReactoriumState(reactor.TargetOperatingLevelPercent, reactor.OperatingLevelPercent,
            reactor.AvailablePower, reactor.MaximumOutputPower, reactor.CurrentDraw, reactor.Fuel,
            reactor.FuelCapacity, reactor.FuelUsagePerMinute, power.PropulsionAllocationPercent,
            power.ShieldsAllocationPercent, power.WeaponsAllocationPercent, power.PropulsionAvailable,
            power.ShieldsAvailable, power.WeaponsAvailable, power.MaximumPropulsionDraw,
            power.MaximumShieldsDraw, power.MaximumWeaponsDraw, world.Tick);
    }
}
