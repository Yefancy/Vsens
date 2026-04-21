using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Sensor;
using SimpleFileBrowser;
using UnityEngine;
using VsensAgent.Core;
using VsensAgent.Network;
using VsensAgent.Network.Protocol;

namespace VsensAgent.VirtualObject.Sensor
{
    public class VsensAgentSensorManager : MonoBehaviour
    {
        public readonly struct RecordingExportResult
        {
            public RecordingExportResult(bool saved, bool canceled, string directoryPath, int exportedFileCount)
            {
                this.saved = saved;
                this.canceled = canceled;
                this.directoryPath = directoryPath;
                this.exportedFileCount = exportedFileCount;
            }

            public bool saved { get; }
            public bool canceled { get; }
            public string directoryPath { get; }
            public int exportedFileCount { get; }
        }

        public static VsensAgentSensorManager Instance { get; private set; }

        [SerializeField] private List<VirtualSensor> _registeredSensors = new List<VirtualSensor>();
        [Header("Test Settings")]
        [SerializeField] private Transform testParent; // Test parent for sensor creation

        private SensorDataCenter _sensorDataCenter;
        private bool _isRecording;
        private float _recordingStartRealtime;
        private Func<string> _exportDirectoryResolver;
        private WsClient _wsClient;
        private Action<object> _recordingSnapshotSender;

        public bool IsRecording => _isRecording;
        public float RecordingDurationSeconds => _isRecording ? Mathf.Max(0f, Time.realtimeSinceStartup - _recordingStartRealtime) : 0f;

        private void Awake() 
        {
            if (Instance == null)
            {
                Instance = this;
                ServiceLocator.Register<VsensAgentSensorManager>(this);
                Debug.Log("[VsensAgentSensorManager] 🔧 Singleton instance initialized and registered to ServiceLocator");
            }
            else if (Instance != this)
            {
                Debug.LogWarning($"[VsensAgentSensorManager] ⚠️ Multiple instances detected. Destroying duplicate on {gameObject.name}");
                Destroy(this.gameObject);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }

            if (ServiceLocator.IsRegistered<VsensAgentSensorManager>() && ServiceLocator.Get<VsensAgentSensorManager>() == this)
            {
                ServiceLocator.Unregister<VsensAgentSensorManager>();
            }
        }

        public VsensAgentSensorManager()
        {
            // 移除构造函数中的单例逻辑，改用Awake
        }

        public bool StartSensorRecording()
        {
            var dataCenter = ResolveSensorDataCenter();
            if (dataCenter == null)
            {
                Debug.LogError("[VsensAgentSensorManager] ❌ Cannot start recording: SensorDataCenter not found.");
                return false;
            }

            dataCenter.StartRecording();
            _isRecording = true;
            _recordingStartRealtime = Time.realtimeSinceStartup;
            Debug.Log("[VsensAgentSensorManager] ⏺️ Started sensor recording.");
            return true;
        }

        public void SetExportDirectoryResolver(Func<string> resolver)
        {
            _exportDirectoryResolver = resolver;
        }

        public void SetRecordingSnapshotSender(Action<object> sender)
        {
            _recordingSnapshotSender = sender;
        }

        public void StopSensorRecordingAndExportAsync(Action<RecordingExportResult> onCompleted)
        {
            if (!TryStopSensorRecording(out var capturedData, out var immediateResult))
            {
                onCompleted?.Invoke(immediateResult);
                return;
            }

            var configuredBaseDirectory = ResolveConfiguredExportBaseDirectory();
            if (configuredBaseDirectory != null)
            {
                onCompleted?.Invoke(ExportCapturedData(capturedData, configuredBaseDirectory));
                return;
            }

            var initialPath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);
            if (!FileBrowser.ShowSaveDialog(
                    paths =>
                    {
                        var selectedDirectory = paths != null && paths.Length > 0 ? paths[0] : string.Empty;
                        onCompleted?.Invoke(ExportCapturedData(capturedData, selectedDirectory));
                    },
                    () => onCompleted?.Invoke(new RecordingExportResult(false, true, string.Empty, 0)),
                    FileBrowser.PickMode.FilesAndFolders,
                    false,
                    initialPath,
                    "sensor_data",
                    "Choose Folder For Recorded Sensor Data"))
            {
                Debug.LogWarning("[VsensAgentSensorManager] ⚠️ Failed to open SimpleFileBrowser for export directory selection.");
                onCompleted?.Invoke(new RecordingExportResult(false, true, string.Empty, 0));
            }
        }

