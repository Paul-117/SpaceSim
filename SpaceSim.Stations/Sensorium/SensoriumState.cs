using SpaceSim.Core.Combat;
using SpaceSim.Core.Simulation;
using SpaceSim.Stations.Armarium;

namespace SpaceSim.Stations.Sensorium;

/// <summary>
/// Sensorium-only contact snapshot. It contains contact telemetry, never a WorldState or world coordinates.
/// Identification remains a station-side operator task; the bridge continues to use its existing contacts.
/// </summary>
public sealed record SensoriumContact(
    int EnemyId,
    string Name,
    string SignatureCode,
    string ShipClass,
    float BearingDegrees,
    float DistanceMeters,
    float ReactorOutputFraction,
    float ShieldFraction,
    float PropulsionOutputFraction,
    float WeaponsOutputFraction,
    int Hull,
    int MaximumHull);

public sealed record SensoriumState(IReadOnlyList<SensoriumContact> Contacts, long SimulationTick);

public static class SensoriumStateBuilder
{
    public static SensoriumState Build(WorldState world)
    {
        var contacts = world.CurrentEnemies.Select(enemy => BuildContact(world, enemy)).ToArray();
        return new SensoriumState(contacts, world.Tick);
    }

    private static SensoriumContact BuildContact(WorldState world, EnemyShipState enemy)
    {
        var ship = enemy.Ship;
        float bearing = CalculateNorthBearingDegrees(world.Ship.Position, ship.Position);
        float distance = ArmariumStateBuilder.CalculateHorizontalDistanceMeters(world.Ship.Position, ship.Position);
        return new SensoriumContact(enemy.EnemyId, enemy.Name, SignatureCode(enemy.ShipClass), enemy.ShipClass.ToString().ToUpperInvariant(),
            bearing, distance, Fraction(ship.Reactor.OperatingLevelPercent, 100f),
            Fraction(ship.Shield.CurrentShield, ship.Shield.MaximumShield),
            Fraction(ship.Power.PropulsionDraw, ship.Power.MaximumPropulsionDraw),
            Fraction(ship.Power.WeaponsDraw, ship.Power.MaximumWeaponsDraw),
            ship.Hull.CurrentHull, ship.Hull.MaximumHull);
    }

    private static string SignatureCode(EnemyShipClass shipClass) => shipClass switch
    {
        EnemyShipClass.Transporter => "CETUS",
        EnemyShipClass.Corvette => "ARGUS",
        EnemyShipClass.Frigate => "ATLAS",
        _ => "UNKNOWN"
    };

    /// <summary>World navigation bearing: zero is north (-Z); it never depends on the player's bow.</summary>
    public static float CalculateNorthBearingDegrees(System.Numerics.Vector3 origin, System.Numerics.Vector3 target)
    {
        System.Numerics.Vector3 delta = target - origin;
        delta.Y = 0f;
        return delta.LengthSquared() < 0.000001f ? 0f : MathF.Atan2(delta.X, -delta.Z) * 180f / MathF.PI;
    }

    private static float Fraction(float value, float maximum) => maximum <= 0f ? 0f : Math.Clamp(value / maximum, 0f, 1f);
}
