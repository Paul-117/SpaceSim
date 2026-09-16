using System.Numerics;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Navigation;

namespace SpaceSim.Core.Targets;

internal sealed class TargetSystem
{
    private readonly SimulationSettings _settings;
    private readonly Random _random;
    private int _nextId = 1;

    public TargetSystem(SimulationSettings settings, int seed)
    {
        _settings = settings;
        _random = new Random(seed);
    }

    private void Add(EncounterState encounter, Vector3 position, List<SimulationEvent> events)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Z) || position.Y != 0f)
            throw new ArgumentException("Targets must have finite positions in the X/Z plane.");
        var target = new TargetState(_nextId++, position, _settings.TargetRadiusMeters);
        encounter.MutableTargets.Add(target);
        events.Add(new TargetSpawned(target.Id, target.Position, encounter.Id));
    }

    public void Initialize(EncounterState encounter, Vector3 center, List<SimulationEvent> events,
        IEnumerable<Vector3>? initialTargets = null)
    {
        if (initialTargets is not null)
        {
            var positions = initialTargets.ToArray();
            if (positions.Length != encounter.InitialTargetCount)
                throw new ArgumentException("Initial target count must match the encounter.", nameof(initialTargets));
            foreach (var position in positions) Add(encounter, position, events);
            return;
        }
        // Called once per encounter, never after a hit, travel, or a return visit.
        for (int i = 0; i < encounter.InitialTargetCount; i++)
            Add(encounter, RandomPosition(encounter, center), events);
    }

    private Vector3 RandomPosition(EncounterState encounter, Vector3 center)
    {
        Vector3 candidate = default;
        // Uniform area distribution; bounded retries avoid hanging on dense custom settings.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = _random.NextSingle() * MathF.Tau;
            float min = _settings.SpawnMinDistanceMeters;
            float max = _settings.SpawnMaxDistanceMeters;
            float radius = MathF.Sqrt(min * min + _random.NextSingle() * (max * max - min * min));
            candidate = center + new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * radius;
            if (encounter.Targets.All(t => Vector3.Distance(t.Position, candidate) > 3f * t.RadiusMeters))
                break;
        }
        return candidate;
    }
}
