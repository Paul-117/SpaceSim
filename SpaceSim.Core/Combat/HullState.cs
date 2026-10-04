namespace SpaceSim.Core.Combat;

/// <summary>Damageable ship systems. Station names in the UI map Weapons to Armarium
/// and Reactor to Voltarium.</summary>
public enum ShipSubsystem { Propulsion, Weapons, Shields, Reactor, Sensors }

public sealed class HullState
{
    public int CurrentHull { get; internal set; }
    public int MaximumHull { get; }
    internal HullState(int maximumHull) { MaximumHull = maximumHull; CurrentHull = maximumHull; }
}

public sealed class SubsystemState
{
    public float PropulsionCondition { get; internal set; } = 1f;
    public float WeaponsCondition { get; internal set; } = 1f;
    public float ShieldsCondition { get; internal set; } = 1f;
    public float ReactorCondition { get; internal set; } = 1f;
    public float SensorsCondition { get; internal set; } = 1f;
    public float Get(ShipSubsystem system) => system switch
    {
        ShipSubsystem.Propulsion => PropulsionCondition,
        ShipSubsystem.Weapons => WeaponsCondition,
        ShipSubsystem.Shields => ShieldsCondition,
        ShipSubsystem.Reactor => ReactorCondition,
        ShipSubsystem.Sensors => SensorsCondition,
        _ => 0f
    };
    internal void Set(ShipSubsystem system, float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        if (system == ShipSubsystem.Propulsion) PropulsionCondition = value;
        else if (system == ShipSubsystem.Weapons) WeaponsCondition = value;
        else if (system == ShipSubsystem.Shields) ShieldsCondition = value;
        else if (system == ShipSubsystem.Reactor) ReactorCondition = value;
        else if (system == ShipSubsystem.Sensors) SensorsCondition = value;
    }
    internal void Repair()
    {
        Set(ShipSubsystem.Propulsion, 1f); Set(ShipSubsystem.Weapons, 1f); Set(ShipSubsystem.Shields, 1f);
        Set(ShipSubsystem.Reactor, 1f); Set(ShipSubsystem.Sensors, 1f);
    }
}
