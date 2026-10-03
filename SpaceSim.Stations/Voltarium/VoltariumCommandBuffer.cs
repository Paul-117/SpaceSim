using System.Threading;
using SpaceSim.Core.Power;

namespace SpaceSim.Stations.Voltarium;

/// <summary>Thread-safe reactor setpoint intent. The simulation consumes it on its own physics tick.</summary>
public sealed class VoltariumCommandBuffer
{
    private int _levelTenths = -1;
    private long _allocationPacked = -1;

    public void SetOperatingLevel(float percent)
    {
        if (!float.IsFinite(percent)) return;
        Interlocked.Exchange(ref _levelTenths, (int)MathF.Round(Math.Clamp(percent, 0f, 100f) * 10f));
    }

    public bool TryReadOperatingLevel(out float percent)
    {
        int value = Interlocked.Exchange(ref _levelTenths, -1);
        percent = value < 0 ? 0f : value / 10f;
        return value >= 0;
    }

    public void SetAllocation(float bridgePercent, float shieldsPercent, float armariumPercent)
    {
        var allocation = new PowerAllocation(bridgePercent, shieldsPercent, armariumPercent);
        if (!allocation.IsValid) return;
        int bridgeTenths = (int)MathF.Round(bridgePercent * 10f);
        int shieldsTenths = (int)MathF.Round(shieldsPercent * 10f);
        int armariumTenths = (int)MathF.Round(armariumPercent * 10f);
        if (bridgeTenths + shieldsTenths + armariumTenths > 1000) return;
        long packed = (long)(uint)bridgeTenths | ((long)(uint)shieldsTenths << 11) | ((long)(uint)armariumTenths << 22);
        Interlocked.Exchange(ref _allocationPacked, packed);
    }

    public bool TryReadAllocation(out PowerAllocation allocation)
    {
        long packed = Interlocked.Exchange(ref _allocationPacked, -1);
        if (packed < 0)
        {
            allocation = default;
            return false;
        }
        allocation = new PowerAllocation((packed & 0x7ff) / 10f, ((packed >> 11) & 0x7ff) / 10f,
            ((packed >> 22) & 0x7ff) / 10f);
        return allocation.IsValid;
    }
}
