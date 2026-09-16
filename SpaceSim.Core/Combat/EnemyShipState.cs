using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.Combat;

/// <summary>An enemy owns the same physical ship and lance state as the player.</summary>
public sealed class EnemyShipState
{
    public int EnemyId { get; }
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    public bool IsDestroyed { get; internal set; }

    internal EnemyShipState(int enemyId, ShipState ship)
    {
        EnemyId = enemyId;
        Ship = ship;
    }
}

