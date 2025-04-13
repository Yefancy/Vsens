using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Animations;
using Sensor;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using Vsens.data;
using Vsens.timeline;
using Vsens.trajectory;

namespace Vsens
{
    public class VsensPlatform : MonoBehaviour
    {
        public BodyAnimationController target;
        public SensorAttachable sensorAttachable;
        public BodyAnimationController[] actors;
        public IMUChart refIMUChart;
        public IMUChart[] virtualIMUChart;
        public IMUTrajectory imuTrajectory;
        public TimeLineController timeLineController;
        public SMPLX avatar;
        public bool globalTranslation;
        private float previewRange = 0.2f;
        private bool isAccMode = true;
            
        protected internal readonly Dictionary<VirtualIMUSensor, VirtualIMUSensor[]> actorIMUs = new();
        protected internal readonly Dictionary<VirtualIMUSensor, VirtualIMUSensor> avatarIMUs = new();
        public VirtualIMUSensor selectedSensor;
        private List<SensorData> synthesisIMUData = new();
        private SMPLX _targetSMPLX;
        private List<SensorData> refIMUData = new();
        
        public float currentProgress // from 0 - 1
        {
            get => target == null ? 0 : (target.normalizedTime % 1f);
            set
            {
                if (target == null) return;
                target.normalizedTime = value % 1f;
                target.PlayAnimationToTime();
                UpdateAndDrawIMUTrajectory();
            }
        }
        
        public bool IsAccMode
        {
            get => isAccMode;
            set
            {
                if (isAccMode == value) return;
                isAccMode = value;
                foreach (var chart in virtualIMUChart)
                {
                    chart.IsAccMode = isAccMode;
                }
                refIMUChart.IsAccMode = isAccMode;
                UpdateAndDrawIMUTrajectory();
            }
        }
        
        public float PreviewRange
        {
            get => previewRange;
            set
            {
                if (Mathf.Approximately(previewRange, value)) return;
                previewRange = value;
                foreach (var chart in virtualIMUChart)
                {
                    chart.PreviewRange = previewRange;
                }
                imuTrajectory.PreviewRange = previewRange;
                timeLineController.SetPreviewRange(previewRange);
            }
        }
        
        // Start is called before the first frame update
        private void Start()
        {
            if (sensorAttachable == null && target != null)
            {
                sensorAttachable = target.GetComponentInChildren<SensorAttachable>();
            }
            ApplyToAllActor(actor =>
            {
                actor.isPlaying = false;
            });
            foreach (var chart in virtualIMUChart)
            {
                chart.JumpProgress += progress =>
                {
                    currentProgress = progress;
                    PlayAnimation(false);
                };
            }
            
            IsAccMode = isAccMode;
            PreviewRange = previewRange;
            _targetSMPLX = target.GetComponent<SMPLX>();
            // run next frame
            StartCoroutine(LoadTargetCoroutine());
        }

        private void Update()
        {
            bool needReload = false;
            bool reloadData = false;
            // sync sensors
            var targetSensors = FindIMUsOnTheTarget();
            if (targetSensors.Length != actorIMUs.Count || targetSensors.Any(sensor => !actorIMUs.ContainsKey(sensor)))
            {
                needReload = true;
            }
            else
            {
                foreach (var entry in actorIMUs)
                {
                    var imu = entry.Key;
                    var sensors = entry.Value;
                    if (imu == null || imu.gameObject == null)
                    {
                        // remove the sensor
                        needReload = true;
                        break;
                    }
                    var localPosition = imu.transform.localPosition;
                    var localRotation = imu.transform.localRotation;
                    foreach (var sensor in sensors)
                    {
                        // make sure the parent object are same, local transform are same
                        if (imu.transform.parent.name != sensor.transform.parent.name)
                        {
                            needReload = true;
                            break;
                        }
                        sensor.transform.localPosition = localPosition;
                        sensor.transform.localRotation = localRotation;
                    }
                    if (needReload) break;

                    if (imu == selectedSensor)
                    {
                        // check if transform is changed
                        if (localRotation != avatarIMUs[imu].transform.localRotation ||
                            localPosition != avatarIMUs[imu].transform.localPosition)
                        {
                            reloadData = true;
                        }
                    }
                    avatarIMUs[imu].transform.localPosition = localPosition;
                    avatarIMUs[imu].transform.localRotation = localRotation;
                }
            }
            
            if (needReload)
            {
                LoadTarget();
                return;
            }
            if (reloadData)
            {
                // update the imu data
                SimulateVirtualIMU();
                UpdateAndDrawIMUTrajectory();
            }
            // update actors animation by following the target
            if (target == null || !target.hasAnimation) return;
            var currentTime = target.time;
            var previewTime = previewRange * target.AnimationPlayTime;
            // sync animations
            ApplyToAllActor((index, actor) =>
            {
                if (actor.getAnimation() != target.getAnimation()) actor.setAnimation(target.getAnimation());
                var middle = actors.Length / 2;
                var timeOffset = (index - middle) * previewTime / actors.Length;
                var targetTime = currentTime + timeOffset;
                actor.SetCurrentPose(target.getPose(targetTime));
                if (globalTranslation)
                {
                    actor.SetCurrentGlobalTranslation(target.getGlobalTranslation(targetTime));
                }
                else
                {
                    actor.Root.localPosition = Vector3.zero;
                }
            });
            var progress = currentProgress;
            foreach (var chart in virtualIMUChart)
            {
                chart.Progress = progress;
            }
            imuTrajectory.Progress = progress;  
            timeLineController.SetCurrentProgress(progress);
            timeLineController.SetAnimationLength(target.AnimationPlayTime);
            imuTrajectory.DrawTrajectory();
        }

