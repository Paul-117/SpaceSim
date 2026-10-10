using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;

namespace SpaceSim.GodotClient.Voice;

public sealed record IntentInterpretation(bool Resolved, string? Command, string Message)
{
    public static IntentInterpretation Unknown(string message) => new(false, null, message);
}

/// <summary>
/// Local Qwen intent fallback. It only converts free text into a small, validated command schema;
/// the Flight command layer remains the sole authority that changes simulation state.
/// </summary>
public sealed class LocalIntentService : IDisposable
{
    private const int Port = 47921;
    private static readonly HttpClient Client = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SemaphoreSlim _startupGate = new(1, 1);
    private readonly string _serverPath;
    private readonly string _modelPath;
    private Process? _server;
    private bool _disposed;

    public LocalIntentService()
    {
        string root = Godot.ProjectSettings.GlobalizePath("res://Voice");
        _serverPath = Path.Combine(root, "IntentRuntime", OperatingSystem.IsWindows() ? "llama-server.exe" : "llama-server");
        _modelPath = Path.Combine(root, "IntentModels", "Qwen3-1.7B-Q8_0.gguf");
    }

    public async Task<IntentInterpretation> InterpretAsync(string transcript, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_serverPath)) return IntentInterpretation.Unknown("INTENT OFFLINE: LLAMA SERVER NOT INSTALLED");
        if (!File.Exists(_modelPath)) return IntentInterpretation.Unknown("INTENT OFFLINE: QWEN3 MODEL NOT INSTALLED");
        if (!await EnsureServerAsync(cancellationToken)) return IntentInterpretation.Unknown("INTENT OFFLINE: QWEN3 DID NOT START");

        try
        {
            var request = new
            {
                model = "qwen3-intent",
                temperature = 0.0,
                max_tokens = 160,
                response_format = new { type = "json_object" },
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    // Qwen3 recognizes this chat-template marker and skips hidden reasoning,
                    // which keeps command interpretation fast and the output constrained.
                    new { role = "user", content = $"Spoken text: {transcript}\n/no_think" }
                }
            };
            using HttpResponseMessage response = await Client.PostAsJsonAsync(Endpoint("/v1/chat/completions"), request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return IntentInterpretation.Unknown($"INTENT REQUEST FAILED ({(int)response.StatusCode})");

            using JsonDocument document = JsonDocument.Parse(await response.Content.ReadAsStreamAsync(cancellationToken));
            string? content = document.RootElement.GetProperty("choices")[0].GetProperty("message")
                .GetProperty("content").GetString();
            return ParseResponse(content);
        }
        catch (OperationCanceledException) { return IntentInterpretation.Unknown("INTENT REQUEST CANCELLED"); }
        catch (Exception exception) { return IntentInterpretation.Unknown($"INTENT REQUEST FAILED: {Shorten(exception.Message)}"); }
    }

    private async Task<bool> EnsureServerAsync(CancellationToken cancellationToken)
    {
        if (await IsHealthyAsync(cancellationToken)) return true;
        await _startupGate.WaitAsync(cancellationToken);
        try
        {
            if (await IsHealthyAsync(cancellationToken)) return true;
            if (_server is null || _server.HasExited)
            {
                var startInfo = new ProcessStartInfo(_serverPath)
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = Path.GetDirectoryName(_serverPath)!
                };
                startInfo.ArgumentList.Add("-m");
                startInfo.ArgumentList.Add(_modelPath);
                startInfo.ArgumentList.Add("--host");
                startInfo.ArgumentList.Add("127.0.0.1");
                startInfo.ArgumentList.Add("--port");
                startInfo.ArgumentList.Add(Port.ToString(System.Globalization.CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("--ctx-size");
                startInfo.ArgumentList.Add("2048");
                startInfo.ArgumentList.Add("--threads");
                startInfo.ArgumentList.Add(Math.Clamp(Environment.ProcessorCount - 2, 2, 6).ToString(System.Globalization.CultureInfo.InvariantCulture));
                startInfo.ArgumentList.Add("--jinja");
                _server = Process.Start(startInfo);
            }

            for (int attempt = 0; attempt < 80; attempt++)
            {
                await Task.Delay(500, cancellationToken);
                if (await IsHealthyAsync(cancellationToken)) return true;
                if (_server is { HasExited: true }) return false;
            }
            return false;
        }
        finally { _startupGate.Release(); }
    }

    private static async Task<bool> IsHealthyAsync(CancellationToken cancellationToken)
    {
        try
        {
            using HttpResponseMessage response = await Client.GetAsync(Endpoint("/health"), cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) { return false; }
    }

    private static IntentInterpretation ParseResponse(string? response)
    {
        if (string.IsNullOrWhiteSpace(response)) return IntentInterpretation.Unknown("INTENT RETURNED NO RESPONSE");
        int start = response.IndexOf('{');
        int end = response.LastIndexOf('}');
        if (start < 0 || end <= start) return IntentInterpretation.Unknown("INTENT RETURNED INVALID JSON");
        try
        {
            using JsonDocument document = JsonDocument.Parse(response[start..(end + 1)]);
            JsonElement root = document.RootElement;
            string intent = root.TryGetProperty("intent", out JsonElement intentValue)
                ? intentValue.GetString()?.Trim().ToLowerInvariant() ?? "unknown" : "unknown";
            string state = root.TryGetProperty("state", out JsonElement stateValue)
                ? stateValue.GetString()?.Trim().ToLowerInvariant() ?? string.Empty : string.Empty;
            return intent switch
            {
                "autopilot" when state is "on" or "off" => new IntentInterpretation(true, $"AUTOPILOT {state.ToUpperInvariant()}", "INTENT: AUTOPILOT"),
                "sonar" when state is "on" or "off" => new IntentInterpretation(true, $"SONAR {state.ToUpperInvariant()}", "INTENT: SONAR"),
                "reactor" when root.TryGetProperty("percent", out JsonElement percent) && percent.TryGetInt32(out int value) && value is >= 0 and <= 100 =>
                    new IntentInterpretation(true, $"REACTOR {value}%", "INTENT: REACTOR"),
                _ => IntentInterpretation.Unknown("VOICE INTENT NOT UNDERSTOOD")
            };
        }
        catch (JsonException) { return IntentInterpretation.Unknown("INTENT RETURNED INVALID JSON"); }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try
        {
            if (_server is { HasExited: false }) _server.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        _server?.Dispose();
        _startupGate.Dispose();
    }

    private static Uri Endpoint(string path) => new($"http://127.0.0.1:{Port}{path}");

    private static string Shorten(string value) => string.IsNullOrWhiteSpace(value) ? "UNKNOWN ERROR" :
        value.Replace('\r', ' ').Replace('\n', ' ').Trim()[..Math.Min(120, value.Replace('\r', ' ').Replace('\n', ' ').Trim().Length)];

    private const string SystemPrompt = """
        You are a strict German voice-command intent parser for SpaceSim. Return exactly one JSON object and no other text.
        Valid outputs only:
        {"intent":"autopilot","state":"on"}
        {"intent":"autopilot","state":"off"}
        {"intent":"reactor","percent":0}
        {"intent":"sonar","state":"on"}
        {"intent":"sonar","state":"off"}
        {"intent":"unknown"}
        Infer intent from natural German phrasing. Reactor percent must be an integer from 0 through 100. Never invent an action.
        """;
}
