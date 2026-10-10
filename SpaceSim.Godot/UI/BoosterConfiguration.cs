namespace SpaceSim.GodotClient.UI;

/// <summary>User-configurable Nomad booster values. Forces are shown in kN;
/// the simulation continues to use SI Newtons and Newton metres.</summary>
public sealed record BoosterConfiguration(
    float MainBoosterKilonewtons,
    float MainRampUpSeconds,
    float MaximumForwardSpeedMetersPerSecond,
    float ReverseBoosterKilonewtons,
    float MaximumReverseSpeedMetersPerSecond,
    float SideBoosterKilonewtons,
    float MaximumRotationDegreesPerSecond)
{
    public static BoosterConfiguration Default { get; } = new(100f, 10f, 100f, 30f, 50f, 1f, 10f);

    public BoosterConfiguration Clamp() => this with
    {
        MainBoosterKilonewtons = Math.Clamp(MainBoosterKilonewtons, 1f, 1_000f),
        MainRampUpSeconds = Math.Clamp(MainRampUpSeconds, .1f, 60f),
        MaximumForwardSpeedMetersPerSecond = Math.Clamp(MaximumForwardSpeedMetersPerSecond, 1f, 10_000f),
        ReverseBoosterKilonewtons = Math.Clamp(ReverseBoosterKilonewtons, 1f, 1_000f),
        MaximumReverseSpeedMetersPerSecond = Math.Clamp(MaximumReverseSpeedMetersPerSecond, 1f, 10_000f),
        SideBoosterKilonewtons = Math.Clamp(SideBoosterKilonewtons, .1f, 100f),
        MaximumRotationDegreesPerSecond = Math.Clamp(MaximumRotationDegreesPerSecond, 1f, 2_000f)
    };
}
