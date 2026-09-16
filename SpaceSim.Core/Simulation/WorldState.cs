using SpaceSim.Core.Ships;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Weapons;

namespace SpaceSim.Core.Simulation;

public sealed class WorldState
{
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    internal List<TargetState> MutableTargets { get; } = new();
    public IReadOnlyList<TargetState> Targets { get; }
    public int HitCount { get; internal set; }
    public long Tick { get; internal set; }
    public double TimeSeconds => (double)Tick / SimulationSettings.TickRate;

    internal WorldState(ShipState ship)
    {
        Ship = ship;
        Targets = MutableTargets.AsReadOnly();
    }
}
