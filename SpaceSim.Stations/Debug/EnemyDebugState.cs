using System.Numerics;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;

namespace SpaceSim.Stations.Debug;

/// <summary>
/// Read-only telemetry for the active enemy. This deliberately exposes a focused debug view,
/// rather than serialising the authoritative world state to a browser.
/// </summary>
public sealed record EnemyDebugState(
    bool EnemyAvailable,
    int EnemyId,
    string Difficulty,
    bool PlayerDetected,
    string AiState,
    float DistanceToPlayer,
    float ClosingSpeed,
    float RelativeSpeed,
    float EnemySpeed,
    float Hull,
    float MaximumHull,
    float Shield,
    float MaximumShield,
    float PropulsionCondition,
    float WeaponsCondition,
    float ShieldsCondition,
    float LanceCharge,
    bool LanceReady,
    float ReactorTargetOperatingLevelPercent,
    float ReactorOperatingLevelPercent,
    float ReactorAvailablePower,
    float ReactorCurrentDraw,
    float ReactorFuel,
    float ReactorFuelCapacity,
    float PropulsionRequested,
    float WeaponsRequested,
    float ShieldsRequested,
    float PropulsionDraw,
    float WeaponsDraw,
    float ShieldsDraw,
    long SimulationTick,
    FireControlDebug? EnemyFireControl = null,
    FireControlDebug? PlayerFireControl = null)
{
    public EnemyLoadoutDebug? Loadout { get; init; }
}

/// <summary>
/// Focused, read-only explanation of the live lance gates. It deliberately distinguishes
/// the AI's fire-command gates from the final ray/sphere hit check.
/// </summary>
public sealed record FireControlDebug(
    bool HasTarget,
    bool CombatActive,
    bool AttackState,
    bool LanceReady,
    bool TargetInRange,
    bool TargetInFront,
    float AimErrorDegrees,
    float AimToleranceDegrees,
    bool AimWithinTolerance,
    bool RayWouldHit,
    bool FireCommandWouldBeIssued)
{
    public static FireControlDebug Unavailable { get; } = new(
        false, false, false, false, false, false, 0f, 0f, false, false, false);
}

/// <summary>Read-only manifest of the modules actually mounted in the active enemy ship.</summary>
public sealed record EnemyLoadoutDebug(
    string ShipClass,
    string Source,
    int? Seed,
    string Subclass,
    int Score,
    float PeakPowerDemand,
    float RequiredReactorOutput,
    IReadOnlyList<LoadoutModuleDebug> Modules);

/// <summary>One concise module row for the browser debug terminal.</summary>
public sealed record LoadoutModuleDebug(string Slot, string Name, string Details);

public static class EnemyDebugStateBuilder
{
    public static EnemyDebugState Build(WorldState world, SimulationSettings? settings = null)
    {
        var enemy = world.CurrentEnemy;
        if (enemy is null) return Empty(world.Tick);

        EnemyAiController? ai = world.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
        var ship = enemy.Ship;
        var context = ai?.LastContext ?? default;
        var power = ship.Power;
        var reactor = ship.Reactor;
        return new EnemyDebugState(
            true,
            enemy.EnemyId,
            enemy.Difficulty.ToString().ToUpperInvariant(),
            ai?.IsPlayerDetected ?? false,
            ai?.CurrentState.ToString().ToUpperInvariant() ?? "UNAVAILABLE",
            context.DistanceToPlayer,
            context.ClosingSpeed,
            context.RelativeVelocity.Length(),
            ship.Velocity.Length(),
            ship.Hull.CurrentHull,
            ship.Hull.MaximumHull,
            ship.Shield.CurrentShield,
            ship.Shield.MaximumShield,
            ship.Systems.PropulsionCondition,
            ship.Systems.WeaponsCondition,
            ship.Systems.ShieldsCondition,
            enemy.Lance.ChargeFraction,
            enemy.Lance.IsReady,
            reactor.TargetOperatingLevelPercent,
            reactor.OperatingLevelPercent,
            reactor.AvailablePower,
            reactor.CurrentDraw,
            reactor.Fuel,
            reactor.FuelCapacity,
            power.PropulsionRequested,
            power.WeaponsRequested,
            power.ShieldsRequested,
            power.PropulsionDraw,
            power.WeaponsDraw,
            power.ShieldsDraw,
            world.Tick,
            BuildEnemyFireControl(world, enemy, ai, settings),
            BuildPlayerFireControl(world, enemy, settings))
        {
            Loadout = BuildLoadout(enemy)
        };
    }

