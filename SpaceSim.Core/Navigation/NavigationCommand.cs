namespace SpaceSim.Core.Navigation;

/// <summary>A one-tick request. Selecting a point on the map alone sends no command.</summary>
public readonly record struct NavigationCommand(int? JumpToEncounterId = null);
