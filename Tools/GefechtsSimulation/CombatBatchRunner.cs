using System.Numerics;
using System.Text;
using System.Text.Json;
using SpaceSim.Core.AI;
using SpaceSim.Core.Combat;
using SpaceSim.Core.Power;
using SpaceSim.Core.Ships;
using SpaceSim.Core.Simulation;
using SpaceSim.Core.Weapons;

namespace SpaceSim.GefechtsSimulation;

public sealed record DuelParameters(
    float MainBoosterKilonewtons = 144f,
    float ReverseBoosterKilonewtons = 72f,
    float SideBoosterKilonewtons = 4.68f,
    float ShieldRechargeSeconds = 5f,
    float LanceRangeMeters = 1000f,
    float LanceChargeSeconds = 3f);

public sealed record CombatLabRequest(int BattleCount = 5, DuelParameters? Nomad = null,
    DuelParameters? Enemy = null, int? Seed = null);

public sealed record BattleResult(int Number, int Seed, string Outcome, double DurationSeconds, float MinimumDistanceMeters, string LogFile);
public sealed record SessionResult(string Id, DateTimeOffset CreatedAt, DuelParameters Nomad, DuelParameters Enemy,
    IReadOnlyList<BattleResult> Battles, int NomadWins, int EnemyWins, int Timeouts);

public static class CombatBatchRunner
{
    private const int MaximumTicksPerBattle = SimulationSettings.TickRate * 180;
    private const float SideThrusterLeverArmMeters = 11.53f;

    public static SessionResult Run(string repositoryRoot, CombatLabRequest request)
    {
        int battleCount = Math.Clamp(request.BattleCount, 1, 50);
        DuelParameters nomad = Clamp(request.Nomad ?? new DuelParameters());
        DuelParameters enemy = Clamp(request.Enemy ?? new DuelParameters());
        int baseSeed = request.Seed ?? Random.Shared.Next(1, int.MaxValue - 500_000);
        DateTimeOffset createdAt = DateTimeOffset.Now;
        string id = $"kestrel_vs_kestrel_{createdAt:yyyy-MM-dd_HH-mm-ss-fff}";
        string directory = Path.Combine(repositoryRoot, "SpaceSim.Godot", "Logs", "Gefechts Simulationen", id);
        Directory.CreateDirectory(directory);
        var results = new List<BattleResult>();
        for (int index = 0; index < battleCount; index++)
            results.Add(RunBattle(index + 1, baseSeed + index * 7919, directory, nomad, enemy));

        var result = new SessionResult(id, createdAt, nomad, enemy, results,
            results.Count(x => x.Outcome == "nomad_sieg"), results.Count(x => x.Outcome == "gegner_sieg"),
            results.Count(x => x.Outcome == "timeout"));
        JsonSerializerOptions json = new() { WriteIndented = true };
        File.WriteAllText(Path.Combine(directory, "session.json"), JsonSerializer.Serialize(result, json), new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(directory, "Zusammenfassung.txt"), Summary(result), new UTF8Encoding(false));
        return result;
    }

