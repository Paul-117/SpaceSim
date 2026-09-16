using SpaceSim.Core.Ships;
using SpaceSim.Core.Targets;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.Navigation;

namespace SpaceSim.Core.Simulation;

public sealed class WorldState
{
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    public WarpDriveState WarpDrive { get; } = new();
    public IReadOnlyList<EncounterState> Encounters { get; }
    public EncounterState CurrentEncounter { get; internal set; }
    internal List<TargetState> MutableTargets => CurrentEncounter.MutableTargets;
    public IReadOnlyList<TargetState> Targets => CurrentEncounter.Targets;
    public int HitCount { get; internal set; }
    public long Tick { get; internal set; }
    public double TimeSeconds => (double)Tick / SimulationSettings.TickRate;

    internal WorldState(ShipState ship, EncounterState[] encounters)
    {
        Ship = ship;
        Encounters = Array.AsReadOnly(encounters);
        CurrentEncounter = encounters[0];
    }
}
