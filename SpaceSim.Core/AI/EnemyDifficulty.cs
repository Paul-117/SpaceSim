using SpaceSim.Core.Power;

namespace SpaceSim.Core.AI;

/// <summary>Encounter-selected tuning profile. It changes decisions, never the shared physics rules.</summary>
public enum EnemyDifficulty
{
    Easy,
    Medium,
    Hard
}

internal sealed record EnemyDifficultyProfile(EnemyAiSettings Ai, PowerSettings Power);

internal static class EnemyDifficultyProfiles
{
    public static EnemyDifficultyProfile Create(EnemyDifficulty difficulty, EnemyAiSettings normalAi,
        PowerSettings normalPower) => difficulty switch
    {
        EnemyDifficulty.Easy => new EnemyDifficultyProfile(
            normalAi with
            {
                PreferredCombatDistanceAggressive = 600f,
                PreferredCombatDistanceNormal = 625f,
                PreferredCombatDistanceDefensive = 650f,
                PreferredCombatDistanceCritical = 675f,
                AttackEnterDistance = 750f,
                AttackExitDistance = 900f,
                FireAimTolerance = Radians(2f),
                NormalThreatChargeThreshold = 1f,
                DefensiveThreatChargeThreshold = 0.95f,
                CriticalThreatChargeThreshold = 0.90f,
                NormalThreatAimAngle = Radians(2f),
                DefensiveThreatAimAngle = Radians(3f),
                CriticalThreatAimAngle = Radians(4f),
                MinimumEvadeDuration = 0.35f,
                MaximumEvadeDuration = 0.65f,
                AggressiveEvadeCooldown = 4f,
                NormalEvadeCooldown = 4f,
                DefensiveEvadeCooldown = 3.5f,
                CriticalEvadeCooldown = 3f
            },
            normalPower with
            {
                AttackProfile = new PowerProfile(50f, 35f, 25f),
                EvadeProfile = new PowerProfile(50f, 10f, 25f),
                RepositionProfile = new PowerProfile(50f, 20f, 25f)
            }),
        EnemyDifficulty.Hard => new EnemyDifficultyProfile(
            normalAi with
            {
                PreferredCombatDistanceAggressive = 375f,
                PreferredCombatDistanceNormal = 400f,
                PreferredCombatDistanceDefensive = 450f,
                PreferredCombatDistanceCritical = 500f,
                MinimumCombatDistance = 200f,
                AttackEnterDistance = 750f,
                AttackExitDistance = 900f,
                FireAimTolerance = Radians(5f),
                NormalThreatChargeThreshold = 0.85f,
                DefensiveThreatChargeThreshold = 0.75f,
                CriticalThreatChargeThreshold = 0.70f,
                NormalThreatAimAngle = Radians(6f),
                DefensiveThreatAimAngle = Radians(8f),
                CriticalThreatAimAngle = Radians(10f),
                MinimumEvadeDuration = 0.5f,
                MaximumEvadeDuration = 1f,
                AggressiveEvadeCooldown = 2.5f,
                NormalEvadeCooldown = 2f,
                DefensiveEvadeCooldown = 1.5f,
                CriticalEvadeCooldown = 1f
            },
            normalPower with
            {
                AttackProfile = new PowerProfile(50f, 40f, 10f),
                EvadeProfile = new PowerProfile(50f, 10f, 20f),
                RepositionProfile = new PowerProfile(50f, 25f, 10f),
                DefendProfile = new PowerProfile(30f, 15f, 35f)
            }),
        _ => new EnemyDifficultyProfile(normalAi, normalPower)
    };

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
