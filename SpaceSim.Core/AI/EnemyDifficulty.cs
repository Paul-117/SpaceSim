using SpaceSim.Core.Combat;

namespace SpaceSim.Core.AI;

/// <summary>Encounter-selected tuning profile. It changes aim precision, never the shared flight rules.</summary>
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
        EnemyDifficulty.Easy => new EnemyDifficultyProfile(
            normalAi with { FireAimTolerance = Radians(2f) }, "Cetus", EnemyShipClass.Corvette),
        EnemyDifficulty.Hard => new EnemyDifficultyProfile(
            normalAi with { FireAimTolerance = Radians(5f) }, "Atlas", EnemyShipClass.Cruiser),
        _ => new EnemyDifficultyProfile(normalAi, "Argus", EnemyShipClass.Frigate)
    };

    private static float Radians(float degrees) => degrees * MathF.PI / 180f;
}