        private IEnumerator LoadTargetCoroutine()
        {
            yield return new WaitForEndOfFrame();
            LoadTarget();
        }

        public void LoadTarget()
        {
            // clear up
            foreach (var imuSensor in actorIMUs.Values.SelectMany(virtualIMUs => virtualIMUs))
            {
                Destroy(imuSensor.gameObject);
            }
            foreach (var imuSensor in avatarIMUs.Values)
            {
                Destroy(imuSensor.gameObject);
            }
            actorIMUs.Clear();
            avatarIMUs.Clear();
            
            if (target == null) return;
            // apply the same animation to all actors.
            var rawAnimation = target.getAnimation();
            if (rawAnimation != null)
            {
                ApplyToAllActor(actor => actor.setAnimation(rawAnimation));
            }

            // place the same imus on all actors and avatar
            var imus = FindIMUsOnTheTarget();
            foreach (var imu in imus)
            {
                var boneIndex = Array.IndexOf(target.Bones, imu.transform.parent);
                if (boneIndex < 0)
                {
                    Debug.LogError($"imu {imu.name} not found in target");
                    continue;
                }

                var copied = new VirtualIMUSensor[actors.Length];
                var index = 0;
                ApplyToAllActor(actor =>
                {
                    var actorParent = actor.Bones[boneIndex];
                    var imuComponent = CopyShadowIMU(imu, actorParent);
                    copied[index] = imuComponent;
                    index++;
                });
                actorIMUs.Add(imu, copied);
                
                // on avatar
                var avatarParent = avatar.TransformFromName[imu.transform.parent.name];
                if (avatarParent == null)
                {
                    Debug.LogError($"imu {imu.name} not found in avatar");
                    continue;
                }
                var newImu = CopyShadowIMU(imu, avatarParent, false);
                newImu.showSelectedVisualization = false;
                newImu.canDeselect = false;
                newImu.onSelectedChanged += selected =>
                {
                    if (selected)
                    {
                        SelectedIMU(imu);
                    }
                    newImu.ShowPreview = selected;
                };
                avatarIMUs.Add(imu, newImu);
            }
            if (actorIMUs.Count > 0)
            {
                var first = actorIMUs.First();
                selectedSensor = null; 
                SelectedIMU(first.Key);
            }
        }

        private static VirtualIMUSensor CopyShadowIMU(VirtualIMUSensor imu, Transform parent, bool disableInteractable = true, bool disableVisualization = true, bool canBeTransform = false)
        {
            var newImu = Instantiate(imu.prefab == null ? imu.gameObject : imu.prefab, parent);
            newImu.gameObject.SetActive(true);
            newImu.name = imu.name;
            newImu.transform.localPosition = imu.transform.localPosition;
            newImu.transform.localRotation = imu.transform.localRotation;
            var imuComponent = newImu.GetComponent<VirtualIMUSensor>();
            imuComponent.registerOnStart = false;
            if (disableVisualization)
            {
                if (imuComponent.inActiveVisualization != null)
                {
                    Destroy(imuComponent.inActiveVisualization);
                    imuComponent.inActiveVisualization = null;
                }
            }
            if (disableInteractable)
            {
                imuComponent.interactable = false;
            }
            imuComponent.canBeTransform = canBeTransform;
            imuComponent.IsActive = false;
            return imuComponent;
        }

        public float[] GetBodyShape()
        {
            return _targetSMPLX.betas;
        }
        
