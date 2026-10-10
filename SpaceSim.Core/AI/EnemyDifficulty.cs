using SpaceSim.Core.Combat;

namespace SpaceSim.Core.AI;

/// <summary>Encounter-selected ship class. Board computers now control execution quality.</summary>
public enum EnemyDifficulty
{
    Easy,
    Medium,
    Hard
}

internal sealed record EnemyDifficultyProfile(EnemyAiSettings Ai, string Name, EnemyShipClass ShipClass);

internal static class EnemyDifficultyProfiles
{
    public static EnemyDifficultyProfile Create(EnemyDifficulty difficulty, EnemyAiSettings normalAi) => difficulty switch
    {
        EnemyDifficulty.Easy => new EnemyDifficultyProfile(normalAi, "Cetus", EnemyShipClass.Interceptor),
        EnemyDifficulty.Hard => new EnemyDifficultyProfile(normalAi, "Atlas", EnemyShipClass.Frigate),
        _ => new EnemyDifficultyProfile(normalAi, "Argus", EnemyShipClass.Corvette)
    };
}
