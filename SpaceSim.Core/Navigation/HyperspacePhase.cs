namespace SpaceSim.Core.Navigation;

/// <summary>Authoritative navigation phase. The player ship exists in real space only.</summary>
public enum HyperspacePhase
{
    RealSpace,
    SelectingDestination,
    PlanningEntry
}
