using SpaceSim.Core.Targets;
using SpaceSim.Core.Combat;
using SpaceSim.Core.AI;
using System.Numerics;

namespace SpaceSim.Core.Navigation;

/// <summary>A local encounter retains its remaining targets for the whole session.</summary>
public sealed class EncounterState
{
    public int Id { get; }
    public string Name { get; }
    public int InitialTargetCount { get; }
    public int HitCount => InitialTargetCount - Targets.Count;
    internal List<TargetState> MutableTargets { get; } = new();
    public IReadOnlyList<TargetState> Targets { get; }
    private readonly List<EnemyShipState> _enemies = new();
    private readonly Dictionary<int, EnemyAiController> _enemyAis = new();
    // Sensorium confirmations persist for this encounter across hyperspace transitions.
    private readonly HashSet<int> _bridgeContactEnemyIds = new();
    public IReadOnlyList<EnemyShipState> Enemies { get; }
    /// <summary>Convenience access for encounters that contain exactly one enemy.</summary>
    public EnemyShipState? Enemy => _enemies.Count == 1 ? _enemies[0] : null;
    /// <summary>Convenience access for encounters that contain exactly one enemy.</summary>
    public EnemyAiController? EnemyAi => Enemy is { } enemy ? GetEnemyAi(enemy.EnemyId) : null;
    /// <summary>Optional neutral station available only in the station encounter.</summary>
    public ShopState? Shop { get; }
    /// <summary>Approximate hostile position shown while a combat encounter entry point is planned.</summary>
    public Vector3? LastKnownEnemyPosition { get; private set; }
    /// <summary>Known station position displayed while the player chooses a hyperspace entry point.</summary>
    public Vector3? LastKnownShopPosition { get; private set; }

    internal EncounterState(int id, string name, int initialTargetCount, Vector3? shopPosition = null)
    {
        Id = id;
        Name = name;
        InitialTargetCount = initialTargetCount;
        Shop = shopPosition is { } position ? new ShopState(position) : null;
        Targets = MutableTargets.AsReadOnly();
        Enemies = _enemies.AsReadOnly();
    }

    internal void AddEnemy(EnemyShipState enemy, EnemyAiController ai)
    {
        _enemies.Add(enemy);
        _enemyAis.Add(enemy.EnemyId, ai);
    }

    public EnemyAiController? GetEnemyAi(int enemyId) =>
        _enemyAis.TryGetValue(enemyId, out var ai) ? ai : null;

    public bool IsBridgeContactVisible(int enemyId) => _bridgeContactEnemyIds.Contains(enemyId);

    internal void RevealBridgeContact(int enemyId)
    {
        if (_enemies.Any(enemy => enemy.EnemyId == enemyId)) _bridgeContactEnemyIds.Add(enemyId);
    }

    internal void RevealAllBridgeContacts()
    {
        foreach (EnemyShipState enemy in _enemies) _bridgeContactEnemyIds.Add(enemy.EnemyId);
    }

    /// <summary>
    /// Captures the one navigational marker available during hyperspace planning. Combat encounters expose an
    /// intentionally imprecise last-known hostile position; the station encounter exposes its fixed location.
    /// </summary>
    internal void CaptureNavigationPosition(Random random, float enemyPositionErrorMeters = 300f)
    {
        if (Shop is { } shop)
        {
            LastKnownShopPosition = shop.Position;
            LastKnownEnemyPosition = null;
            return;
        }

        EnemyShipState? enemy = _enemies.FirstOrDefault(candidate => !candidate.IsDestroyed);
        if (enemy is null)
        {
            LastKnownEnemyPosition = null;
            return;
        }

        float angle = (float)(random.NextDouble() * MathF.Tau);
        // sqrt gives an even distribution across the error disc rather than crowding its centre.
        float radius = MathF.Sqrt((float)random.NextDouble()) * enemyPositionErrorMeters;
        LastKnownEnemyPosition = enemy.Ship.Position + new Vector3(MathF.Cos(angle) * radius, 0f,
            MathF.Sin(angle) * radius);
        LastKnownShopPosition = null;
    }

    public Vector3? LastKnownNavigationPosition => Shop is not null ? LastKnownShopPosition : LastKnownEnemyPosition;
}