        public void SetBodyShape(float[] betas)
        {
            var previous = GetBodyShape();
            if (betas.Length == previous.Length && betas.SequenceEqual(previous))
            {
                return;
            }
            var minY = _targetSMPLX.GetVerticesMinY();
            _targetSMPLX.betas = betas;
            _targetSMPLX.SetBetaShapes();
            var newMinY = _targetSMPLX.GetVerticesMinY();
            var diff = newMinY - minY;
            _targetSMPLX.transform.localPosition -= new Vector3(0, diff, 0);
            avatar.betas = betas;
            avatar.SetBetaShapes();
            avatar.transform.localPosition -= new Vector3(0, diff, 0);
            ApplyToAllActor(actor =>
            {
                actor.TryGetComponent<SMPLX>(out var smplx);
                if (smplx != null)
                {
                    smplx.betas = betas;
                    smplx.SetBetaShapes();
                    smplx.gameObject.transform.localPosition -= new Vector3(0, diff, 0);
                }
            });
        }

        public void ApplyToAllActor(Action<int, BodyAnimationController> consumer)
        {
            for (int i = 0; i < actors.Length; i++)
            {
                consumer.Invoke(i, actors[i]);
            }
        }

        public void ApplyToAllActor(Action<BodyAnimationController> consumer)
        {
            foreach (var t in actors)
            {
                consumer.Invoke(t);
            }
        }

        public VirtualIMUSensor[] FindIMUsOnTheTarget()
        {
            return sensorAttachable == null ? 
                target.GetComponentsInChildren<VirtualIMUSensor>() : 
                sensorAttachable.sensors.Where(sensor => sensor is VirtualIMUSensor).Cast<VirtualIMUSensor>().ToArray();
        }
        
        public void SelectedIMU(VirtualIMUSensor sensor)
        {
            if (selectedSensor != sensor)
            {
                selectedSensor = sensor;
                if (sensor == null)
                {
                    // avatar.GetComponentInChildren<IMUMarkerQuad>().imuSensor = null;
                    ApplyToAllActor(actor => actor.GetComponent<IMUMarkerQuad>().imuSensor = null);
                }
                else
                {
                    // avatar
                    // avatar.GetComponentInChildren<IMUMarkerQuad>().imuSensor = avatarIMUs[sensor];
                    avatarIMUs[sensor].isSelected = true;
            
                    // actors
                    var actorSensors = actorIMUs[sensor];
                    for (var i = 0; i < actors.Length; i++)
                    {
                        var actorSensor = actorSensors[i];
                        var marker = actors[i].GetComponentInChildren<IMUMarkerQuad>();
                        marker.imuSensor = actorSensor;
                    }
                }
            }
            SimulateVirtualIMU();
            UpdateAndDrawIMUTrajectory();
        }

        private void UpdateAndDrawIMUTrajectory()
        {
            var imuData = synthesisIMUData
                .Select(item => (VirtualIMUSensor.IMUSensorData)item.data)
                .Select(data => isAccMode ? data.Acceleration : data.Orientation).ToList();
            var maxValue = float.MinValue;
            var minValue = float.MaxValue;
            var minMag = float.MaxValue;
            var maxMag = 0f;
            foreach (var item in imuData)
            {
                var magnitude = item.magnitude;
                minValue = Mathf.Min(minValue, item.x);
                maxValue = Mathf.Max(maxValue, item.x);
                minValue = Mathf.Min(minValue, item.y);
                maxValue = Mathf.Max(maxValue, item.y);
                minValue = Mathf.Min(minValue, item.z);
                maxValue = Mathf.Max(maxValue, item.z);
                maxMag = Mathf.Max(maxMag, magnitude);
                minMag = Mathf.Min(minMag, magnitude);
            }
            imuTrajectory.UpdateIMUData(imuData, minValue, maxValue, minMag, maxMag);
        }

        public void SimulateVirtualIMU()
        {
            if (virtualIMUChart == null) return;
            if (selectedSensor == null)
            {
                synthesisIMUData = new List<SensorData>();
                foreach (var chart in virtualIMUChart)
                {
                    chart.updateIMUData(synthesisIMUData);
                }
                UpdateRefChart();
                return;
            }
            if (target == null || !target.hasAnimation) return;
            var smplx = target.GetComponent<SMPLX>();
            // simulate the virtual imu
            target.PlayAnimationTo(0);
            selectedSensor.ClearData();
            selectedSensor.ClearSmoothCache();
            selectedSensor.StartRecording();
            var time = 0f;
            var deltaTime = target.deltaTime;
            var animationTime = target.frameCount * target.deltaTime;
            while (time < animationTime)
            {
                target.PlayAnimationTo(time);
                if (smplx.usePoseCorrectives)
                {
                    smplx.UpdatePoseCorrectives();
                }
                selectedSensor.UpdateWorking(time, deltaTime);
                time += deltaTime;
            }
            selectedSensor.StopRecording();
            synthesisIMUData = selectedSensor.Data;
            target.PlayAnimationToTime();
            
            // update chart
            foreach (var chart in virtualIMUChart)
            {
                chart.updateIMUData(synthesisIMUData);
            }
            UpdateRefChart();
        }

