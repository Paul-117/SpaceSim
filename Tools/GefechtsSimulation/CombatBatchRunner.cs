using System.Numerics;
using System.Text;
using System.Text.Json;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Generation;
using SpaceSim.Core.Power;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.GefechtsSimulation;

/// <summary>Legacy scalar tuning retained only so older saved sessions remain readable.</summary>
public sealed record DuelParameters(float MainBoosterKilonewtons = 100f, float ReverseBoosterKilonewtons = 30f,
    float SideBoosterKilonewtons = 1f, float ShieldRechargeSeconds = 5f, float LanceRangeMeters = 1000f, float LanceChargeSeconds = 5f);

public sealed record CombatLabRequest(int BattleCount = 5, DuelParameters? Nomad = null, DuelParameters? Enemy = null,
    int? Seed = null, CombatShipLoadout? NomadShip = null, CombatShipLoadout? EnemyShip = null);

public sealed record BattleResult(int Number, int Seed, string Outcome, double DurationSeconds, float MinimumDistanceMeters, string LogFile);
public sealed record SessionResult(string Id, DateTimeOffset CreatedAt, DuelParameters? Nomad, DuelParameters? Enemy,
    IReadOnlyList<BattleResult> Battles, int NomadWins, int EnemyWins, int Timeouts,
    CombatShipLoadout? NomadShip = null, CombatShipLoadout? EnemyShip = null);

public static class CombatBatchRunner
{
    private const int MaximumTicksPerBattle = SimulationSettings.TickRate * 180;

