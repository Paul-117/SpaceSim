using System.Numerics;
using SpaceSim.Core.Simulation;

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

    public void Add(WorldState world, Vector3 position, List<SimulationEvent> events)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Z) || position.Y != 0f)
            throw new ArgumentException("Targets must have finite positions in the X/Z plane.");
        var target = new TargetState(_nextId++, position, _settings.TargetRadiusMeters);
        world.MutableTargets.Add(target);
        events.Add(new TargetSpawned(target.Id, target.Position));
    }

    public void MaintainPopulation(WorldState world, List<SimulationEvent> events)
    {
        float maxSquared = _settings.TargetRecycleDistanceMeters * _settings.TargetRecycleDistanceMeters;
        for (int i = world.MutableTargets.Count - 1; i >= 0; i--)
        {
            var target = world.MutableTargets[i];
            if (Vector3.DistanceSquared(target.Position, world.Ship.Position) <= maxSquared) continue;
            world.MutableTargets.RemoveAt(i);
            events.Add(new TargetDespawned(target.Id));
        }
        while (world.MutableTargets.Count < _settings.TargetCount)
            Add(world, RandomPosition(world), events);
    }

    private Vector3 RandomPosition(WorldState world)
    {
        Vector3 candidate = default;
        // Uniform area distribution; bounded retries avoid hanging on dense custom settings.
        for (int attempt = 0; attempt < 32; attempt++)
        {
            float angle = _random.NextSingle() * MathF.Tau;
            float min = _settings.SpawnMinDistanceMeters;
            float max = _settings.SpawnMaxDistanceMeters;
            float radius = MathF.Sqrt(min * min + _random.NextSingle() * (max * max - min * min));
            candidate = world.Ship.Position + new Vector3(MathF.Cos(angle), 0f, MathF.Sin(angle)) * radius;
            if (world.Targets.All(t => Vector3.Distance(t.Position, candidate) > 3f * t.RadiusMeters))
                break;
        }
        return candidate;
    }
}
