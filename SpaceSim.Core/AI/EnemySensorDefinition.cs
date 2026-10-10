namespace SpaceSim.Core.AI;

/// <summary>Passive sensor suite used only by enemy ships to discover the player.</summary>
public enum EnemySensorType
{
    ArgusS200, GhostEye, RavenS4, EchoshroudV7, OracleX9,
    HawkeyeM3, Voidseeker, Watchtower, Nightfall, Omniscient
}

/// <summary>
/// Detection anchors are defined at 50 and 100 PU of the player's actual reactor output.
/// Output below 50 PU uses MinimumRange; output above 100 PU uses MaximumRange.
/// </summary>
public sealed record EnemySensorDefinition(
    EnemySensorType Type,
    string Name,
    float MinimumRangeMeters,
    float MaximumRangeMeters,
    float PowerUsage)
{
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        foreach (float value in new[] { MinimumRangeMeters, MaximumRangeMeters, PowerUsage })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(EnemySensorDefinition));
        if (MaximumRangeMeters < MinimumRangeMeters)
            throw new ArgumentOutOfRangeException(nameof(MaximumRangeMeters));
    }

    public float DetectionRangeForPlayerOutput(float playerOutputPower)
    {
        if (!float.IsFinite(playerOutputPower) || playerOutputPower <= 50f) return MinimumRangeMeters;
        if (playerOutputPower >= 100f) return MaximumRangeMeters;
        float progress = (playerOutputPower - 50f) / 50f;
        return MinimumRangeMeters + (MaximumRangeMeters - MinimumRangeMeters) * progress;
    }
}

/// <summary>Central enemy-sensor catalogue. Existing enemies intentionally use ARGUS S-200.</summary>
public static class EnemySensorDefinitions
{
    public static EnemySensorDefinition ArgusS200 { get; } = new(EnemySensorType.ArgusS200, "ARGUS S-200", 1_000f, 2_500f, 20f);
    public static EnemySensorDefinition GhostEye { get; } = new(EnemySensorType.GhostEye, "GHOST EYE", 500f, 1_400f, 8f);
    public static EnemySensorDefinition RavenS4 { get; } = new(EnemySensorType.RavenS4, "RAVEN S-4", 750f, 1_800f, 12f);
    public static EnemySensorDefinition EchoshroudV7 { get; } = new(EnemySensorType.EchoshroudV7, "ECHOSHROUD V-7", 650f, 2_300f, 18f);
    public static EnemySensorDefinition OracleX9 { get; } = new(EnemySensorType.OracleX9, "ORACLE X-9", 1_200f, 3_000f, 45f);
    public static EnemySensorDefinition HawkeyeM3 { get; } = new(EnemySensorType.HawkeyeM3, "HAWKEYE M-3", 1_100f, 2_700f, 32f);
    public static EnemySensorDefinition Voidseeker { get; } = new(EnemySensorType.Voidseeker, "VOIDSEEKER", 900f, 3_000f, 38f);
    public static EnemySensorDefinition Watchtower { get; } = new(EnemySensorType.Watchtower, "WATCHTOWER", 1_500f, 2_200f, 28f);
    public static EnemySensorDefinition Nightfall { get; } = new(EnemySensorType.Nightfall, "NIGHTFALL", 500f, 2_000f, 15f);
    public static EnemySensorDefinition Omniscient { get; } = new(EnemySensorType.Omniscient, "OMNISCIENT", 1_400f, 3_000f, 55f);

    public static IReadOnlyList<EnemySensorDefinition> All { get; } =
        [ArgusS200, GhostEye, RavenS4, EchoshroudV7, OracleX9, HawkeyeM3, Voidseeker, Watchtower, Nightfall, Omniscient];

    public static EnemySensorDefinition Get(EnemySensorType type) => All.Single(definition => definition.Type == type);
}