    private static EnemyLoadoutDebug BuildLoadout(EnemyShipState enemy)
    {
        GeneratedShipLoadout? generated = enemy.GeneratedLoadout;
        ShipTuning tuning = enemy.Ship.Tuning;
        float peak = generated?.PeakPowerDemand ?? ShipLoadoutGenerator.PeakPowerDemand(
            tuning.MainBooster, tuning.ReverseBooster, tuning.SideBooster, tuning.BowWeapon, tuning.Shield, enemy.Sensor);
        float required = generated?.RequiredReactorOutputAtEightyPercent ??
            peak * ShipLoadoutGenerator.RequiredPowerCoverage;
        var modules = new List<LoadoutModuleDebug>
        {
            new("HULL", enemy.ShipClass.ToString().ToUpperInvariant(),
                $"{enemy.Ship.MassKg / 1_000f:0.##} t | {enemy.Ship.Hull.MaximumHull:0.#} HP"),
            new("BOARD COMPUTER", enemy.BoardComputer.Name,
                $"update {enemy.BoardComputer.CommandUpdateIntervalTicks} ticks | delay {enemy.BoardComputer.ReactionDelayTicks} ticks | fire x{enemy.BoardComputer.FireAimToleranceMultiplier:0.##} | yaw {enemy.BoardComputer.YawAuthorityFactor * 100f:0.#}%"),
            new("REACTOR", tuning.Reactor.Name,
                $"{tuning.Reactor.MaximumOutputPower:0.#} PU | ramp {tuning.Reactor.RampUpSeconds:0.#} s | fuel {tuning.Reactor.MaximumFuelUsagePerMinute:0.#}/min"),
            new("BOW WEAPON", tuning.BowWeapon.Name,
                $"{tuning.BowWeapon.Damage:0.#} dmg | {tuning.BowWeapon.RangeMeters:0.#} m | {tuning.BowWeapon.ChargeSeconds:0.#} s | {tuning.BowWeapon.PowerDraw:0.#} PU"),
            new("SHIELD", tuning.Shield.Name,
                $"{tuning.Shield.MaximumHitPoints:0.#} HP | recharge {tuning.Shield.RechargeSeconds:0.#} s | reboot {tuning.Shield.RebootSeconds:0.#} s | {tuning.Shield.PowerDraw:0.#} PU"),
            new("SENSOR", enemy.Sensor.Name,
                $"{enemy.Sensor.MinimumRangeMeters:0.#}-{enemy.Sensor.MaximumRangeMeters:0.#} m | {enemy.Sensor.PowerUsage:0.#} PU"),
            new("MAIN BOOSTER", tuning.MainBooster.Name,
                $"{tuning.MainBooster.ThrustNewtons / 1_000f:0.#} kN | ramp {tuning.MainBooster.RampUpSeconds:0.#} s | {tuning.MainBooster.MaximumSpeedMetersPerSecond:0.#} m/s | {tuning.MainBooster.PowerDraw:0.#} PU"),
            new("REVERSE BOOSTER", tuning.ReverseBooster.Name,
                $"{tuning.ReverseBooster.ThrustNewtons / 1_000f:0.#} kN | {tuning.ReverseBooster.MaximumSpeedMetersPerSecond:0.#} m/s | {tuning.ReverseBooster.PowerDraw:0.#} PU"),
            new("SIDE BOOSTER", tuning.SideBooster.Name,
                $"{tuning.SideBooster.ThrustNewtons / 1_000f:0.#} kN | {tuning.SideBooster.MaximumRotationDegreesPerSecond:0.#} deg/s | {tuning.SideBooster.PowerDraw:0.#} PU")
        };
        return new EnemyLoadoutDebug(enemy.ShipClass.ToString().ToUpperInvariant(),
            generated is null ? "STANDARD" : "PROCEDURAL", generated?.Seed,
            generated?.Subclass.ToString().ToUpperInvariant() ?? "-", generated?.Score ?? 0,
            peak, required, modules);
    }

