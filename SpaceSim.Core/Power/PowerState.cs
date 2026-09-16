namespace SpaceSim.Core.Power;

/// <summary>Authoritative reactor output allocation for one ship.</summary>
public sealed class PowerState
{
    public float AvailablePower { get; }
    public float NominalSystemPower { get; }
    public float AdjustmentStep { get; }
    public float PropulsionAllocation { get; internal set; }
    public float WeaponsAllocation { get; internal set; }
    public float ShieldsAllocation { get; internal set; }
    public float AllocatedPower => PropulsionAllocation + WeaponsAllocation + ShieldsAllocation;
    public float UnallocatedPower => AvailablePower - AllocatedPower;
    public float PropulsionPowerFactor => Math.Clamp(PropulsionAllocation / NominalSystemPower, 0f, 1f);
    public float WeaponsPowerFactor => Math.Clamp(WeaponsAllocation / NominalSystemPower, 0f, 1f);
    public float ShieldsPowerFactor => Math.Clamp(ShieldsAllocation / NominalSystemPower, 0f, 1f);

    internal PowerState(float availablePower, float nominalSystemPower, float adjustmentStep, PowerProfile profile)
    {
        AvailablePower = availablePower;
        NominalSystemPower = nominalSystemPower;
        AdjustmentStep = adjustmentStep;
        PropulsionAllocation = profile.Propulsion;
        WeaponsAllocation = profile.Weapons;
        ShieldsAllocation = profile.Shields;
    }
}

/// <summary>A complete, valid allocation used for defaults and enemy tactics.</summary>
public readonly record struct PowerProfile(float Propulsion, float Weapons, float Shields);

/// <summary>A neutral, atomic allocation request. Godot and AI do not mutate PowerState.</summary>
public readonly record struct PowerAllocationCommand(
    float PropulsionDelta = 0f,
    float WeaponsDelta = 0f,
    float ShieldsDelta = 0f)
{
    public bool IsEmpty => PropulsionDelta == 0f && WeaponsDelta == 0f && ShieldsDelta == 0f;
    public PowerAllocationCommand Combine(PowerAllocationCommand other) => new(
        PropulsionDelta + other.PropulsionDelta,
        WeaponsDelta + other.WeaponsDelta,
        ShieldsDelta + other.ShieldsDelta);
}
