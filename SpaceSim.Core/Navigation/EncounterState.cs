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
    /// <summary>Sensor estimate shown during hyperspace entry planning; it deliberately is not the live enemy position.</summary>
    public Vector3? LastKnownEnemyPosition { get; private set; }

    internal EncounterState(int id, string name, int initialTargetCount)
    {
        Id = id;
        Name = name;
        InitialTargetCount = initialTargetCount;
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

    internal void CaptureLastKnownEnemyPosition(Random random, float maximumErrorMeters)
    {
        EnemyShipState? enemy = _enemies.FirstOrDefault(candidate => !candidate.IsDestroyed);
        if (enemy is null)
        {
            LastKnownEnemyPosition = null;
            return;
        }
        float angle = (float)(random.NextDouble() * MathF.Tau);
        // sqrt gives an even distribution across the error disc rather than crowding its centre.
        float distance = MathF.Sqrt((float)random.NextDouble()) * maximumErrorMeters;
        LastKnownEnemyPosition = enemy.Ship.Position + new Vector3(MathF.Cos(angle) * distance, 0f, MathF.Sin(angle) * distance);
    }
}
