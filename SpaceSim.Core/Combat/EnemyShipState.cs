using SpaceSim.Core.Ships;
using SpaceSim.Core.Weapons;
using SpaceSim.Core.AI;
using SpaceSim.Core.Generation;

namespace SpaceSim.Core.Combat;

/// <summary>An enemy owns the same physical ship and lance state as the player.</summary>
public sealed class EnemyShipState
{
    public int EnemyId { get; }
    public string Name { get; }
    public EnemyShipClass ShipClass { get; }
    public EnemyDifficulty Difficulty { get; }
    public BoardComputerProfile BoardComputer { get; }
    /// <summary>Passive detection equipment; player ships do not use this enemy-only sensor model.</summary>
    public EnemySensorDefinition Sensor { get; }
    /// <summary>Actual draw reserved for the suite. Duel mode deliberately disables this so both ships remain fully powered.</summary>
    public float SensorPowerUsage { get; }
    /// <summary>
    /// The generated hardware manifest for procedural ships. Null means the ship uses an explicit
    /// or standard loadout instead.
    /// </summary>
    public GeneratedShipLoadout? GeneratedLoadout { get; }
    public ShipState Ship { get; }
    public LanceState Lance { get; } = new();
    public bool IsDestroyed { get; internal set; }

    internal EnemyShipState(int enemyId, string name, EnemyShipClass shipClass, ShipState ship, EnemyDifficulty difficulty,
        BoardComputerProfile boardComputer, EnemySensorDefinition sensor, float sensorPowerUsage,
        GeneratedShipLoadout? generatedLoadout = null)
    {
        EnemyId = enemyId;
        Name = name;
        ShipClass = shipClass;
        Ship = ship;
        Difficulty = difficulty;
        BoardComputer = boardComputer;
        Sensor = sensor ?? throw new ArgumentNullException(nameof(sensor));
        Sensor.Validate();
        SensorPowerUsage = Math.Clamp(sensorPowerUsage, 0f, Sensor.PowerUsage);
        GeneratedLoadout = generatedLoadout;
    }
}

/// <summary>Presentation and contact classification; all current classes share the same physical model.</summary>
public enum EnemyShipClass
{
    Interceptor,
    Corvette,
    Frigate
}
