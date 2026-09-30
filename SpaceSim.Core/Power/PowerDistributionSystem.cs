using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.Power;

/// <summary>Calculates independent station demand and proportional reactor delivery.</summary>
internal static class PowerDistributionSystem
{
    public static PowerState Create(PowerSettings settings) => new(settings.MaximumPropulsionDraw,
        settings.AuxiliaryThrusterDraw, settings.MaximumWeaponsDraw, settings.MaximumShieldsDraw);

    public static ReactorState CreateReactor(PowerSettings settings, float? initialOperatingLevelPercent = null) => new(settings.MaximumReactorOutputPower,
        initialOperatingLevelPercent ?? settings.DefaultReactorOperatingLevelPercent, settings.ReactorFuelCapacity);

    public static void ApplyPlayerDemand(ShipState ship, LanceState lance, ShipCommand command)
    {
        float main = command.MainThrust ? Normalized(command.MainThrustIntensity) : 0f;
        float propulsion = ship.Power.AuxiliaryThrusterReserveDraw + ship.Power.MaximumMainThrusterDraw * main;
        float weapons = lance.IsReady ? 0f : ship.Power.MaximumWeaponsDraw;
        float shields = ship.Shield.IsRechargeDelayed || ship.Shield.CurrentShield >= ship.Shield.MaximumShield
            ? 0f : ship.Power.MaximumShieldsDraw;
        ApplyAllocatedDemand(ship.Power, ship.Reactor, propulsion, weapons, shields);
    }

    /// <summary>Enemies use every station at its normal maximum once combat is detected.</summary>
    public static void ApplyEnemyCombatDemand(ShipState ship) =>
        ApplyDemand(ship.Power, ship.Reactor, ship.Power.MaximumPropulsionDraw,
            ship.Power.MaximumWeaponsDraw, ship.Power.MaximumShieldsDraw);

    public static void ApplyEnemyPatrolDemand(ShipState ship, float propulsionDemand) =>
        ApplyDemand(ship.Power, ship.Reactor, propulsionDemand, 0f, 0f);

    public static void SetReactorOperatingLevel(ReactorState reactor, float percent)
    {
        if (!float.IsFinite(percent) || reactor.IsFuelDepleted) return;
        reactor.TargetOperatingLevelPercent = Math.Clamp(percent, 0f, 100f);
    }

    public static void SetPlayerAllocation(PowerState state, PowerAllocation allocation)
    {
        if (!allocation.IsValid) return;
        state.PropulsionAllocationPercent = allocation.BridgePercent;
        state.ShieldsAllocationPercent = allocation.ShieldsPercent;
        state.WeaponsAllocationPercent = allocation.ArmariumPercent;
    }

    public static void StepReactor(ReactorState reactor, PowerSettings settings)
    {
        if (reactor.IsFuelDepleted)
        {
            reactor.Fuel = 0f;
            reactor.TargetOperatingLevelPercent = 0f;
            reactor.OperatingLevelPercent = 0f;
            reactor.FuelUsagePerMinute = 0f;
            return;
        }

        float maximumStep = 100f / settings.ReactorRampSeconds * SimulationSettings.FixedDeltaSeconds;
        float difference = reactor.TargetOperatingLevelPercent - reactor.OperatingLevelPercent;
        reactor.OperatingLevelPercent += MathF.Sign(difference) * MathF.Min(MathF.Abs(difference), maximumStep);
        if (reactor.OperatingLevelPercent <= 0f)
        {
            reactor.OperatingLevelPercent = 0f;
            reactor.FuelUsagePerMinute = 0f;
            return;
        }

        float outputFraction = reactor.OperatingLevelPercent / 100f;
        reactor.FuelUsagePerMinute = settings.ReactorIdleFuelUsagePerMinute +
            (settings.ReactorMaximumFuelUsagePerMinute - settings.ReactorIdleFuelUsagePerMinute) * outputFraction * outputFraction;
        reactor.Fuel = MathF.Max(0f, reactor.Fuel - reactor.FuelUsagePerMinute / 60f * SimulationSettings.FixedDeltaSeconds);
        if (reactor.IsFuelDepleted)
        {
            reactor.TargetOperatingLevelPercent = 0f;
            reactor.OperatingLevelPercent = 0f;
            reactor.FuelUsagePerMinute = 0f;
        }
    }

