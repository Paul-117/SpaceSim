namespace SpaceSim.Core.Ships;

/// <summary>Selectable main engine installed in one ship.</summary>
public enum MainBoosterType { AtlasM100, SkyfangM85, DreadnoughtM180, StarlingM70, FirebrandM150 }
/// <summary>Selectable reverse engine installed in one ship.</summary>
public enum ReverseBoosterType { AnchorR30, GravebreakR60, BackdraftR20 }
/// <summary>Selectable port/starboard thruster pair installed in one ship.</summary>
public enum SideBoosterType { VectorS1, TalonS2, ColossusS4 }

public sealed record MainBoosterDefinition(MainBoosterType Type, string Name, float ThrustNewtons,
    float RampUpSeconds, float MaximumSpeedMetersPerSecond, float PowerDraw)
{
    internal void Validate() => BoosterDefinitions.Validate(Name, ThrustNewtons, RampUpSeconds, MaximumSpeedMetersPerSecond, PowerDraw);
}

public sealed record ReverseBoosterDefinition(ReverseBoosterType Type, string Name, float ThrustNewtons,
    float MaximumSpeedMetersPerSecond, float PowerDraw)
{
    internal void Validate() => BoosterDefinitions.Validate(Name, ThrustNewtons, MaximumSpeedMetersPerSecond, PowerDraw);
}

public sealed record SideBoosterDefinition(SideBoosterType Type, string Name, float ThrustNewtons,
    float MaximumRotationDegreesPerSecond, float PowerDraw)
{
    public float MaximumAngularVelocityRadiansPerSecond => MaximumRotationDegreesPerSecond * MathF.PI / 180f;
    internal void Validate() => BoosterDefinitions.Validate(Name, ThrustNewtons, MaximumRotationDegreesPerSecond, PowerDraw);
}

/// <summary>Central propulsion catalogue. Existing ships intentionally use the three Standard models.</summary>
public static class BoosterDefinitions
{
    public static MainBoosterDefinition AtlasM100 { get; } = new(MainBoosterType.AtlasM100, "ATLAS M-100", 100_000f, 10f, 100f, 30f);
    public static MainBoosterDefinition SkyfangM85 { get; } = new(MainBoosterType.SkyfangM85, "SKYFANG M-85", 85_000f, 3f, 120f, 40f);
    public static MainBoosterDefinition DreadnoughtM180 { get; } = new(MainBoosterType.DreadnoughtM180, "DREADNOUGHT M-180", 180_000f, 18f, 85f, 48f);
    public static MainBoosterDefinition StarlingM70 { get; } = new(MainBoosterType.StarlingM70, "STARLING M-70", 70_000f, 5f, 145f, 35f);
    public static MainBoosterDefinition FirebrandM150 { get; } = new(MainBoosterType.FirebrandM150, "FIREBRAND M-150", 150_000f, 7f, 130f, 60f);

    public static ReverseBoosterDefinition AnchorR30 { get; } = new(ReverseBoosterType.AnchorR30, "ANCHOR R-30", 30_000f, 50f, 30f);
    public static ReverseBoosterDefinition GravebreakR60 { get; } = new(ReverseBoosterType.GravebreakR60, "GRAVEBREAK R-60", 60_000f, 40f, 45f);
    public static ReverseBoosterDefinition BackdraftR20 { get; } = new(ReverseBoosterType.BackdraftR20, "BACKDRAFT R-20", 20_000f, 80f, 24f);

    public static SideBoosterDefinition VectorS1 { get; } = new(SideBoosterType.VectorS1, "VECTOR S-1", 1_000f, 10f, 30f);
    public static SideBoosterDefinition TalonS2 { get; } = new(SideBoosterType.TalonS2, "TALON S-2", 2_000f, 18f, 50f);
    public static SideBoosterDefinition ColossusS4 { get; } = new(SideBoosterType.ColossusS4, "COLOSSUS S-4", 4_000f, 7f, 42f);

    public static IReadOnlyList<MainBoosterDefinition> MainAll { get; } = [AtlasM100, SkyfangM85, DreadnoughtM180, StarlingM70, FirebrandM150];
    public static IReadOnlyList<ReverseBoosterDefinition> ReverseAll { get; } = [AnchorR30, GravebreakR60, BackdraftR20];
    public static IReadOnlyList<SideBoosterDefinition> SideAll { get; } = [VectorS1, TalonS2, ColossusS4];

    internal static void Validate(string name, params float[] values)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (values.Any(value => !float.IsFinite(value) || value <= 0f))
            throw new ArgumentOutOfRangeException(nameof(values));
    }
}
