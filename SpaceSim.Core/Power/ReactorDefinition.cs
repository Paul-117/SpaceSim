namespace SpaceSim.Core.Power;

/// <summary>Selectable ship reactor installed in one ship.</summary>
public enum ReactorType
{
    CoreX125, SwiftcoreR90, MonolithT200, EcofluxP110, InfernoX175,
    HorizonA150, EnduranceW80, OverdriveOd140, LeviathanL250, HelixN115
}

/// <summary>All output, fuel and ramp values of one reactor. Fuel capacity remains a scenario setting for now.</summary>
public sealed record ReactorDefinition(
    ReactorType Type,
    string Name,
    float MaximumOutputPower,
    float MaximumFuelUsagePerMinute,
    float RampUpSeconds)
{
    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        foreach (float value in new[] { MaximumOutputPower, MaximumFuelUsagePerMinute, RampUpSeconds })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(ReactorDefinition));
    }
}

/// <summary>Central reactor catalogue. Existing ships intentionally use CORE-X125 until loadouts are introduced.</summary>
public static class ReactorDefinitions
{
    public static ReactorDefinition CoreX125 { get; } = new(ReactorType.CoreX125, "CORE-X125", 125f, 7f, 60f);
    public static ReactorDefinition SwiftcoreR90 { get; } = new(ReactorType.SwiftcoreR90, "SWIFTCORE R-90", 90f, 6.5f, 15f);
    public static ReactorDefinition MonolithT200 { get; } = new(ReactorType.MonolithT200, "MONOLITH T-200", 200f, 13f, 110f);
    public static ReactorDefinition EcofluxP110 { get; } = new(ReactorType.EcofluxP110, "ECOFLUX P-110", 110f, 4f, 45f);
    public static ReactorDefinition InfernoX175 { get; } = new(ReactorType.InfernoX175, "INFERNO X-175", 175f, 15f, 25f);
    public static ReactorDefinition HorizonA150 { get; } = new(ReactorType.HorizonA150, "HORIZON A-150", 150f, 8.5f, 65f);
    public static ReactorDefinition EnduranceW80 { get; } = new(ReactorType.EnduranceW80, "ENDURANCE W-80", 80f, 2.5f, 90f);
    public static ReactorDefinition OverdriveOd140 { get; } = new(ReactorType.OverdriveOd140, "OVERDRIVE OD-140", 140f, 12f, 10f);
    public static ReactorDefinition LeviathanL250 { get; } = new(ReactorType.LeviathanL250, "LEVIATHAN L-250", 250f, 19f, 150f);
    public static ReactorDefinition HelixN115 { get; } = new(ReactorType.HelixN115, "HELIX N-115", 115f, 5.5f, 30f);

    public static IReadOnlyList<ReactorDefinition> All { get; } =
        [CoreX125, SwiftcoreR90, MonolithT200, EcofluxP110, InfernoX175, HorizonA150, EnduranceW80, OverdriveOd140, LeviathanL250, HelixN115];

    public static ReactorDefinition Get(ReactorType type) => All.Single(definition => definition.Type == type);
}
