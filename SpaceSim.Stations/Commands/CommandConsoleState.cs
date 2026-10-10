namespace SpaceSim.Stations.Commands;

/// <summary>Authoritative result of the last command executed by the simulation.</summary>
public sealed record CommandConsoleState(string LastCommand, string Message, bool Accepted, long SimulationTick)
{
    public static CommandConsoleState Waiting { get; } = new("", "WAITING FOR COMMAND", true, 0);
}
