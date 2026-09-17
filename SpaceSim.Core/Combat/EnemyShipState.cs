using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.AI;

namespace SpaceSim.Core.Combat;

/// <summary>An enemy owns the same physical ship and lance state as the player.</summary>
public sealed class EnemyShipState
{
    public int EnemyId { get; }
    public EnemyDifficulty Difficulty { get; }
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    public bool IsDestroyed { get; internal set; }

    internal EnemyShipState(int enemyId, ShipState ship, EnemyDifficulty difficulty)
    {
        EnemyId = enemyId;
        Ship = ship;
        Difficulty = difficulty;
    }
}
