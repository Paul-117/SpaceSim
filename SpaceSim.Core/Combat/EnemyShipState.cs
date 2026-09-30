using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.AI;

namespace SpaceSim.Core.Combat;

/// <summary>An enemy owns the same physical ship and lance state as the player.</summary>
public sealed class EnemyShipState
{
    public int EnemyId { get; }
    public string Name { get; }
    public EnemyShipClass ShipClass { get; }
    public EnemyDifficulty Difficulty { get; }
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    public bool IsDestroyed { get; internal set; }

    internal EnemyShipState(int enemyId, string name, EnemyShipClass shipClass, ShipState ship, EnemyDifficulty difficulty)
    {
        EnemyId = enemyId;
        Name = name;
        ShipClass = shipClass;
        Ship = ship;
        Difficulty = difficulty;
    }
}

/// <summary>Presentation and contact classification; all current classes share the same physical model.</summary>
public enum EnemyShipClass
{
    Scout,
    Raider,
    Destroyer
}