    private static FireControlDebug BuildEnemyFireControl(WorldState world, EnemyShipState enemy,
        EnemyAiController? ai, SimulationSettings? settings)
    {
        Vector3 toPlayer = world.Ship.Position - enemy.Ship.Position;
        float distance = toPlayer.Length();
        Vector3 direction = distance > .0001f ? toPlayer / distance : enemy.Ship.Forward;
        float aimErrorDegrees = Degrees(SignedPlanarAngle(enemy.Ship.Forward, direction));
        float toleranceDegrees = Degrees(ai?.FireAimToleranceRadians ?? 0f);
        float weaponRange = enemy.Ship.Tuning.LanceRangeMeters;
        bool inRange = distance <= weaponRange;
        bool inFront = Vector3.Dot(enemy.Ship.Forward, direction) > 0f;
        bool aimed = MathF.Abs(aimErrorDegrees) <= toleranceDegrees + .0001f;
        bool attackState = ai?.CurrentState == EnemyAiState.Attack;
        bool combatActive = ai?.IsPlayerDetected ?? false;
        bool rayWouldHit = RayWouldHit(enemy.Ship.Position, enemy.Ship.Forward, world.Ship.Position,
            settings?.EnemyAi.ShipHitRadiusMeters ?? 16f, weaponRange);
        bool command = combatActive && attackState && enemy.Lance.IsReady && inRange && inFront && aimed;
        return new FireControlDebug(true, combatActive, attackState, enemy.Lance.IsReady, inRange, inFront,
            aimErrorDegrees, toleranceDegrees, aimed, rayWouldHit, command);
    }

    private static FireControlDebug BuildPlayerFireControl(WorldState world, EnemyShipState enemy,
        SimulationSettings? settings)
    {
        Vector3 toEnemy = enemy.Ship.Position - world.Ship.Position;
        float distance = toEnemy.Length();
        Vector3 direction = distance > .0001f ? toEnemy / distance : world.LanceDirection;
        float aimErrorDegrees = Degrees(SignedPlanarAngle(world.LanceDirection, direction));
        const float autopilotToleranceDegrees = AutopilotController.AimToleranceDegrees;
        float weaponRange = world.Ship.Tuning.LanceRangeMeters;
        bool inRange = distance <= weaponRange;
        bool inFront = Vector3.Dot(world.LanceDirection, direction) > 0f;
        bool aimed = MathF.Abs(aimErrorDegrees) <= autopilotToleranceDegrees + .0001f;
        bool rayWouldHit = RayWouldHit(world.Ship.Position, world.LanceDirection, enemy.Ship.Position,
            settings?.EnemyAi.ShipHitRadiusMeters ?? 16f, weaponRange);
        // The bridge can deliberately fire at any time. The other gates describe whether it will hit this contact.
        return new FireControlDebug(true, true, false, world.Lance.IsReady, inRange, inFront,
            aimErrorDegrees, autopilotToleranceDegrees, aimed, rayWouldHit, world.Lance.IsReady);
    }

    private static bool RayWouldHit(Vector3 origin, Vector3 direction, Vector3 center, float radius, float range)
    {
        Vector3 normalizedDirection = Vector3.Normalize(direction);
        Vector3 offset = center - origin;
        float alongRay = Vector3.Dot(offset, normalizedDirection);
        if (alongRay < 0f) return false;
        float perpendicularSquared = MathF.Max(0f, offset.LengthSquared() - alongRay * alongRay);
        float radiusSquared = radius * radius;
        if (perpendicularSquared > radiusSquared) return false;
        return alongRay - MathF.Sqrt(radiusSquared - perpendicularSquared) <= range;
    }

    private static float SignedPlanarAngle(Vector3 from, Vector3 to)
    {
        Vector3 a = Vector3.Normalize(new Vector3(from.X, 0f, from.Z));
        Vector3 b = Vector3.Normalize(new Vector3(to.X, 0f, to.Z));
        return MathF.Atan2(Vector3.Cross(a, b).Y, Vector3.Dot(a, b));
    }

    private static float Degrees(float radians) => radians * 180f / MathF.PI;

    private static EnemyDebugState Empty(long tick) => new(
        false, 0, "NONE", false, "NONE", 0f, 0f, 0f, 0f,
        0, 0, 0f, 0f, 0f, 0f, 0f, 0f, false,
        0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f, tick,
        FireControlDebug.Unavailable, FireControlDebug.Unavailable);
}
