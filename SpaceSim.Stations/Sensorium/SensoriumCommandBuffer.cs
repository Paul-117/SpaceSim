using System.Threading;

namespace SpaceSim.Stations.Sensorium;

/// <summary>Thread-safe one-shot bridge for active-sonar emissions from the browser station.</summary>
public sealed class SensoriumCommandBuffer
{
    private int _activeSonarPing;
    private int _confirmedEnemyId;

    public void RequestActiveSonarPing() => Interlocked.Exchange(ref _activeSonarPing, 1);
    public void RequestContactIdentification(int enemyId)
    {
        if (enemyId > 0) Interlocked.Exchange(ref _confirmedEnemyId, enemyId);
    }

    public SensoriumCommand ReadCommand()
    {
        int enemyId = Interlocked.Exchange(ref _confirmedEnemyId, 0);
        return new(Interlocked.Exchange(ref _activeSonarPing, 0) != 0, enemyId > 0 ? enemyId : null);
    }
}

public readonly record struct SensoriumCommand(bool ActiveSonarPing, int? ConfirmedEnemyId = null);
