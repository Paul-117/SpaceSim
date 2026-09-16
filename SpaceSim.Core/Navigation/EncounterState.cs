using SpaceSim.Core.Targets;

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

    internal EncounterState(int id, string name, int initialTargetCount)
    {
        Id = id;
        Name = name;
        InitialTargetCount = initialTargetCount;
        Targets = MutableTargets.AsReadOnly();
    }
}
