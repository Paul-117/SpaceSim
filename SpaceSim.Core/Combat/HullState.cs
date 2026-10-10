namespace SpaceSim.Core.Combat;

/// <summary>Damageable ship systems. Station names in the UI map Weapons to Armarium
/// and Reactor to Voltarium.</summary>
public enum ShipSubsystem { MainBooster, ReverseBooster, SideLeft, SideRight, Lance, Shields, Reactor }

public sealed class HullState
{
    /// <summary>Remaining structural hit points. Fractional values preserve partial shield overflow exactly.</summary>
    public float CurrentHull { get; internal set; }
    public float MaximumHull { get; }
    internal HullState(float maximumHull) { MaximumHull = maximumHull; CurrentHull = maximumHull; }
}

public sealed class SubsystemState
{
    public const float DebuffedCondition = 0.5f;
    public const float ReactorDebuffedCondition = 2f / 3f;

    public float MainBoosterCondition { get; internal set; } = 1f;
    public float ReverseBoosterCondition { get; internal set; } = 1f;
    public float SideLeftCondition { get; internal set; } = 1f;
    public float SideRightCondition { get; internal set; } = 1f;
    public float LanceCondition { get; internal set; } = 1f;
    public float ShieldGeneratorCondition { get; internal set; } = 1f;
    public float ReactorCondition { get; internal set; } = 1f;
    /// <summary>Reserved for later Sensorium damage. It is not part of the current damage pool.</summary>
    public float SensorsCondition { get; internal set; } = 1f;

    /// <summary>Legacy aggregate for displays which describe all boosters as one engine system.</summary>
    public float PropulsionCondition => Math.Min(Math.Min(MainBoosterCondition, ReverseBoosterCondition),
        Math.Min(SideLeftCondition, SideRightCondition));
    public float WeaponsCondition => LanceCondition;
    public float ShieldsCondition => ShieldGeneratorCondition;

    public static IReadOnlyList<ShipSubsystem> DamageableSubsystems { get; } =
        [ShipSubsystem.MainBooster, ShipSubsystem.ReverseBooster, ShipSubsystem.SideLeft, ShipSubsystem.SideRight,
         ShipSubsystem.Lance, ShipSubsystem.Shields, ShipSubsystem.Reactor];

    public float Get(ShipSubsystem system) => system switch
    {
        ShipSubsystem.MainBooster => MainBoosterCondition,
        ShipSubsystem.ReverseBooster => ReverseBoosterCondition,
        ShipSubsystem.SideLeft => SideLeftCondition,
        ShipSubsystem.SideRight => SideRightCondition,
        ShipSubsystem.Lance => LanceCondition,
        ShipSubsystem.Shields => ShieldGeneratorCondition,
        ShipSubsystem.Reactor => ReactorCondition,
        _ => 0f
    };
    internal void Set(ShipSubsystem system, float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        if (system == ShipSubsystem.MainBooster) MainBoosterCondition = value;
        else if (system == ShipSubsystem.ReverseBooster) ReverseBoosterCondition = value;
        else if (system == ShipSubsystem.SideLeft) SideLeftCondition = value;
        else if (system == ShipSubsystem.SideRight) SideRightCondition = value;
        else if (system == ShipSubsystem.Lance) LanceCondition = value;
        else if (system == ShipSubsystem.Shields) ShieldGeneratorCondition = value;
        else if (system == ShipSubsystem.Reactor) ReactorCondition = value;
    }

    internal float ApplyDebuff(ShipSubsystem system)
    {
        float after = system == ShipSubsystem.Reactor ? ReactorDebuffedCondition : DebuffedCondition;
        Set(system, after);
        return after;
    }

    internal void Repair()
    {
        Set(ShipSubsystem.MainBooster, 1f); Set(ShipSubsystem.ReverseBooster, 1f);
        Set(ShipSubsystem.SideLeft, 1f); Set(ShipSubsystem.SideRight, 1f);
        Set(ShipSubsystem.Lance, 1f); Set(ShipSubsystem.Shields, 1f); Set(ShipSubsystem.Reactor, 1f);
        SensorsCondition = 1f;
    }
}