        public RecordingExportResult StopSensorRecordingAndExport()
        {
            if (!TryStopSensorRecording(out var capturedData, out var immediateResult))
            {
                return immediateResult;
            }

            var configuredBaseDirectory = ResolveConfiguredExportBaseDirectory();
            if (configuredBaseDirectory == null)
            {
                Debug.LogWarning("[VsensAgentSensorManager] ⚠️ Synchronous export requires a configured export directory resolver. Use StopSensorRecordingAndExportAsync for interactive export.");
                return new RecordingExportResult(false, true, string.Empty, 0);
            }

            return ExportCapturedData(capturedData, configuredBaseDirectory);
        }

        private bool TryStopSensorRecording(out Dictionary<VirtualSensor, List<SensorData>> capturedData, out RecordingExportResult result)
        {
            capturedData = null;

            var dataCenter = ResolveSensorDataCenter();
            if (dataCenter == null)
            {
                Debug.LogError("[VsensAgentSensorManager] ❌ Cannot stop recording: SensorDataCenter not found.");
                _isRecording = false;
                result = new RecordingExportResult(false, false, string.Empty, 0);
                return false;
            }

            capturedData = dataCenter.StopRecording();
            _isRecording = false;

            if (capturedData == null || capturedData.Count == 0)
            {
                Debug.LogWarning("[VsensAgentSensorManager] ⚠️ No recorded sensor data to export.");
                result = new RecordingExportResult(false, false, string.Empty, 0);
                return false;
            }

            result = default;
            return true;
        }

        private RecordingExportResult ExportCapturedData(Dictionary<VirtualSensor, List<SensorData>> capturedData, string baseDirectory)
        {
            var targetDirectory = ResolveExportDirectory(baseDirectory);
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                Debug.Log("[VsensAgentSensorManager] ℹ️ Export canceled by user.");
                return new RecordingExportResult(false, true, string.Empty, 0);
            }

            Directory.CreateDirectory(targetDirectory);

            int exportedFiles = 0;
            foreach (var pair in capturedData)
            {
                if (pair.Key == null || pair.Value == null || pair.Value.Count == 0)
                {
                    continue;
                }

                var sensor = pair.Key;
                var safeSensorName = SanitizeFileName(sensor.name);
                var safeSensorType = SanitizeFileName(sensor.SensorDefinition().getSensorName());
                var filePath = Path.Combine(targetDirectory, $"{safeSensorName}_{safeSensorType}.csv");
                using var writer = new StreamWriter(filePath);
                writer.WriteLine($"tag,time,{sensor.SensorDefinition().getCsvHeader()}");
                foreach (var sample in pair.Value)
                {
                    writer.WriteLine(sample.ToCsvLine());
                }

                exportedFiles++;
            }

