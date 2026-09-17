namespace SpaceSim.Core.Combat;

public enum ShipSubsystem { Propulsion, Weapons, Shields }

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
    public float Get(ShipSubsystem system) => system switch
    {
        ShipSubsystem.Propulsion => PropulsionCondition,
        ShipSubsystem.Weapons => WeaponsCondition,
        _ => ShieldsCondition
    };
    internal void Set(ShipSubsystem system, float value)
    {
        value = Math.Clamp(value, 0f, 1f);
        if (system == ShipSubsystem.Propulsion) PropulsionCondition = value;
        else if (system == ShipSubsystem.Weapons) WeaponsCondition = value;
        else ShieldsCondition = value;
    }
    internal void Repair()
    {
        Set(ShipSubsystem.Propulsion, 1f); Set(ShipSubsystem.Weapons, 1f); Set(ShipSubsystem.Shields, 1f);
    }
}
