namespace SpaceSim.Core.Power;

/// <summary>Only this core service changes allocations; invalid requests leave the state untouched.</summary>
internal static class PowerDistributionSystem
{
    public static PowerState Create(PowerSettings settings) =>
        new(settings.TotalAvailablePower, settings.NominalSystemPower, settings.AdjustmentStep, settings.DefaultProfile);

    public static bool TryApply(PowerState state, PowerAllocationCommand command)
    {
        if (command.IsEmpty) return false;
        float propulsion = state.PropulsionAllocation + command.PropulsionDelta;
        float weapons = state.WeaponsAllocation + command.WeaponsDelta;
        float shields = state.ShieldsAllocation + command.ShieldsDelta;
        if (!Valid(state, propulsion, weapons, shields)) return false;
        state.PropulsionAllocation = propulsion;
        state.WeaponsAllocation = weapons;
        state.ShieldsAllocation = shields;
        return true;
    }

    public static void ApplyProfile(PowerState state, PowerProfile profile)
    {
        if (!Valid(state, profile.Propulsion, profile.Weapons, profile.Shields))
            throw new ArgumentException("Enemy profile exceeds its available reactor output.", nameof(profile));
        state.PropulsionAllocation = profile.Propulsion;
        state.WeaponsAllocation = profile.Weapons;
        state.ShieldsAllocation = profile.Shields;
    }

    private static bool Valid(PowerState state, float propulsion, float weapons, float shields) =>
        float.IsFinite(propulsion) && float.IsFinite(weapons) && float.IsFinite(shields) &&
        propulsion >= 0f && weapons >= 0f && shields >= 0f &&
        propulsion + weapons + shields <= state.AvailablePower + 0.0001f;
}
