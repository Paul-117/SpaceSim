namespace SpaceSim.Core.Simulation;

/// <summary>Intent from a sensor station. The authoritative simulation decides its encounter effects.</summary>
public readonly record struct SensorCommand(bool ActiveSonarPing = false, int? ConfirmedEnemyId = null);
