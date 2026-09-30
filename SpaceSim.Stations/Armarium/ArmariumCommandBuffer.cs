using System.Threading;

namespace SpaceSim.Stations.Armarium;

/// <summary>
/// Thread-safe bridge for intents from the Armarium. It does not mutate the simulation;
/// Flight reads one snapshot per physics tick and turns it into a normal ShipCommand.
/// </summary>
public sealed class ArmariumCommandBuffer
{
    private int _pendingFire;
    private int _turretDirection;

    public void RequestFire() => Interlocked.Exchange(ref _pendingFire, 1);

    /// <param name="direction">-1 for port/left, 1 for starboard/right, 0 for neutral.</param>
    public void SetTurretDirection(int direction) => Volatile.Write(ref _turretDirection, Math.Clamp(direction, -1, 1));

    public ArmariumCommand ReadCommand() => new(
        AimLanceLeft: Volatile.Read(ref _turretDirection) < 0,
        AimLanceRight: Volatile.Read(ref _turretDirection) > 0,
        FireLance: Interlocked.Exchange(ref _pendingFire, 0) != 0);

    public void Clear()
    {
        Interlocked.Exchange(ref _pendingFire, 0);
        Volatile.Write(ref _turretDirection, 0);
    }

    public void ClearTurret() => Volatile.Write(ref _turretDirection, 0);
}

public readonly record struct ArmariumCommand(bool AimLanceLeft, bool AimLanceRight, bool FireLance);
