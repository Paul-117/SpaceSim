using System.Collections.Concurrent;
using System.Diagnostics;
using Godot;

namespace SpaceSim.GodotClient.Voice;

/// <summary>
/// Speaks presentation-only Bridge notifications with the locally installed Piper runtime.
/// It has no authority over the simulation and silently stays idle when Piper is not installed.
/// </summary>
public partial class PiperNotificationSpeechService : Node
{
    private const int MaximumQueuedMessages = 6;
    private static readonly TimeSpan DuplicateSuppression = TimeSpan.FromMilliseconds(750);
    private readonly Queue<string> _queuedMessages = new();
    private readonly ConcurrentQueue<SynthesisResult> _synthesizedMessages = new();
    private readonly Dictionary<string, DateTime> _lastSpokenAt = new(StringComparer.Ordinal);
    private readonly string _runtimePath;
    private readonly string _modelPath;
    private AudioStreamPlayer _player = null!;
    private string? _currentWavPath;
    private int _isSynthesizing;
    private int _generation;
    private bool _warnedUnavailable;
    private bool _disposed;

    public PiperNotificationSpeechService()
    {
        string root = ProjectSettings.GlobalizePath("res://Voice");
        _runtimePath = Path.Combine(root, "PiperRuntime", OperatingSystem.IsWindows() ? "piper.exe" : "piper");
        _modelPath = Path.Combine(root, "PiperModels", "en_US-amy-medium.onnx");
    }

    public override void _Ready()
    {
        _player = new AudioStreamPlayer { Bus = "Master" };
        AddChild(_player);
    }

    /// <summary>Queues one notification for speech after it is already visible in the Bridge UI.</summary>
    public void Speak(string notification)
    {
        string text = notification?.Trim() ?? string.Empty;
        if (_disposed || string.IsNullOrWhiteSpace(text)) return;
        if (!IsAvailable())
        {
            if (!_warnedUnavailable)
            {
                GD.Print("Notification speech offline: install Piper Amy medium with Tools/Voice/Install-Piper_AmyMedium_Windows.ps1");
                _warnedUnavailable = true;
            }
            return;
        }

        DateTime now = DateTime.UtcNow;
        if (_lastSpokenAt.TryGetValue(text, out DateTime last) && now - last < DuplicateSuppression) return;
        _lastSpokenAt[text] = now;
        if (_queuedMessages.Count >= MaximumQueuedMessages) _queuedMessages.Dequeue();
        _queuedMessages.Enqueue(text);
    }

    /// <summary>Stops playback and drops queued, now stale notifications.</summary>
    public void Clear()
    {
        Interlocked.Increment(ref _generation);
        _queuedMessages.Clear();
        _lastSpokenAt.Clear();
        while (_synthesizedMessages.TryDequeue(out SynthesisResult? result))
            if (result is not null) TryDelete(result.WavPath);
        _player?.Stop();
        TryDelete(_currentWavPath);
        _currentWavPath = null;
    }

    public override void _Process(double delta)
    {
        if (_disposed) return;

        if (_currentWavPath is not null && !_player.Playing)
        {
            TryDelete(_currentWavPath);
            _currentWavPath = null;
        }

        if (_currentWavPath is null && _synthesizedMessages.TryDequeue(out SynthesisResult? synthesized) && synthesized is not null)
        {
            if (synthesized.Success && File.Exists(synthesized.WavPath))
            {
                AudioStreamWav? stream = AudioStreamWav.LoadFromFile(synthesized.WavPath);
                if (stream is not null)
                {
                    _currentWavPath = synthesized.WavPath;
                    _player.Stream = stream;
                    _player.Play();
                }
                else TryDelete(synthesized.WavPath);
            }
            else if (!string.IsNullOrWhiteSpace(synthesized.Error))
            {
                GD.Print($"Notification speech failed: {synthesized.Error}");
                TryDelete(synthesized.WavPath);
            }
        }

        if (_currentWavPath is null && Volatile.Read(ref _isSynthesizing) == 0 && _queuedMessages.Count > 0)
        {
            string message = _queuedMessages.Dequeue();
            int generation = Volatile.Read(ref _generation);
            string speechDirectory = GetTemporarySpeechDirectory();
            Interlocked.Exchange(ref _isSynthesizing, 1);
            _ = Task.Run(() => Synthesize(message, generation, speechDirectory));
        }
    }

    public override void _ExitTree()
    {
        _disposed = true;
        Clear();
    }

    private void Synthesize(string text, int generation, string speechDirectory)
    {
        string wavPath = Path.Combine(speechDirectory, $"notification_{Guid.NewGuid():N}.wav");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(wavPath)!);
            var startInfo = new ProcessStartInfo(_runtimePath)
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_runtimePath)!
            };
            startInfo.ArgumentList.Add("--model");
            startInfo.ArgumentList.Add(_modelPath);
            startInfo.ArgumentList.Add("--output_file");
            startInfo.ArgumentList.Add(wavPath);

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start Piper.");
            process.StandardInput.WriteLine(text);
            process.StandardInput.Close();
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync();
            string error = process.StandardError.ReadToEnd();
            if (!process.WaitForExit(30_000))
            {
                process.Kill(entireProcessTree: true);
                Publish(generation, SynthesisResult.Failure(wavPath, "Piper timed out"));
            }
            else if (process.ExitCode != 0 || !File.Exists(wavPath))
                Publish(generation, SynthesisResult.Failure(wavPath, Shorten(error)));
            else Publish(generation, SynthesisResult.Successful(wavPath));
            _ = standardOutput.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            Publish(generation, SynthesisResult.Failure(wavPath, Shorten(exception.Message)));
        }
        finally
        {
            Interlocked.Exchange(ref _isSynthesizing, 0);
        }
    }

    private bool IsAvailable() => File.Exists(_runtimePath) && File.Exists(_modelPath);

    private void Publish(int generation, SynthesisResult result)
    {
        if (_disposed || generation != Volatile.Read(ref _generation))
        {
            TryDelete(result.WavPath);
            return;
        }
        _synthesizedMessages.Enqueue(result);
    }

    private static string GetTemporarySpeechDirectory() =>
        ProjectSettings.GlobalizePath("user://notification_speech");

    private static string Shorten(string value) => string.IsNullOrWhiteSpace(value) ? "UNKNOWN ERROR" :
        value.Replace('\r', ' ').Replace('\n', ' ').Trim()[..Math.Min(140, value.Replace('\r', ' ').Replace('\n', ' ').Trim().Length)];

    private static void TryDelete(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private sealed record SynthesisResult(bool Success, string WavPath, string? Error)
    {
        public static SynthesisResult Successful(string wavPath) => new(true, wavPath, null);
        public static SynthesisResult Failure(string wavPath, string error) => new(false, wavPath, error);
    }
}