    public static SessionResult Run(string repositoryRoot, CombatLabRequest request)
    {
        int battleCount = Math.Clamp(request.BattleCount, 1, 50);
        int baseSeed = request.Seed ?? Random.Shared.Next(1, int.MaxValue - 500_000);
        CombatShipLoadout nomad = request.NomadShip ?? CombatShipLoadoutFactory.Generate(new CombatShipGenerationRequest(Seed: baseSeed));
        CombatShipLoadout enemy = request.EnemyShip ?? CombatShipLoadoutFactory.Generate(new CombatShipGenerationRequest(Seed: baseSeed + 13_337));
        GeneratedShipLoadout nomadGenerated = CombatShipLoadoutFactory.Build(nomad);
        GeneratedShipLoadout enemyGenerated = CombatShipLoadoutFactory.Build(enemy);
        DateTimeOffset createdAt = DateTimeOffset.Now;
        string id = $"{NamePart(nomad.Name)}_vs_{NamePart(enemy.Name)}_{createdAt:yyyy-MM-dd_HH-mm-ss-fff}";
        string directory = Path.Combine(repositoryRoot, "SpaceSim.Godot", "Logs", "Gefechts Simulationen", id);
        Directory.CreateDirectory(directory);
        var results = new List<BattleResult>();
        for (int index = 0; index < battleCount; index++)
            results.Add(RunBattle(index + 1, baseSeed + index * 7919, directory, nomad, enemy, nomadGenerated, enemyGenerated));

        var result = new SessionResult(id, createdAt, null, null, results,
            results.Count(x => x.Outcome == "nomad_sieg"), results.Count(x => x.Outcome == "gegner_sieg"),
            results.Count(x => x.Outcome == "timeout"), nomad, enemy);
        JsonSerializerOptions json = new() { WriteIndented = true };
        File.WriteAllText(Path.Combine(directory, "session.json"), JsonSerializer.Serialize(result, json), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "Zusammenfassung.txt"), Summary(result), new UTF8Encoding(false));
        return result;
    }

    private static BattleResult RunBattle(int number, int seed, string directory, CombatShipLoadout nomad, CombatShipLoadout enemy,
        GeneratedShipLoadout nomadGenerated, GeneratedShipLoadout enemyGenerated)
    {
        var random = new Random(seed);
        float bearing = (float)(random.NextDouble() * MathF.Tau);
        float nomadCourse = (float)(random.NextDouble() * MathF.Tau);
        float enemyCourse = (float)(random.NextDouble() * MathF.Tau);
        ShipInitialState nomadInitial = new(Vector3.Zero, Course(nomadCourse) * (float)(random.NextDouble() * 100d), -nomadCourse);
        ShipInitialState enemyInitial = new(Course(bearing) * 2_000f, Course(enemyCourse) * (float)(random.NextDouble() * 100d), -enemyCourse);
        var settings = new SimulationSettings
        {
            TargetCount = 0, EncounterTwoTargetCount = 0, EncounterThreeTargetCount = 0, EncounterFourTargetCount = 0,
            Power = new PowerSettings { ReactorSimulationEnabled = false }, Hull = new HullSettings { EnableSubsystemDamage = false },
            EnemyExplosion = new EnemyExplosionSettings { Enabled = false }
        };
        var simulation = new Simulation(settings, nomadInitial, seed, enemyInitial: enemyInitial, duelMode: true,
            duelAiModel: EnemyAiModel.Kestrel, playerTuning: nomadGenerated.Tuning, enemyTuning: enemyGenerated.Tuning,
            duelBoardComputer: enemyGenerated.BoardComputer, duelEnemyShipClass: enemy.ShipClass,
            duelPlayerMassKg: nomad.MassKg, duelPlayerMaximumHull: nomad.MaximumHull,
            duelEnemyLoadout: enemyGenerated, duelEnemyName: enemy.Name);
        EnemyShipState opponent = simulation.World.CurrentEnemy ?? throw new InvalidOperationException("Duel enemy unavailable.");
        var nomadAi = new DuelAiPilot(settings.EnemyAi, EnemyDifficulty.Hard,
            nomadGenerated.Tuning.ReverseThrustNewtons / nomad.MassKg, EnemyAiModel.Kestrel, seed + 1,
            nomadGenerated.BoardComputer, nomadGenerated.Subclass);
        EnemyAiController enemyAi = simulation.World.CurrentEncounter.GetEnemyAi(opponent.EnemyId) ?? throw new InvalidOperationException("Enemy AI unavailable.");
        string file = $"gefecht_{number:00}.txt";
        using var log = new CombatLogWriter(Path.Combine(directory, file), number, seed, nomad, enemy);
        float minimum = Vector3.Distance(simulation.World.Ship.Position, opponent.Ship.Position);
        for (int tick = 0; tick < MaximumTicksPerBattle; tick++)
        {
            ShipCommand command = nomadAi.Tick(simulation.World.Ship, simulation.World.Lance, opponent.Ship, opponent.Lance,
                simulation.World.Ship.Tuning.LanceRangeMeters, simulation.World.GameState);
            simulation.Step(command);
            minimum = Math.Min(minimum, Vector3.Distance(simulation.World.Ship.Position, opponent.Ship.Position));
            log.Snapshot(simulation.World, opponent, nomadAi, enemyAi, command, simulation.Events);
            if (opponent.IsDestroyed) { log.Complete(simulation.World, "player_victory"); return new(number, seed, "nomad_sieg", simulation.World.TimeSeconds, minimum, file); }
            if (simulation.World.GameState == GameState.GameOver) { log.Complete(simulation.World, "player_destroyed"); return new(number, seed, "gegner_sieg", simulation.World.TimeSeconds, minimum, file); }
        }
        log.Complete(simulation.World, "timeout");
        return new(number, seed, "timeout", simulation.World.TimeSeconds, minimum, file);
    }

    private static Vector3 Course(float radians) => new(MathF.Sin(radians), 0f, -MathF.Cos(radians));
    private static string NamePart(string name) => string.Concat(name.Where(char.IsLetterOrDigit)).ToLowerInvariant() switch { { Length: > 0 } safe => safe, _ => "ship" };

    private static string Summary(SessionResult result)
    {
        var text = new StringBuilder();
        text.AppendLine("SpaceSim Gefechts-Simulation");
        text.AppendLine($"Erstellt: {result.CreatedAt:O}");
        if (result.NomadShip is not null) AppendShip(text, "SCHIFF 1", result.NomadShip);
        if (result.EnemyShip is not null) AppendShip(text, "SCHIFF 2", result.EnemyShip);
        text.AppendLine();
        foreach (BattleResult battle in result.Battles)
            text.AppendLine($"Gefechtslauf {battle.Number:00}: {battle.Outcome}; Seed {battle.Seed}; Dauer {battle.DurationSeconds:F2} s; minimaler Abstand {battle.MinimumDistanceMeters:F1} m; {battle.LogFile}");
        text.AppendLine(); text.AppendLine($"Schiff-1-Siege: {result.NomadWins}"); text.AppendLine($"Schiff-2-Siege: {result.EnemyWins}"); text.AppendLine($"Timeouts: {result.Timeouts}");
        return text.ToString();
    }

    private static void AppendShip(StringBuilder text, string heading, CombatShipLoadout ship)
    {
        text.AppendLine($"{heading}: {ship.Name} · {ship.ShipClass} · {ship.Subclass} · {ship.MassKg / 1000f:0.00} t · Hull {ship.MaximumHull:0} HP");
        text.AppendLine($"  Boardcomputer: {ship.BoardComputerClass} MK {ship.BoardComputerMark}; Reaktor: {ReactorDefinitions.Get(ship.Reactor).Name}; Waffe: {BowWeaponDefinitions.Get(ship.BowWeapon).Name}");
        text.AppendLine($"  Schild: {ShieldDefinitions.Get(ship.Shield).Name}; Sensor: {EnemySensorDefinitions.Get(ship.Sensor).Name}");
        text.AppendLine($"  Booster: {BoosterDefinitions.MainAll.Single(x => x.Type == ship.MainBooster).Name} / {BoosterDefinitions.ReverseAll.Single(x => x.Type == ship.ReverseBooster).Name} / {BoosterDefinitions.SideAll.Single(x => x.Type == ship.SideBooster).Name}");
    }
}

