using System.Threading;

namespace SpaceSim.Stations.Armarium;

/// <summary>Thread-safe intent bridge. Many network requests before one tick still become one fire pulse.</summary>
public sealed class ArmariumFireCommandBuffer
{
    private int _pending;
    public void RequestFire() => Interlocked.Exchange(ref _pending, 1);
    public bool ConsumeFireImpulse() => Interlocked.Exchange(ref _pending, 0) != 0;
    public void Clear() => Interlocked.Exchange(ref _pending, 0);
}