    private static void ApplyDemand(PowerState state, ReactorState reactor, float propulsion, float weapons, float shields)
    {
        state.PropulsionRequested = Math.Clamp(propulsion, 0f, state.MaximumPropulsionDraw);
        state.WeaponsRequested = Math.Clamp(weapons, 0f, state.MaximumWeaponsDraw);
        state.ShieldsRequested = Math.Clamp(shields, 0f, state.MaximumShieldsDraw);
        state.DemandScale = state.RequestedPower <= 0f ? 1f : Math.Min(1f, reactor.AvailablePower / state.RequestedPower);
        float fullPropulsionDemand = state.MaximumPropulsionDraw + state.WeaponsRequested + state.ShieldsRequested;
        state.PropulsionAvailable = fullPropulsionDemand <= 0f ? state.MaximumPropulsionDraw :
            state.MaximumPropulsionDraw * Math.Min(1f, reactor.AvailablePower / fullPropulsionDemand);
        state.PropulsionDraw = state.PropulsionRequested * state.DemandScale;
        state.AuxiliaryThrusterDraw = Math.Min(state.PropulsionDraw, state.AuxiliaryThrusterReserveDraw);
        state.MainThrusterDraw = Math.Max(0f, state.PropulsionDraw - state.AuxiliaryThrusterReserveDraw);
        state.WeaponsAvailable = state.MaximumWeaponsDraw * state.DemandScale;
        state.ShieldsAvailable = state.MaximumShieldsDraw * state.DemandScale;
        state.WeaponsDraw = state.WeaponsRequested * state.DemandScale;
        state.ShieldsDraw = state.ShieldsRequested * state.DemandScale;
        reactor.CurrentDraw = state.CurrentDraw;
    }

    private static void ApplyAllocatedDemand(PowerState state, ReactorState reactor, float propulsion, float weapons, float shields)
    {
        ClampPlayerAllocationToStationLimits(state, reactor.AvailablePower);
        state.PropulsionRequested = Math.Clamp(propulsion, 0f, state.MaximumPropulsionDraw);
        state.WeaponsRequested = Math.Clamp(weapons, 0f, state.MaximumWeaponsDraw);
        state.ShieldsRequested = Math.Clamp(shields, 0f, state.MaximumShieldsDraw);
        state.DemandScale = 1f;
        state.PropulsionAvailable = Budget(reactor.AvailablePower, state.PropulsionAllocationPercent, state.MaximumPropulsionDraw);
        state.WeaponsAvailable = Budget(reactor.AvailablePower, state.WeaponsAllocationPercent, state.MaximumWeaponsDraw);
        state.ShieldsAvailable = Budget(reactor.AvailablePower, state.ShieldsAllocationPercent, state.MaximumShieldsDraw);
        state.PropulsionDraw = Math.Min(state.PropulsionRequested, state.PropulsionAvailable);
        state.AuxiliaryThrusterDraw = Math.Min(state.PropulsionDraw, state.AuxiliaryThrusterReserveDraw);
        state.MainThrusterDraw = Math.Max(0f, state.PropulsionDraw - state.AuxiliaryThrusterReserveDraw);
        state.WeaponsDraw = Math.Min(state.WeaponsRequested, state.WeaponsAvailable);
        state.ShieldsDraw = Math.Min(state.ShieldsRequested, state.ShieldsAvailable);
        reactor.CurrentDraw = state.CurrentDraw;
    }

    private static float Budget(float output, float allocationPercent, float stationMaximum) =>
        Math.Min(stationMaximum, Math.Max(0f, output) * allocationPercent / 100f);

    private static void ClampPlayerAllocationToStationLimits(PowerState state, float output)
    {
        state.PropulsionAllocationPercent = Math.Min(state.PropulsionAllocationPercent, MaximumPercent(output, state.MaximumPropulsionDraw));
        state.WeaponsAllocationPercent = Math.Min(state.WeaponsAllocationPercent, MaximumPercent(output, state.MaximumWeaponsDraw));
        state.ShieldsAllocationPercent = Math.Min(state.ShieldsAllocationPercent, MaximumPercent(output, state.MaximumShieldsDraw));
    }

    private static float MaximumPercent(float output, float stationMaximum) => output <= 0f ? 100f :
        Math.Min(100f, stationMaximum / output * 100f);

    private static float Normalized(float value) => float.IsFinite(value) ? Math.Clamp(value, 0f, 1f) : 0f;
}
