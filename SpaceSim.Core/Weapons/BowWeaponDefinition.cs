namespace SpaceSim.Core.Weapons;

/// <summary>Selectable forward weapon installed in a ship's bow mount.</summary>
public enum BowWeaponType
{
    PeregrineL1000, Raptor, Doomhammer, Longspear, Hellstorm,
    Ravager, Spectre, Oblivion, Viper, Wraith
}

/// <summary>All combat-relevant values for one bow weapon. DPS is derived, never separately tuned.</summary>
public sealed record BowWeaponDefinition(
    BowWeaponType Type,
    string Name,
    float Damage,
    float RangeMeters,
    float ChargeSeconds,
    float TurretMaximumAngleDegrees,
    float TurretDegreesPerSecond,
    float PowerDraw)
{
    public float DamagePerSecond => Damage / ChargeSeconds;
    public float VisualRangeMeters => RangeMeters * 3f;

    internal void Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Name);
        foreach (float value in new[] { Damage, RangeMeters, ChargeSeconds, TurretMaximumAngleDegrees, TurretDegreesPerSecond, PowerDraw })
            if (!float.IsFinite(value) || value <= 0f) throw new ArgumentOutOfRangeException(nameof(BowWeaponDefinition));
    }
}

/// <summary>Central bow-weapon catalogue. Existing ships intentionally mount PEREGRINE L-1000 until loadouts are introduced.</summary>
public static class BowWeaponDefinitions
{
    public static BowWeaponDefinition PeregrineL1000 { get; } = new(BowWeaponType.PeregrineL1000, "PEREGRINE L-1000", 20f, 1_000f, 5f, 5f, 3.33f, 40f);
    public static BowWeaponDefinition Raptor { get; } = new(BowWeaponType.Raptor, "RAPTOR", 11f, 750f, 2.5f, 12f, 10f, 38f);
    public static BowWeaponDefinition Doomhammer { get; } = new(BowWeaponType.Doomhammer, "DOOMHAMMER", 36f, 1_400f, 11f, 5f, 2f, 60f);
    public static BowWeaponDefinition Longspear { get; } = new(BowWeaponType.Longspear, "LONGSPEAR", 15f, 1_650f, 6.5f, 3f, 2.5f, 46f);
    public static BowWeaponDefinition Hellstorm { get; } = new(BowWeaponType.Hellstorm, "HELLSTORM", 6f, 500f, 1f, 15f, 15f, 48f);
    public static BowWeaponDefinition Ravager { get; } = new(BowWeaponType.Ravager, "RAVAGER", 28f, 600f, 5.5f, 7f, 5f, 42f);
    public static BowWeaponDefinition Spectre { get; } = new(BowWeaponType.Spectre, "SPECTRE", 13f, 1_050f, 3.5f, 15f, 12f, 50f);
    public static BowWeaponDefinition Oblivion { get; } = new(BowWeaponType.Oblivion, "OBLIVION", 48f, 1_800f, 15f, 2f, 1.5f, 75f);
    public static BowWeaponDefinition Viper { get; } = new(BowWeaponType.Viper, "VIPER", 18f, 850f, 3.8f, 8f, 7f, 44f);
    public static BowWeaponDefinition Wraith { get; } = new(BowWeaponType.Wraith, "WRAITH", 16f, 900f, 5f, 6f, 4f, 22f);

    public static IReadOnlyList<BowWeaponDefinition> All { get; } =
        [PeregrineL1000, Raptor, Doomhammer, Longspear, Hellstorm, Ravager, Spectre, Oblivion, Viper, Wraith];

    public static BowWeaponDefinition Get(BowWeaponType type) => All.Single(definition => definition.Type == type);
}