        #region Player

        public void SetAnimation(RawAnimation rawAnimation)
        {
            if (target == null || target.getAnimation() == rawAnimation) return;
            target.setAnimation(rawAnimation);
            LoadTarget();
        }
        
        public void PlayAnimationTo(bool isPlaying, float normalizeTime = -1)
        {
            if (target == null) return;
            target.isPlaying = isPlaying;
            if (!(normalizeTime >= 0)) return;
            target.normalizedTime = normalizeTime;
            target.PlayAnimationToTime();
        }
        
        public void PlayAnimation(bool isPlaying)
        {
            PlayAnimationTo(isPlaying);
        }

        public bool IsPlaying()
        {
            return target != null && target.isPlaying;
        }

        #endregion

        #region Sensor Transform
        
        public void UpdateSensorPosition(Axis axis, float appended)
        {
            if (selectedSensor == null) return;
            var localPosition = selectedSensor.transform.localPosition;
            switch (axis)
            {
                case Axis.X:
                    localPosition.x += appended;
                    break;
                case Axis.Y:
                    localPosition.y += appended;
                    break;
                case Axis.Z:
                    localPosition.z += appended;
                    break;
            }
            selectedSensor.transform.localPosition = localPosition;
        }

        #endregion

        
        #region Sensor Data

        private void UpdateRefChart()
        {
            if (refIMUChart == null) return;
            if (selectedSensor == null)
            {
                refIMUChart.updateIMUData(new List<SensorData>());
                return;
            }
            var data = GetRefDataByTag(selectedSensor.name);
            refIMUChart.updateIMUData(data);
        }
        
        public void SetRefData(List<SensorData> refData)
        {
            refIMUData = refData;
            UpdateRefChart();
        }

        private List<SensorData> GetRefDataByTag(string sensorID)
        {
            var data = new List<SensorData>();
            foreach (var sensorData in refIMUData)
            {
                if (sensorData.sensorID == sensorID)
                {
                    data.Add(sensorData);
                }
            }
            return data;
        }

        public void SaveIMUData()
        {
            if (target == null || !target.hasAnimation) return;
            var data = new List<SensorData>();
            var smplx = target.GetComponent<SMPLX>();
            // simulate the virtual imu
            target.PlayAnimationTo(0);
            foreach (var sensor in actorIMUs.Keys)
            {
                sensor.ClearData();
                sensor.ClearSmoothCache();
                sensor.StartRecording();
            }
            var time = 0f;
            var deltaTime = target.deltaTime;
            var animationTime = target.frameCount * target.deltaTime;
            while (time < animationTime)
            {
                target.PlayAnimationTo(time);
                if (smplx.usePoseCorrectives)
                {
                    smplx.UpdatePoseCorrectives();
                }
                foreach (var sensor in actorIMUs.Keys) sensor.UpdateWorking(time, deltaTime);
                time += deltaTime;
            }
            foreach (var sensor in actorIMUs.Keys)
            {
                sensor.StopRecording();
                data.AddRange(sensor.Data);
            }
            target.PlayAnimationToTime();
            var date = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            var path = $"{Application.streamingAssetsPath}/RefIMUData";
            if (!System.IO.Directory.Exists(path))
            {
                System.IO.Directory.CreateDirectory(path);
            }
            var fileName = $"{path}/{target.name}_{date}.csv";
            using var writer = new System.IO.StreamWriter(fileName);
            writer.WriteLine("tag,time,ex,ey,ez,ax,ay,az,lx,ly,lz,x,y,z");
            foreach (var sensorData in data) writer.WriteLine(sensorData.ToCsvLine());
        }
        #endregion
        
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(VsensPlatform))]
    public class VsensPlatformEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var platform = (VsensPlatform) target;
            if (GUILayout.Button("Load Target"))
            {
                platform.LoadTarget();
            }
            if (platform.actorIMUs.Count > 0)
            {
                foreach (var entry in platform.actorIMUs)
                {
                    var imu = entry.Key;
                    if (GUILayout.Button($"Select IMU {imu.transform.parent.name}"))
                    {
                        platform.SelectedIMU(imu);
                    }
                }
            }
            platform.IsAccMode = GUILayout.Toggle(platform.IsAccMode, "Acceleration Mode");
            platform.PreviewRange = EditorGUILayout.Slider("Preview Range", platform.PreviewRange, 0, 1f);
        }
    }
    #endif
}
