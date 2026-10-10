using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

public static class ShieldSystem
{
    public static ShieldState Create(ShieldDefinition shield) => new(shield.MaximumHitPoints);

    public static void Recharge(ShieldState shield, float powerFactor, ShieldSettings settings, ShieldDefinition definition)
    {
        if (shield.IsRebooting)
        {
            if (powerFactor > 0f)
                shield.RebootRemaining = Math.Max(0f, shield.RebootRemaining - powerFactor * SimulationSettings.FixedDeltaSeconds);
            return;
        }
        if (shield.RechargeDelayRemaining > 0f)
        {
            shield.RechargeDelayRemaining = Math.Max(0f,
                shield.RechargeDelayRemaining - SimulationSettings.FixedDeltaSeconds);
            return;
        }
        if (powerFactor <= 0f || shield.CurrentShield >= shield.MaximumShield) return;
        shield.CurrentShield = Math.Min(shield.MaximumShield,
            shield.CurrentShield + definition.RechargePerSecond * powerFactor * SimulationSettings.FixedDeltaSeconds);
    }

    /// <returns>The lance damage that remains after shield absorption.</returns>
    public static float ApplyLanceDamage(ShieldState shield, WeaponOwner targetOwner, int? targetEnemyId,
        System.Numerics.Vector3 position, ShieldSettings settings, List<SimulationEvent> events, float incomingDamage,
        float absorptionFactor, ShieldDefinition definition)
    {
        float before = shield.CurrentShield;
        float absorbed = Math.Min(before, incomingDamage * Math.Clamp(absorptionFactor, 0f, 1f));
        if (absorbed > 0f)
        {
            shield.CurrentShield = before - absorbed;
            if (shield.CurrentShield <= 0f)
                BeginReboot(shield, definition);
            else
                shield.RechargeDelayRemaining = settings.RechargeDelaySeconds;
            events.Add(new ShieldHit(targetOwner, targetEnemyId, incomingDamage, before, shield.CurrentShield, position));
            if (before > 0f && shield.CurrentShield <= 0f)
                events.Add(new ShieldDepleted(targetOwner, targetEnemyId, position));
        }
        return Math.Max(0f, incomingDamage - absorbed);
    }

    public static void Deplete(ShieldState shield, WeaponOwner targetOwner, int? targetEnemyId,
        System.Numerics.Vector3 position, ShieldSettings settings, List<SimulationEvent> events, ShieldDefinition definition)
    {
        float before = shield.CurrentShield;
        if (before <= 0f) return;
        shield.CurrentShield = 0f;
        BeginReboot(shield, definition);
        events.Add(new ShieldHit(targetOwner, targetEnemyId, before, before, 0f, position));
        events.Add(new ShieldDepleted(targetOwner, targetEnemyId, position));
    }

    /// <summary>Starts a cold/depleted generator. Reboot time advances only while shield power is delivered.</summary>
    public static void BeginReboot(ShieldState shield, ShieldDefinition definition)
    {
        shield.CurrentShield = 0f;
        shield.RechargeDelayRemaining = 0f;
        shield.RebootRemaining = definition.RebootSeconds;
    }

    public static void RestoreFull(ShieldState shield)
    {
        shield.CurrentShield = shield.MaximumShield;
        shield.RechargeDelayRemaining = 0f;
        shield.RebootRemaining = 0f;
    }
}
