using System.Diagnostics;

namespace SpaceSim.GodotClient.Voice;

public sealed record VoiceTranscriptionResult(bool Success, string Transcript, string Message)
{
    public static VoiceTranscriptionResult Failure(string message) => new(false, string.Empty, message);
}

/// <summary>Runs the locally installed whisper.cpp CLI against a recorded WAV file.</summary>
public sealed class WhisperSpeechRecognitionService
{
    private readonly string _executablePath;
    private readonly string _modelPath;

    public WhisperSpeechRecognitionService()
    {
        string root = Godot.ProjectSettings.GlobalizePath("res://Voice");
        _executablePath = Path.Combine(root, "Runtime", OperatingSystem.IsWindows() ? "whisper-cli.exe" : "whisper-cli");
        _modelPath = Path.Combine(root, "Models", "ggml-base.bin");
    }

    public async Task<VoiceTranscriptionResult> TranscribeAsync(string wavPath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_executablePath))
            return VoiceTranscriptionResult.Failure("VOICE OFFLINE: WHISPER CLI NOT INSTALLED");
        if (!File.Exists(_modelPath))
            return VoiceTranscriptionResult.Failure("VOICE OFFLINE: GERMAN MODEL NOT INSTALLED");
        if (!File.Exists(wavPath))
            return VoiceTranscriptionResult.Failure("VOICE RECORDING FAILED");

        string outputBase = Path.Combine(Path.GetDirectoryName(wavPath)!, Path.GetFileNameWithoutExtension(wavPath));
        string outputTextPath = outputBase + ".txt";
        try
        {
            var startInfo = new ProcessStartInfo(_executablePath)
            {
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                WorkingDirectory = Path.GetDirectoryName(_executablePath)!
            };
            startInfo.ArgumentList.Add("-m");
            startInfo.ArgumentList.Add(_modelPath);
            startInfo.ArgumentList.Add("-f");
            startInfo.ArgumentList.Add(wavPath);
            startInfo.ArgumentList.Add("-l");
            startInfo.ArgumentList.Add("de");
            startInfo.ArgumentList.Add("-nt");
            startInfo.ArgumentList.Add("-otxt");
            startInfo.ArgumentList.Add("-of");
            startInfo.ArgumentList.Add(outputBase);

            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Could not start whisper-cli.");
            Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
            await process.WaitForExitAsync(cancellationToken);
            string error = await standardError;
            _ = await standardOutput;
            if (process.ExitCode != 0)
                return VoiceTranscriptionResult.Failure($"WHISPER FAILED ({process.ExitCode}): {Shorten(error)}");
            if (!File.Exists(outputTextPath))
                return VoiceTranscriptionResult.Failure("WHISPER RETURNED NO TRANSCRIPT");

            string transcript = (await File.ReadAllTextAsync(outputTextPath, cancellationToken)).Trim();
            return string.IsNullOrWhiteSpace(transcript)
                ? VoiceTranscriptionResult.Failure("NO SPEECH DETECTED")
                : new VoiceTranscriptionResult(true, transcript, "VOICE TRANSCRIBED");
        }
        catch (OperationCanceledException)
        {
            return VoiceTranscriptionResult.Failure("VOICE TRANSCRIPTION CANCELLED");
        }
        catch (Exception exception)
        {
            return VoiceTranscriptionResult.Failure($"VOICE TRANSCRIPTION FAILED: {Shorten(exception.Message)}");
        }
        finally
        {
            TryDelete(outputTextPath);
            TryDelete(wavPath);
        }
    }

    private static string Shorten(string value) => string.IsNullOrWhiteSpace(value) ? "UNKNOWN ERROR" :
        value.Replace('\r', ' ').Replace('\n', ' ').Trim()[..Math.Min(120, value.Replace('\r', ' ').Replace('\n', ' ').Trim().Length)];

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
