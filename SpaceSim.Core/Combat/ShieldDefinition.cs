namespace SpaceSim.Core.Combat;

/// <summary>Selectable ship-wide shield generator installed in one ship.</summary>
public enum ShieldType
{
    GuardianS20, PhantomVeil, Ironclad, Citadel, Pulseguard,
    SentinelArray, EtherealWard, Bulwark, NovaBarrier, Quicksilver
}

/// <summary>All combat- and power-relevant values of one shield generator.</summary>
public sealed record ShieldDefinition(
    ShieldType Type,
    string Name,
    float MaximumHitPoints,
    float RechargeSeconds,
    float RebootSeconds,
    float PowerDraw)
{
    public float RechargePerSecond => MaximumHitPoints / RechargeSeconds;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        foreach (float value in new[] { MaximumHitPoints, RechargeSeconds, RebootSeconds, PowerDraw })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(ShieldDefinition));
    }
}

/// <summary>Central shield-generator catalogue. Existing ships intentionally use GUARDIAN S-20 until loadouts are introduced.</summary>
public static class ShieldDefinitions
{
    public static ShieldDefinition GuardianS20 { get; } = new(ShieldType.GuardianS20, "GUARDIAN S-20", 20f, 5f, 10f, 30f);
    public static ShieldDefinition PhantomVeil { get; } = new(ShieldType.PhantomVeil, "PHANTOM VEIL", 12f, 1.5f, 6f, 35f);
    public static ShieldDefinition Ironclad { get; } = new(ShieldType.Ironclad, "IRONCLAD", 40f, 9f, 20f, 45f);
    public static ShieldDefinition Citadel { get; } = new(ShieldType.Citadel, "CITADEL", 50f, 15f, 30f, 60f);
    public static ShieldDefinition Pulseguard { get; } = new(ShieldType.Pulseguard, "PULSEGUARD", 18f, 2.5f, 12f, 42f);
    public static ShieldDefinition SentinelArray { get; } = new(ShieldType.SentinelArray, "SENTINEL ARRAY", 30f, 6f, 15f, 38f);
    public static ShieldDefinition EtherealWard { get; } = new(ShieldType.EtherealWard, "ETHEREAL WARD", 15f, 3.5f, 5f, 24f);
    public static ShieldDefinition Bulwark { get; } = new(ShieldType.Bulwark, "BULWARK", 35f, 8f, 18f, 32f);
    public static ShieldDefinition NovaBarrier { get; } = new(ShieldType.NovaBarrier, "NOVA BARRIER", 25f, 4f, 22f, 50f);
    public static ShieldDefinition Quicksilver { get; } = new(ShieldType.Quicksilver, "QUICKSILVER", 10f, 1f, 8f, 40f);

    public static IReadOnlyList<ShieldDefinition> All { get; } =
        [GuardianS20, PhantomVeil, Ironclad, Citadel, Pulseguard, SentinelArray, EtherealWard, Bulwark, NovaBarrier, Quicksilver];

    public static ShieldDefinition Get(ShieldType type) => All.Single(definition => definition.Type == type);
}