internal sealed class CombatLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly string _nomadName;
    private bool _complete;
    public CombatLogWriter(string path, int battle, int seed, CombatShipLoadout nomad, CombatShipLoadout enemy)
    {
        _nomadName = nomad.Name;
        _writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        Write(new { type = "session_started", battleNumber = battle, seed, mode = "Kestrel_vs_Kestrel", sampleRateHz = SimulationSettings.TickRate, nomad, enemy });
    }
    public void Snapshot(WorldState world, EnemyShipState enemy, DuelAiPilot nomadAi, EnemyAiController enemyAi, ShipCommand command, IReadOnlyList<SimulationEvent> events) => Write(new
    {
        type = "snapshot", simulationTick = world.Tick, simulationSeconds = world.TimeSeconds,
        player = Ship(_nomadName, world.Ship, world.Lance, nomadAi.CurrentState, command),
        enemy = Ship(enemy.Name, enemy.Ship, enemy.Lance, enemyAi.CurrentState, enemyAi.LastCommand),
        aiContext = Context(enemyAi.LastContext), playerAiContext = Context(nomadAi.LastContext), events = events.Select(Event).ToArray(),
        settings = new { lanceRangeMeters = world.Ship.Tuning.LanceRangeMeters, collisionDistanceMeters = 50f }
    });
    public void Complete(WorldState world, string outcome) { if (_complete) return; _complete = true; Write(new { type = "session_finished", outcome, simulationTick = world.Tick, simulationSeconds = world.TimeSeconds }); }
    public void Dispose() { if (!_complete) Write(new { type = "session_finished", outcome = "aborted" }); _writer.Dispose(); }
    private void Write(object value) => _writer.WriteLine(JsonSerializer.Serialize(value));
    private static object Ship(string name, ShipState ship, LanceState lance, EnemyAiState state, ShipCommand command) => new { name, state = state.ToString(), command = new { mainThrust = command.MainThrust, reverseThrust = command.ReverseThrust, yawLeft = command.YawLeft, yawRight = command.YawRight, fireLance = command.FireLance }, position = V(ship.Position), velocity = V(ship.Velocity), forward = V(ship.Forward), speedMetersPerSecond = ship.Velocity.Length(), lance = new { charge = lance.ChargeFraction, ready = lance.IsReady }, shield = ship.Shield.CurrentShield, hull = ship.Hull.CurrentHull };
    private static object Context(EnemyAiContext c) => new { distanceMeters = c.DistanceToPlayer, relativeSpeedMetersPerSecond = c.RelativeVelocity.Length(), closingSpeedMetersPerSecond = c.ClosingSpeed, enemyAimErrorDegrees = D(c.EnemyAimError), playerAimErrorDegrees = D(c.PlayerAimError) };
    private static object Event(SimulationEvent e) => e switch { WeaponFired x => new { type = nameof(WeaponFired), owner = x.Owner.ToString(), hitKind = x.HitKind.ToString(), origin = V(x.Origin), end = V(x.End) }, ShieldHit x => new { type = nameof(ShieldHit), position = V(x.Position) }, HullDamaged x => new { type = nameof(HullDamaged), position = V(x.Position) }, EnemyDestroyed x => new { type = nameof(EnemyDestroyed), position = V(x.Position) }, PlayerDestroyed x => new { type = nameof(PlayerDestroyed), position = V(x.Position) }, ShipCollision x => new { type = nameof(ShipCollision), position = V(x.Position) }, _ => new { type = e.GetType().Name } };
    private static object V(Vector3 v) => new { x = v.X, y = v.Y, z = v.Z }; private static float D(float r) => r * 180f / MathF.PI;
}