    private static BattleResult RunBattle(int number, int seed, string directory, DuelParameters nomadParameters, DuelParameters enemyParameters)
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
        ShipTuning nomadTuning = Tuning(settings, nomadParameters);
        ShipTuning enemyTuning = Tuning(settings, enemyParameters);
        var simulation = new Simulation(settings, nomadInitial, seed, enemyInitial: enemyInitial, duelMode: true,
            duelAiModel: EnemyAiModel.Kestrel, playerTuning: nomadTuning, enemyTuning: enemyTuning);
        EnemyShipState enemy = simulation.World.CurrentEnemy ?? throw new InvalidOperationException("Duel enemy unavailable.");
        var nomadAi = new DuelAiPilot(settings.EnemyAi, EnemyDifficulty.Hard, nomadTuning.ReverseThrustNewtons / settings.ShipMassKg,
            EnemyAiModel.Kestrel, seed + 1);
        EnemyAiController enemyAi = simulation.World.CurrentEncounter.GetEnemyAi(enemy.EnemyId) ?? throw new InvalidOperationException("Enemy AI unavailable.");
        string file = $"gefecht_{number:00}.txt";
        using var log = new CombatLogWriter(Path.Combine(directory, file), number, seed, nomadParameters, enemyParameters);
        float minimum = Vector3.Distance(simulation.World.Ship.Position, enemy.Ship.Position);
        for (int tick = 0; tick < MaximumTicksPerBattle; tick++)
        {
            ShipCommand command = nomadAi.Tick(simulation.World.Ship, simulation.World.Lance, enemy.Ship, enemy.Lance,
                simulation.World.Ship.Tuning.LanceRangeMeters, simulation.World.GameState);
            simulation.Step(command);
            minimum = Math.Min(minimum, Vector3.Distance(simulation.World.Ship.Position, enemy.Ship.Position));
            log.Snapshot(simulation.World, enemy, nomadAi, enemyAi, command, simulation.Events);
            if (enemy.IsDestroyed) { log.Complete(simulation.World, "player_victory"); return new(number, seed, "nomad_sieg", simulation.World.TimeSeconds, minimum, file); }
            if (simulation.World.GameState == GameState.GameOver) { log.Complete(simulation.World, "player_destroyed"); return new(number, seed, "gegner_sieg", simulation.World.TimeSeconds, minimum, file); }
        }
        log.Complete(simulation.World, "timeout");
        return new(number, seed, "timeout", simulation.World.TimeSeconds, minimum, file);
    }

    private static ShipTuning Tuning(SimulationSettings settings, DuelParameters p) => ShipTuning.From(settings) with
    {
        MainThrustNewtons = p.MainBoosterKilonewtons * 1_000f,
        ReverseThrustNewtons = p.ReverseBoosterKilonewtons * 1_000f,
        YawTorqueNewtonMeters = p.SideBoosterKilonewtons * 1_000f * SideThrusterLeverArmMeters,
        LanceRangeMeters = p.LanceRangeMeters,
        LanceVisualRangeMeters = Math.Max(p.LanceRangeMeters, p.LanceRangeMeters * 3f),
        LanceChargeSeconds = p.LanceChargeSeconds,
        ShieldRechargePerSecond = settings.Shield.MaximumShield / p.ShieldRechargeSeconds
    };

    private static DuelParameters Clamp(DuelParameters p) => p with
    {
        MainBoosterKilonewtons = Math.Clamp(p.MainBoosterKilonewtons, 1f, 1_000f),
        ReverseBoosterKilonewtons = Math.Clamp(p.ReverseBoosterKilonewtons, 1f, 1_000f),
        SideBoosterKilonewtons = Math.Clamp(p.SideBoosterKilonewtons, .1f, 100f),
        ShieldRechargeSeconds = Math.Clamp(p.ShieldRechargeSeconds, .2f, 120f),
        LanceRangeMeters = Math.Clamp(p.LanceRangeMeters, 50f, 10_000f),
        LanceChargeSeconds = Math.Clamp(p.LanceChargeSeconds, .1f, 120f)
    };

    private static Vector3 Course(float radians) => new(MathF.Sin(radians), 0f, -MathF.Cos(radians));

    private static string Summary(SessionResult result)
    {
        var text = new StringBuilder();
        text.AppendLine("SpaceSim Gefechts-Simulation");
        text.AppendLine($"Erstellt: {result.CreatedAt:O}");
        text.AppendLine($"Nomad: Main {result.Nomad.MainBoosterKilonewtons} kN, Reverse {result.Nomad.ReverseBoosterKilonewtons} kN, Side {result.Nomad.SideBoosterKilonewtons} kN, Schild {result.Nomad.ShieldRechargeSeconds} s, Lanze {result.Nomad.LanceRangeMeters} m / {result.Nomad.LanceChargeSeconds} s");
        text.AppendLine($"Gegner: Main {result.Enemy.MainBoosterKilonewtons} kN, Reverse {result.Enemy.ReverseBoosterKilonewtons} kN, Side {result.Enemy.SideBoosterKilonewtons} kN, Schild {result.Enemy.ShieldRechargeSeconds} s, Lanze {result.Enemy.LanceRangeMeters} m / {result.Enemy.LanceChargeSeconds} s");
        text.AppendLine();
        foreach (BattleResult battle in result.Battles) text.AppendLine($"Gefechtslauf {battle.Number:00}: {battle.Outcome}; Seed {battle.Seed}; Dauer {battle.DurationSeconds:F2} s; minimaler Abstand {battle.MinimumDistanceMeters:F1} m; {battle.LogFile}");
        text.AppendLine(); text.AppendLine($"Nomad-Siege: {result.NomadWins}"); text.AppendLine($"Gegner-Siege: {result.EnemyWins}"); text.AppendLine($"Timeouts: {result.Timeouts}");
        return text.ToString();
    }
}

internal sealed class CombatLogWriter : IDisposable
{
    private readonly StreamWriter _writer;
    private bool _complete;
    public CombatLogWriter(string path, int battle, int seed, DuelParameters nomad, DuelParameters enemy)
    {
        _writer = new StreamWriter(path, false, new UTF8Encoding(false)) { AutoFlush = true };
        Write(new { type = "session_started", battleNumber = battle, seed, mode = "Kestrel_vs_Kestrel", sampleRateHz = SimulationSettings.TickRate, nomad, enemy });
    }
    public void Snapshot(WorldState world, EnemyShipState enemy, DuelAiPilot nomadAi, EnemyAiController enemyAi, ShipCommand command, IReadOnlyList<SimulationEvent> events) => Write(new
    {
        type = "snapshot", simulationTick = world.Tick, simulationSeconds = world.TimeSeconds,
        player = Ship("Nomad", world.Ship, world.Lance, nomadAi.CurrentState, command),
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
