using UnityEngine;
using System.IO;

public class AudioRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [Tooltip("Leave empty to use the default microphone.")]
    public string micDevice = null;

    [Tooltip("Max duration to allocate for recording (in seconds).")]
    public int maxDuration = 60;

    [Tooltip("Sample rate (Hz) — 16000 is good for STT.")]
    public int sampleRate = 16000;

    [Header("Save Settings")]
    [Tooltip("Optional: Set a custom folder to save the .wav file.")]
    public string customSavePath;

    private AudioClip recordedClip;
    private string filePath;
    private bool isRecording = false;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
            if (string.IsNullOrEmpty(micDevice))
                micDevice = Microphone.devices[0];

            Debug.Log("[Recorder] Using microphone: " + micDevice);
        }
        else
        {
            Debug.LogError("[Recorder] No microphone found!");
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
        {
            StartRecording();
        }

        if (Input.GetKeyUp(KeyCode.R) && isRecording)
        {
            StopRecording();
        }
    }

    void StartRecording()
    {
        recordedClip = Microphone.Start(micDevice, false, maxDuration, sampleRate);
        isRecording = true;
        Debug.Log("[Recorder] Recording started...");
    }

    void StopRecording()
    {
        int position = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;

        if (position <= 0)
        {
            Debug.LogWarning("[Recorder] No audio data captured.");
            return;
        }

        // Trim to actual recorded samples
        float[] fullData = new float[recordedClip.samples * recordedClip.channels];
        recordedClip.GetData(fullData, 0);

        float[] trimmedData = new float[position * recordedClip.channels];
        System.Array.Copy(fullData, trimmedData, trimmedData.Length);

        AudioClip trimmedClip = AudioClip.Create("TrimmedClip", position, recordedClip.channels, sampleRate, false);
        trimmedClip.SetData(trimmedData, 0);
        recordedClip = trimmedClip;

        Debug.Log($"[Recorder] Trimmed to {(position / (float)sampleRate):0.00} seconds.");
        SaveToWav();

        // ✅ 通知 WebSocket
        WsClient.SendTranscribeRequest(filePath, GetRoomDescriptionJson());
    }

    void SaveToWav()
    {
        string folderPath = string.IsNullOrEmpty(customSavePath)
            ? Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "UnityRecordings")
            : customSavePath;

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "recorded_audio_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";
        filePath = Path.Combine(folderPath, fileName);

        WavUtility.FromAudioClip(recordedClip, filePath, true);  // ✅ 注意：你需要带路径版本的 WavUtility
        Debug.Log("[Recorder] Saved to: " + filePath);
    }

    // Support method to get room description in JSON format (Assuming RoomDescriber is set up)
    private string GetRoomDescriptionJson()
    {
        RoomDescriber describer = FindFirstObjectByType<RoomDescriber>();
        if (describer != null)
        {
            return describer.GetRoomDescription().ToString();
        }
        else
        {
            Debug.LogWarning("[Recorder] No RoomDescriber found in scene.");
            return "{}";
        }
    }

    public string GetLatestFilePath()
    {
        return filePath;
    }
}
