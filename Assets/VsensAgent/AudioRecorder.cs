using UnityEngine;
using System.IO;

public class AudioRecorder : MonoBehaviour
{
    private string micDevice;
    private AudioClip recordedClip;
    private int sampleRate = 16000;
    private string filePath;
    private bool isRecording = false;

    void Start()
    {
        if (Microphone.devices.Length > 0)
        {
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
        // 按下 R 开始录音
        if (Input.GetKeyDown(KeyCode.R) && !isRecording)
        {
            StartRecording();
        }

        // 松开 R 停止录音
        if (Input.GetKeyUp(KeyCode.R) && isRecording)
        {
            StopRecording();
        }
    }

    void StartRecording()
    {
        if (micDevice == null) return;

        recordedClip = Microphone.Start(micDevice, false, 60, sampleRate); // 最多录 60 秒
        isRecording = true;
        Debug.Log("[Recorder] Recording started...");
    }

    void StopRecording()
    {
        Microphone.End(micDevice);
        isRecording = false;
        Debug.Log("[Recorder] Recording stopped.");
        SaveToWav();
    }

    void SaveToWav()
    {
        string folderPath = Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "UnityRecordings");
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "recorded_audio_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";
        filePath = Path.Combine(folderPath, fileName);

        WavUtility.FromAudioClip(recordedClip, out filePath, true);
        Debug.Log("[Recorder] Saved to: " + filePath);
    }

    public string GetLatestFilePath()
    {
        return filePath;
    }
}
