using System.Text.Json;
using Godot;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;
using NVector3 = System.Numerics.Vector3;

namespace SpaceSim.GodotClient.Logging;

/// <summary>
/// Writes one authoritative simulation-tick snapshot for one 1VS1 run. JSON lines keep the text file
/// both human-readable and easy to analyse with a script or another AI.
/// </summary>
internal sealed class DuelAiLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly JsonSerializerOptions _json = new() { WriteIndented = false };
    private bool _completed;

    public string Path { get; }

    public DuelAiLogger(EnemyAiModel model)
    {
        string modelFolder = model == EnemyAiModel.Basic ? "Basic AI" : model.ToString();
        string directory = ProjectSettings.GlobalizePath($"res://Logs/{modelFolder}");
        Directory.CreateDirectory(directory);
        Path = System.IO.Path.Combine(directory, $"ai_1vs1_{DateTime.Now:yyyy-MM-dd_HH-mm-ss-fff}.txt");
        _writer = new StreamWriter(Path, append: false) { AutoFlush = true };
        Write(new { type = "session_started", createdAt = DateTimeOffset.Now, model = model.ToString(),
            sampleIntervalSeconds = SimulationSettings.FixedDeltaSeconds, sampleRateHz = SimulationSettings.TickRate });
    }

    public void WriteSnapshot(WorldState world, SimulationSettings settings, ShipCommand playerCommand,
        IReadOnlyList<SimulationEvent> events)
    {
        EnemyShipState? enemy = world.CurrentEnemy ?? world.CurrentEncounter.Enemies.FirstOrDefault();
        if (enemy is null) return;
        EnemyAiController? ai = world.CurrentEncounter.GetEnemyAi(enemy.EnemyId);
        EnemyAiContext context = ai?.LastContext ?? default;
        Write(new
        {
            type = "snapshot",
            simulationTick = world.Tick,
            simulationSeconds = world.TimeSeconds,
            enemy = new
            {
                id = enemy.EnemyId,
                name = enemy.Name,
                shipClass = enemy.ShipClass.ToString(),
                difficulty = enemy.Difficulty.ToString(),
                model = ai?.Model.ToString() ?? "NONE",
                destroyed = enemy.IsDestroyed,
                state = ai?.CurrentState.ToString() ?? "NONE",
                detectedPlayer = ai?.IsPlayerDetected ?? false,
                command = Command(ai?.LastCommand ?? default),
                position = Vector(enemy.Ship.Position),
                velocity = Vector(enemy.Ship.Velocity),
                speedMetersPerSecond = enemy.Ship.Velocity.Length(),
                forward = Vector(enemy.Ship.Forward),
                angularVelocityRadiansPerSecond = enemy.Ship.AngularVelocity.Y,
                lance = Lance(enemy.Lance),
                shield = enemy.Ship.Shield.CurrentShield,
                hull = enemy.Ship.Hull.CurrentHull,
                reactorPercent = enemy.Ship.Reactor.OperatingLevelPercent,
                power = Power(enemy.Ship),
                systems = Systems(enemy.Ship)
            },
            player = new
            {
                position = Vector(world.Ship.Position),
                velocity = Vector(world.Ship.Velocity),
                speedMetersPerSecond = world.Ship.Velocity.Length(),
                forward = Vector(world.Ship.Forward),
                angularVelocityRadiansPerSecond = world.Ship.AngularVelocity.Y,
                command = Command(playerCommand),
                lance = Lance(world.Lance),
                shield = world.Ship.Shield.CurrentShield,
                hull = world.Ship.Hull.CurrentHull,
                reactorPercent = world.Ship.Reactor.OperatingLevelPercent,
                power = Power(world.Ship),
                systems = Systems(world.Ship)
            },
            aiContext = new
            {
                distanceMeters = context.DistanceToPlayer,
                relativePosition = Vector(context.RelativePosition),
                relativeVelocity = Vector(context.RelativeVelocity),
                relativeSpeedMetersPerSecond = context.RelativeVelocity.Length(),
                closingSpeedMetersPerSecond = context.ClosingSpeed,
                lineOfSightAngularVelocityRadiansPerSecond = context.LineOfSightAngularVelocity,
                enemyAimErrorDegrees = Degrees(context.EnemyAimError),
                playerAimErrorDegrees = Degrees(context.PlayerAimError),
                enemyLanceCharge = context.EnemyLanceCharge,
                playerLanceCharge = context.PlayerLanceCharge
            },
            events = events.Select(Event).ToArray(),
            settings = new
            {
                lanceRangeMeters = settings.LanceRangeMeters,
                collisionDistanceMeters = settings.ShipCollisionDistanceMeters
            }
        });
    }

    public void Complete(WorldState world, string outcome)
    {
        if (_completed) return;
        _completed = true;
        Write(new
        {
            type = "session_finished",
            outcome,
            simulationTick = world.Tick,
            simulationSeconds = world.TimeSeconds,
            playerHull = world.Ship.Hull.CurrentHull,
            enemyHull = world.CurrentEncounter.Enemies.FirstOrDefault()?.Ship.Hull.CurrentHull,
            finishedAt = DateTimeOffset.Now
        });
        _writer.Dispose();
    }

    public void Dispose() => CompleteWithoutWorld();

    private void CompleteWithoutWorld()
    {
        if (_completed) return;
        _completed = true;
        Write(new { type = "session_finished", outcome = "aborted", finishedAt = DateTimeOffset.Now });
        _writer.Dispose();
    }

    private void Write(object record) => _writer.WriteLine(JsonSerializer.Serialize(record, _json));

    private static object Vector(NVector3 vector) => new { x = vector.X, y = vector.Y, z = vector.Z };
    private static object Command(ShipCommand command) => new
    {
        mainThrust = command.MainThrust,
        mainThrustIntensity = command.MainThrustIntensity,
        reverseThrust = command.ReverseThrust,
        reverseThrustIntensity = command.ReverseThrustIntensity,
        yawLeft = command.YawLeft,
        yawRight = command.YawRight,
        yawIntensity = command.YawIntensity,
        fireLance = command.FireLance
    };
    private static object Lance(LanceState lance) => new { charge = lance.ChargeFraction, ready = lance.IsReady };
    private static object Power(ShipState ship) => new
    {
        available = ship.Power.EffectiveReactorOutput,
        propulsionDraw = ship.Power.PropulsionDraw,
        weaponsDraw = ship.Power.WeaponsDraw,
        shieldsDraw = ship.Power.ShieldsDraw,
        propulsionFactor = ship.Power.PropulsionPowerFactor,
        weaponsFactor = ship.Power.WeaponsPowerFactor,
        shieldsFactor = ship.Power.ShieldsPowerFactor
    };
    private static object Systems(ShipState ship) => new
    {
        propulsion = ship.Systems.PropulsionCondition,
        weapons = ship.Systems.WeaponsCondition,
        shields = ship.Systems.ShieldsCondition,
        reactor = ship.Systems.ReactorCondition,
        sensors = ship.Systems.SensorsCondition
    };

    private static object Event(SimulationEvent @event) => @event switch
    {
        WeaponFired shot => new
        {
            type = nameof(WeaponFired), owner = shot.Owner.ToString(), hitKind = shot.HitKind.ToString(),
            targetId = shot.TargetId, origin = Vector(shot.Origin), end = Vector(shot.End),
            visualFadeStart = shot.VisualFadeStart is { } fade ? Vector(fade) : null,
            visualEnd = shot.VisualEnd is { } visualEnd ? Vector(visualEnd) : null
        },
        ShieldHit hit => new
        {
            type = nameof(ShieldHit), targetOwner = hit.TargetOwner.ToString(), targetEnemyId = hit.TargetEnemyId,
            damage = hit.Damage, shieldBefore = hit.ShieldBefore, shieldAfter = hit.ShieldAfter, position = Vector(hit.Position)
        },
        HullDamaged hit => new
        {
            type = nameof(HullDamaged), targetOwner = hit.TargetOwner.ToString(), targetEnemyId = hit.TargetEnemyId,
            hullBefore = hit.HullBefore, hullAfter = hit.HullAfter, position = Vector(hit.Position)
        },
        EnemyDestroyed destroyed => new { type = nameof(EnemyDestroyed), enemyId = destroyed.EnemyId, position = Vector(destroyed.Position) },
        PlayerDestroyed destroyed => new { type = nameof(PlayerDestroyed), enemyId = destroyed.EnemyId, position = Vector(destroyed.Position) },
        ShipCollision collision => new { type = nameof(ShipCollision), enemyId = collision.EnemyId, position = Vector(collision.Position) },
        EnemyExplosion explosion => new { type = nameof(EnemyExplosion), enemyId = explosion.EnemyId, position = Vector(explosion.Position), distanceToPlayer = explosion.DistanceToPlayer },
        ShieldDepleted depleted => new { type = nameof(ShieldDepleted), targetOwner = depleted.TargetOwner.ToString(), targetEnemyId = depleted.TargetEnemyId, position = Vector(depleted.Position) },
        _ => new { type = @event.GetType().Name }
    };
    private static float Degrees(float radians) => radians * 180f / MathF.PI;
}
