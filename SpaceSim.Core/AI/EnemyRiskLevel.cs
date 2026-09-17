using SpaceSim.Core.Combat;

namespace SpaceSim.Core.AI;

/// <summary>Health-derived caution layer that complements, but never replaces, the combat FSM.</summary>
public enum EnemyRiskLevel
{
    Aggressive,
    Normal,
    Defensive,
    Critical
}

public static class EnemyRiskAssessment
{
    public static EnemyRiskLevel Calculate(EnemyShipState enemy, EnemyAiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(enemy);
        ArgumentNullException.ThrowIfNull(settings);
        var ship = enemy.Ship;
        float shieldFraction = ship.Shield.MaximumShield <= 0f ? 0f :
            ship.Shield.CurrentShield / ship.Shield.MaximumShield;
        return Calculate(ship.Hull.CurrentHull, ship.Hull.MaximumHull, shieldFraction,
            ship.Systems.PropulsionCondition, ship.Systems.WeaponsCondition, ship.Systems.ShieldsCondition, settings);
    }

    /// <summary>Pure form used by deterministic tests and future non-visual AI tooling.</summary>
    public static EnemyRiskLevel Calculate(int currentHull, int maximumHull, float shieldFraction,
        float propulsionCondition, float weaponsCondition, float shieldsCondition, EnemyAiSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        float weakestSystem = MathF.Min(propulsionCondition, MathF.Min(weaponsCondition, shieldsCondition));
        if (currentHull <= 1 || weakestSystem <= settings.CriticalSystemConditionThreshold)
            return EnemyRiskLevel.Critical;
        if (currentHull < maximumHull || shieldFraction < settings.DefensiveShieldThreshold ||
            weakestSystem < settings.DefensiveSystemConditionThreshold)
            return EnemyRiskLevel.Defensive;
        return shieldFraction < settings.AggressiveShieldThreshold
            ? EnemyRiskLevel.Normal
            : EnemyRiskLevel.Aggressive;
    }
}
