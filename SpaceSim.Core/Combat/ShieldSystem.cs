using SpaceSim.Core.Simulation;

namespace SpaceSim.Core.Combat;

internal static class ShieldSystem
{
    public static ShieldState Create(ShieldSettings settings) => new(settings.MaximumShield);

    public static void Recharge(ShieldState shield, float powerFactor, ShieldSettings settings)
    {
        if (shield.RechargeDelayRemaining > 0f)
        {
            shield.RechargeDelayRemaining = Math.Max(0f,
                shield.RechargeDelayRemaining - SimulationSettings.FixedDeltaSeconds);
            return;
        }
        if (powerFactor <= 0f || shield.CurrentShield >= shield.MaximumShield) return;
        shield.CurrentShield = Math.Min(shield.MaximumShield,
            shield.CurrentShield + settings.RechargePerSecond * powerFactor * SimulationSettings.FixedDeltaSeconds);
    }

    public static bool ApplyLanceDamage(ShieldState shield, WeaponOwner targetOwner, int? targetEnemyId,
        System.Numerics.Vector3 position, ShieldSettings settings, List<SimulationEvent> events)
    {
        float before = shield.CurrentShield;
        float absorbed = Math.Min(before, settings.LanceDamage);
        if (absorbed > 0f)
        {
            shield.CurrentShield = before - absorbed;
            shield.RechargeDelayRemaining = settings.RechargeDelaySeconds;
            events.Add(new ShieldHit(targetOwner, targetEnemyId, settings.LanceDamage, before, shield.CurrentShield, position));
            if (before > 0f && shield.CurrentShield <= 0f)
                events.Add(new ShieldDepleted(targetOwner, targetEnemyId, position));
        }
        return settings.LanceDamage - absorbed > 0.0001f;
    }
}
