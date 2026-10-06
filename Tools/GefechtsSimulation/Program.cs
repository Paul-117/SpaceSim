using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using SpaceSim.GefechtsSimulation;

string root = FindRepositoryRoot();
bool openBrowser = !args.Contains("--no-browser", StringComparer.OrdinalIgnoreCase);
if (args.Contains("--batch", StringComparer.OrdinalIgnoreCase))
{
    SessionResult result = CombatBatchRunner.Run(root, new CombatLabRequest());
    Console.WriteLine($"{result.Id}: Nomad {result.NomadWins} : {result.EnemyWins} Gegner, {result.Timeouts} Timeouts.");
    return;
}

const string address = "http://127.0.0.1:41871";
var builder = WebApplication.CreateSlimBuilder(args);
builder.WebHost.UseUrls(address);
var app = builder.Build();
JsonSerializerOptions json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
app.MapGet("/api/sessions", () => Results.Json(Sessions(root), json));
app.MapGet("/api/session/{id}", (string id) =>
{
    if (!SafeId(id)) return Results.BadRequest();
    string file = Path.Combine(LogRoot(root), id, "session.json");
    if (!File.Exists(file)) return Results.NotFound();
    SessionResult? result = JsonSerializer.Deserialize<SessionResult>(File.ReadAllText(file));
    return result is null ? Results.NotFound() : Results.Json(result, json);
});
app.MapPost("/api/run", (CombatLabRequest? request) =>
{
    try { return Results.Json(CombatBatchRunner.Run(root, request ?? new CombatLabRequest()), json); }
    catch (Exception error) { return Results.Problem(error.Message, statusCode: 500); }
});
app.MapGet("/{**asset}", (string? asset) =>
{
    asset = string.IsNullOrWhiteSpace(asset) ? "index.html" : asset;
    if (!new[] { "index.html", "combat-lab.js", "combat-lab.css" }.Contains(asset) || asset.Contains("..", StringComparison.Ordinal))
        return Results.NotFound();
    string file = Path.Combine(AppContext.BaseDirectory, "Ui", asset);
    if (!File.Exists(file)) return Results.NotFound();
    string type = asset.EndsWith(".css") ? "text/css; charset=utf-8" : asset.EndsWith(".js") ? "application/javascript; charset=utf-8" : "text/html; charset=utf-8";
    return Results.File(file, type);
});
Console.WriteLine("SpaceSim Gefechts-Simulation UI");
Console.WriteLine($"Bereit unter {address}/");
Console.WriteLine("Zum Beenden dieses Fenster schlie�en oder Strg+C dr�cken.");
app.Run();

static object[] Sessions(string root)
{
    string logs = LogRoot(root);
    if (!Directory.Exists(logs)) return [];
    return Directory.GetDirectories(logs).OrderByDescending(Directory.GetCreationTimeUtc).Select(directory =>
    {
        string id = Path.GetFileName(directory);
        string state = Path.Combine(directory, "session.json");
        if (File.Exists(state))
        {
            SessionResult? result = JsonSerializer.Deserialize<SessionResult>(File.ReadAllText(state));
            if (result is not null) return (object)new { id, createdAt = result.CreatedAt, result.Nomad, result.Enemy, result.NomadWins, result.EnemyWins, result.Timeouts, battleCount = result.Battles.Count, configured = true };
        }
        int count = Directory.GetFiles(directory, "gefecht_*.txt").Length;
        return (object)new { id, createdAt = Directory.GetCreationTimeUtc(directory), nomad = (DuelParameters?)null, enemy = (DuelParameters?)null, nomadWins = 0, enemyWins = 0, timeouts = 0, battleCount = count, configured = false };
    }).ToArray();
}
static string LogRoot(string root) => Path.Combine(root, "SpaceSim.Godot", "Logs", "Gefechts Simulationen");
static bool SafeId(string id) => !string.IsNullOrWhiteSpace(id) && id == Path.GetFileName(id) && !id.Contains("..", StringComparison.Ordinal);
static string FindRepositoryRoot()
{
    DirectoryInfo? directory = new(AppContext.BaseDirectory);
    while (directory is not null) { if (Directory.Exists(Path.Combine(directory.FullName, ".git"))) return directory.FullName; directory = directory.Parent; }
    throw new DirectoryNotFoundException("Repository root (.git) not found.");
}