            Debug.Log($"[VsensAgentSensorManager] 💾 Exported {exportedFiles} sensor file(s) to {targetDirectory}");
            return new RecordingExportResult(exportedFiles > 0, false, targetDirectory, exportedFiles);
        }

        public void RegisterSensor(VirtualSensor sensor)
        {
            PruneRegisteredSensors();
            if (sensor == null)
            {
                return;
            }

            if (!_registeredSensors.Contains(sensor))
            {
                _registeredSensors.Add(sensor);
            }
            else
            {
                Debug.LogWarning($"Sensor {sensor.name} is already registered.");
            }
        }

        public void UnregisterSensor(VirtualSensor sensor)
        {
            PruneRegisteredSensors();
            if (sensor == null)
            {
                return;
            }

            _registeredSensors.Remove(sensor);
        }

        private SensorDataCenter ResolveSensorDataCenter()
        {
            _sensorDataCenter ??= SensorDataCenter.Instance;
            _sensorDataCenter ??= FindFirstObjectByType<SensorDataCenter>();
            return _sensorDataCenter;
        }

        private static string SanitizeFileName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "sensor";
            }

            var invalidChars = new string(Path.GetInvalidFileNameChars());
            var invalidRegex = $"[{Regex.Escape(invalidChars)}]";
            return Regex.Replace(value, invalidRegex, "_");
        }

        private string ResolveExportDirectory(string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                return string.Empty;
            }

            return Path.Combine(baseDirectory, DefaultExportFolderName());
        }

        private string ResolveConfiguredExportBaseDirectory()
        {
            if (_exportDirectoryResolver != null)
            {
                return _exportDirectoryResolver();
            }
            return null;
        }
        
        private WsClient ResolveWsClient()
        {
            if (_wsClient == null)
            {
                _wsClient = ServiceLocator.IsRegistered<WsClient>()
                    ? ServiceLocator.Get<WsClient>()
                    : FindFirstObjectByType<WsClient>();
            }
            return _wsClient;
        }

        private string DefaultExportFolderName()
        {
            var ws = ResolveWsClient();
            return $"{ws?.Username ?? "anonymous"}_{ws?.Us ?? "unknown"}_{DateTime.Now:yyyyMMdd_HHmmss}";
        }

        public bool TryBuildRecordingSnapshotUploadRequest(
            RecordingExportResult exportResult,
            out SensorRecordingSnapshotUploadRequest request)
        {
            request = null;

            if (!exportResult.saved || exportResult.canceled || string.IsNullOrWhiteSpace(exportResult.directoryPath))
            {
                return false;
            }

            if (!Directory.Exists(exportResult.directoryPath))
            {
                Debug.LogWarning($"[VsensAgentSensorManager] ⚠️ Recording export directory does not exist: {exportResult.directoryPath}");
                return false;
            }

            var csvFiles = Directory.GetFiles(exportResult.directoryPath, "*.csv")
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (csvFiles.Length == 0)
            {
                Debug.LogWarning($"[VsensAgentSensorManager] ⚠️ No CSV files found for recording snapshot upload in {exportResult.directoryPath}");
                return false;
            }

            request = new SensorRecordingSnapshotUploadRequest
            {
                timestamp_label = Path.GetFileName(exportResult.directoryPath),
                local_export_directory = exportResult.directoryPath,
                files = csvFiles.Select(BuildRecordingSnapshotUploadFile).ToArray(),
            };
            return true;
        }

        public bool TryUploadRecordingSnapshot(RecordingExportResult exportResult)
        {
            if (!TryBuildRecordingSnapshotUploadRequest(exportResult, out var request))
            {
                return false;
            }

            var sender = _recordingSnapshotSender ?? WsClient.SendMessage;
            sender?.Invoke(request);
            Debug.Log($"[VsensAgentSensorManager] ☁️ Uploaded recording snapshot metadata for {request.timestamp_label} ({request.files?.Length ?? 0} files).");
            return true;
        }

        private static SensorRecordingSnapshotUploadFile BuildRecordingSnapshotUploadFile(string filePath)
        {
            var fileName = Path.GetFileName(filePath);
            var sensorName = Path.GetFileNameWithoutExtension(fileName);
            var separatorIndex = sensorName.LastIndexOf('_');
            if (separatorIndex > 0)
            {
                sensorName = sensorName.Substring(0, separatorIndex);
            }

            return new SensorRecordingSnapshotUploadFile
            {
                file_name = fileName,
                sensor_name = sensorName,
                csv_content = File.ReadAllText(filePath),
            };
        }

        public List<string> GetRegisteredSensorNames()
        {
            PruneRegisteredSensors();
            return _registeredSensors.Select(sensor => sensor.SensorDefinition().getSensorName()).ToList();
        }

        public bool HasSensor(string sensorName)
        {
            PruneRegisteredSensors();
            return _registeredSensors.Any(prefab => prefab.SensorDefinition().getSensorName() == sensorName);
        }

        public bool TryRemoveSensor(string sensorObjectName, out string error)
        {
            if (string.IsNullOrWhiteSpace(sensorObjectName))
            {
                error = "Sensor object name is required.";
                return false;
            }

            var sensor = FindObjectsByType<VirtualSensor>(FindObjectsSortMode.None)
                .FirstOrDefault(candidate => candidate != null && candidate.name == sensorObjectName);
            if (sensor == null)
            {
                error = $"Sensor '{sensorObjectName}' not found.";
                return false;
            }

            UnregisterSensor(sensor);
            ResolveSensorDataCenter()?.UnregisterSensor(sensor);

            var sensorObject = sensor.gameObject;
            if (sensorObject == null)
            {
                error = $"Sensor '{sensorObjectName}' has no GameObject.";
                return false;
            }

            if (Application.isPlaying)
            {
                Destroy(sensorObject);
            }
            else
            {
                DestroyImmediate(sensorObject);
            }

            error = null;
            return true;
        }

        [CanBeNull]
        public VirtualSensor CreateSensorByName(string sensorName, Transform parent)
        {
            PruneRegisteredSensors();
            Debug.Log($"[SensorManager] 🔍 Attempting to create sensor: '{sensorName}'");
            Debug.Log($"[SensorManager] 📋 Available sensors: {string.Join(", ", GetRegisteredSensorNames())}");
            
            foreach (var prefab in _registeredSensors)
            {
                string prefabSensorName = prefab.SensorDefinition().getSensorName();
                Debug.Log($"[SensorManager] 🔍 Checking prefab sensor name: '{prefabSensorName}'");
                
                if (prefabSensorName != sensorName) continue;
                
                Debug.Log($"[SensorManager] ✅ Found matching sensor prefab, creating instance...");
                var created = Instantiate(prefab, parent);
                ResolveSensorDataCenter().RegisterSensor(created);
                created.prefab = prefab.gameObject;
                
                string parentInfo = parent != null ? parent.name : "Global (null parent)";
                Debug.Log($"[SensorManager] ✅ Successfully created sensor '{sensorName}' on '{parentInfo}'");
                return created;
            }
            
            Debug.LogWarning($"[SensorManager] ❌ No sensor found with name '{sensorName}'");
            return null;
        }
        
        // 获取测试用的父物体
        private Transform GetTestParent()
        {
            if (testParent != null) return testParent;
            return this.transform; // 如果没有指定，就用自己作为父物体
        }

        private void PruneRegisteredSensors()
        {
            _registeredSensors.RemoveAll(sensor => sensor == null);
        }
        
        #region #Test
        
        [ContextMenu("List All Registered Sensors")]
        public void TestListRegisteredSensors()
        {
            var sensorNames = GetRegisteredSensorNames();
            Debug.Log($"[SensorManager] 📋 Registered Sensors ({sensorNames.Count}):");
            for (int i = 0; i < sensorNames.Count; i++)
            {
                Debug.Log($"  {i + 1}. {sensorNames[i]}");
            }
        }
        
        [ContextMenu("Create VirtualLightSensor")]
        public void TestCreateLightSensor()
        {
            var parent = GetTestParent();
            var sensor = CreateSensorByName("OPTICAL", parent);
            if (sensor != null)
            {
                Debug.Log($"[SensorManager] 💡 Light sensor created successfully on {parent.name}");
            }
        }
        
        [ContextMenu("Create VirtualDistanceSensor")]
        public void TestCreateDistanceSensor()
        {
            var parent = GetTestParent();
            var sensor = CreateSensorByName("DISTANCE", parent);
            if (sensor != null)
            {
                Debug.Log($"[SensorManager] 📏 Distance sensor created successfully on {parent.name}");
            }
        }
        
        [ContextMenu("Create All Available Sensors")]
        public void TestCreateAllSensors()
        {
            var parent = GetTestParent();
            var sensorNames = GetRegisteredSensorNames();
            
            Debug.Log($"[SensorManager] 🏭 Creating all {sensorNames.Count} available sensors...");
            
            foreach (var sensorName in sensorNames)
            {
                var sensor = CreateSensorByName(sensorName, parent);
                if (sensor != null)
                {
                    // 给每个传感器一个稍微不同的位置，避免重叠
                    sensor.transform.localPosition += new Vector3(
                        UnityEngine.Random.Range(-0.5f, 0.5f), 
                        UnityEngine.Random.Range(-0.5f, 0.5f), 
                        UnityEngine.Random.Range(-0.5f, 0.5f)
                    );
                }
            }
        }
        
        [ContextMenu("Clear All Child Sensors")]
        public void TestClearAllSensors()
        {
            var parent = GetTestParent();
            var childSensors = parent.GetComponentsInChildren<VirtualSensor>();
            
            Debug.Log($"[SensorManager] 🧹 Clearing {childSensors.Length} sensors from {parent.name}...");
            
            for (int i = childSensors.Length - 1; i >= 0; i--)
            {
                if (childSensors[i] != null)
                {
                    string sensorName = childSensors[i].name;
                    if (Application.isPlaying)
                    {
                        Destroy(childSensors[i].gameObject);
                    }
                    else
                    {
                        DestroyImmediate(childSensors[i].gameObject);
                    }
                    Debug.Log($"[SensorManager] 🗑️ Removed {sensorName}");
                }
            }
        }
        
        [ContextMenu("Test Sensor Functionality")]
        public void TestSensorFunctionality()
        {
            var parent = GetTestParent();
            var sensors = parent.GetComponentsInChildren<VirtualSensor>();
            
            Debug.Log($"[SensorManager] 🔍 Testing {sensors.Length} sensors...");
            
            foreach (var sensor in sensors)
            {
                if (sensor != null)
                {
                    var definition = sensor.SensorDefinition();
                    Debug.Log($"[SensorManager] 📊 {sensor.name}: {definition.getSensorName()}");
                    
                    // 如果传感器有描述方法，调用它
                    try
                    {
                        var description = sensor.GetSensorDescription();
                        Debug.Log($"[SensorManager] 📝 Description: {description}");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogWarning($"[SensorManager] ⚠️ Failed to get description for {sensor.name}: {ex.Message}");
                    }
                }
            }
        }
        
        #endregion
    }
}
