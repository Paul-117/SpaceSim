using SpaceSim.Core.Targets;
using SpaceSim.Core.Combat;
using SpaceSim.Core.AI;

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
    public IReadOnlyList<EnemyShipState> Enemies { get; }
    /// <summary>Convenience access for encounters that contain exactly one enemy.</summary>
    public EnemyShipState? Enemy => _enemies.Count == 1 ? _enemies[0] : null;
    /// <summary>Convenience access for encounters that contain exactly one enemy.</summary>
    public EnemyAiController? EnemyAi => Enemy is { } enemy ? GetEnemyAi(enemy.EnemyId) : null;

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
}
