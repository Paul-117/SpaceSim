using Godot;

namespace SpaceSim.GodotClient.Voice;

/// <summary>
/// Records push-to-talk input from the local computer microphone. No audio is sent
/// to a station browser or to the network; only the local whisper.cpp process sees it.
/// </summary>
public partial class VoiceCommandRecorder : Node
{
    private const string CaptureBusName = "VoiceCommandCapture";
    private readonly WhisperSpeechRecognitionService _recognizer = new();
    private AudioEffectRecord _recordEffect = null!;
    private AudioStreamPlayer _microphone = null!;

    public bool IsRecording { get; private set; }
    public bool IsTranscribing { get; private set; }
    public event Action<string>? StatusChanged;
    public event Action<VoiceTranscriptionResult>? TranscriptionCompleted;

    public override void _Ready()
    {
        int busIndex = FindOrCreateCaptureBus();
        _recordEffect = new AudioEffectRecord();
        AudioServer.AddBusEffect(busIndex, _recordEffect);
        _microphone = new AudioStreamPlayer
        {
            Stream = new AudioStreamMicrophone(),
            Bus = CaptureBusName
        };
        AddChild(_microphone);
    }

    public void StartRecording()
    {
        if (IsRecording || IsTranscribing) return;
        _recordEffect.SetRecordingActive(true);
        _microphone.Play();
        IsRecording = true;
        StatusChanged?.Invoke("VOICE RECORDING — RELEASE V TO TRANSCRIBE");
    }

    public async Task StopRecordingAsync()
    {
        if (!IsRecording || IsTranscribing) return;
        IsRecording = false;
        _recordEffect.SetRecordingActive(false);
        _microphone.Stop();
        AudioStreamWav recording = _recordEffect.GetRecording();
        if (recording is null)
        {
            StatusChanged?.Invoke("VOICE RECORDING FAILED");
            return;
        }

        string voiceDirectory = "user://voice_commands";
        DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(voiceDirectory));
        string fileName = $"command_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.wav";
        string virtualPath = $"{voiceDirectory}/{fileName}";
        Error saveResult = recording.SaveToWav(virtualPath);
        if (saveResult != Error.Ok)
        {
            StatusChanged?.Invoke("VOICE RECORDING COULD NOT BE SAVED");
            return;
        }

        IsTranscribing = true;
        StatusChanged?.Invoke("VOICE TRANSCRIBING…");
        VoiceTranscriptionResult result = await _recognizer.TranscribeAsync(ProjectSettings.GlobalizePath(virtualPath));
        IsTranscribing = false;
        TranscriptionCompleted?.Invoke(result);
    }

    public override void _ExitTree()
    {
        if (_recordEffect is not null) _recordEffect.SetRecordingActive(false);
        _microphone?.Stop();
    }

    private static int FindOrCreateCaptureBus()
    {
        for (int index = 0; index < AudioServer.BusCount; index++)
            if (AudioServer.GetBusName(index) == CaptureBusName) return index;

        AudioServer.AddBus();
        int busIndex = AudioServer.BusCount - 1;
        AudioServer.SetBusName(busIndex, CaptureBusName);
        // AudioEffectRecord receives the original bus signal before the final mute.
        // Do not attenuate the AudioStreamPlayer itself: that would record near-silence
        // and makes Whisper hallucinate markers such as "[MUSIK]".
        AudioServer.SetBusMute(busIndex, true);
        return busIndex;
    }
}
