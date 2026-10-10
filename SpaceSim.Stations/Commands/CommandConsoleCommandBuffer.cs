using System.Collections.Concurrent;

namespace SpaceSim.Stations.Commands;

/// <summary>
/// Thread-safe, bounded hand-off for raw command-console input. Parsing and all
/// authority checks remain on the Godot simulation thread.
/// </summary>
public sealed class CommandConsoleCommandBuffer
{
    private const int MaximumCommandLength = 160;
    private readonly ConcurrentQueue<string> _commands = new();

    public void Request(string? command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;
        string value = command.Trim();
        if (value.Length > MaximumCommandLength) value = value[..MaximumCommandLength];
        _commands.Enqueue(value);
    }

    public bool TryRead(out string command) => _commands.TryDequeue(out command!);

    public void Clear()
    {
        while (_commands.TryDequeue(out _)) { }
    }
}
