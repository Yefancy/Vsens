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
    private bool inputEnabled = true; 

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
        // only respond to R key if input is enabled
        if (!inputEnabled) 
        {
            // when input is disabled, ignore R key presses and releases warning.
            if (Input.GetKeyDown(KeyCode.R))
            {
                Debug.LogWarning("[AudioRecorder] ⚠️ R key pressed but input is DISABLED (chat focused)");
            }
            return;
        }
        
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
        
        // 🔥 通知ChatUI显示录音指示器
        var chatUI = FindFirstObjectByType<VsensAgent.ChatUIManager>();
        if (chatUI != null)
        {
            chatUI.ShowVoiceRecording(true);
        }
    }

    void StopRecording()
    {
        int position = Microphone.GetPosition(micDevice);
        Microphone.End(micDevice);
        isRecording = false;
        
        // 🔥 获取ChatUI引用（在方法开头获取一次）
        var chatUI = FindFirstObjectByType<VsensAgent.ChatUIManager>();
        
        // 通知ChatUI隐藏录音指示器
        if (chatUI != null)
        {
            chatUI.ShowVoiceRecording(false);
        }

        if (position <= 0)
        {
            Debug.LogWarning("[Recorder] No audio data captured.");
            return;
        }

        // Trim to actual recorded samples
        if (recordedClip == null)
        {
            Debug.LogError("[AudioRecorder] ❌ recordedClip is null! Cannot process audio.");
            return;
        }

        float[] fullData = new float[recordedClip.samples * recordedClip.channels];
        recordedClip.GetData(fullData, 0);

        float[] trimmedData = new float[position * recordedClip.channels];
        System.Array.Copy(fullData, trimmedData, trimmedData.Length);

        AudioClip trimmedClip = AudioClip.Create("TrimmedClip", position, recordedClip.channels, sampleRate, false);
        trimmedClip.SetData(trimmedData, 0);
        recordedClip = trimmedClip;

        SaveToWav();
        
        // 🔥 通知ChatUI添加语音消息
        if (chatUI != null && position > 0)
        {
            chatUI.AddVoiceMessage(filePath);
        }

        // ✅ 通知 WebSocket
        WsClient.SendTranscribeRequest(filePath);
    }

    void SaveToWav()
    {
        // 使用项目根目录的相对路径
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string folderPath = Path.Combine(projectRoot, "AudioRecordings", "input");

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string fileName = "recorded_audio_" + System.DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".wav";
        string absolutePath = Path.Combine(folderPath, fileName);
        
        // 保存相对路径用于WebSocket传输
        filePath = Path.Combine("AudioRecordings", "input", fileName);

        WavUtility.FromAudioClip(recordedClip, absolutePath, true);
    }

    public string GetLatestFilePath()
    {
        return filePath;
    }
    
    /// <summary>
    /// 设置是否启用输入控制（由InputController调用）
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;
        
        // 如果在禁用输入时正在录音，停止录音
        if (!enabled && isRecording)
        {
            StopRecording();
        }
    }
    
    /// <summary>
    /// 获取当前输入状态
    /// </summary>
    public bool IsInputEnabled()
    {
        return inputEnabled;
    }
}
