using System.Numerics;

namespace SpaceSim.Core.Navigation;

/// <summary>One-tick intent for the authoritative hyperspace navigation state machine.</summary>
public readonly record struct NavigationCommand(
    int? JumpToEncounterId = null,
    bool EnterHyperspace = false,
    Vector3? EntryPosition = null,
    int? QuickStartEncounterId = null,
    float QuickStartDistanceMeters = 3_000f);
