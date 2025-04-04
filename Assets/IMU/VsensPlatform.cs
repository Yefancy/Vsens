using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Animations;
using IMU.data;
using IMU.timeline;
using IMU.trajectory;
using Sensor;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace IMU
{
    public class VsensPlatform : MonoBehaviour
    {
        public BodyAnimationController target;
        public BodyAnimationController[] actors;
        public IMUChart virtualIMUChart;
        public IMUTrajectory imuTrajectory;
        public TimeLineController timeLineController;
        public bool globalTranslation;
        private float previewRange = 0.2f;
        private bool isAccMode = true;
            
        protected internal Dictionary<VirtualIMUSensor, VirtualIMUSensor[]> availableIMUs = new();
        private VirtualIMUSensor selectedSensor;
        private List<SensorData> synthesisIMUData = new();
        
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
                virtualIMUChart.IsAccMode = isAccMode;
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
                virtualIMUChart.PreviewRange = previewRange;
                imuTrajectory.PreviewRange = previewRange;
                timeLineController.SetPreviewRange(previewRange);
            }
        }
        
        // Start is called before the first frame update
        private void Start()
        {
            ApplyToAllActor(actor =>
            {
                actor.isPlaying = false;
                actor.AddComponent<IMUMarkerRendering>();
            });
            // run next frame
            StartCoroutine(LoadTargetCoroutine());
            virtualIMUChart.jumpProgress = progress => currentProgress = progress;
            IsAccMode = isAccMode;
            PreviewRange = previewRange;
        }

        private void Update()
        {
            // update actors animation by following the target
            if (target == null || !target.hasAnimation) return;
            var currentTime = target.time;
            var previewTime = previewRange * target.AnimationPlayTime;
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
            virtualIMUChart.Progress = progress;
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
            availableIMUs.Clear();
            if (target == null) return;
            // apply the same animation to all actors.
            var rawAnimation = target.getAnimation();
            if (rawAnimation != null)
            {
                ApplyToAllActor(actor => actor.setAnimation(rawAnimation));
            }

            // place the same imus on all actors
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
                    var newImu = Instantiate(imu.prefab == null ? imu.gameObject : imu.prefab, actorParent);
                    newImu.gameObject.SetActive(true);
                    newImu.name = imu.name;
                    newImu.transform.localPosition = imu.transform.localPosition;
                    newImu.transform.localRotation = imu.transform.localRotation;
                    var imuComponent = newImu.GetComponent<VirtualIMUSensor>();
                    imuComponent.registerOnStart = false;
                    Destroy(imuComponent.inActiveVisualization);
                    imuComponent.inActiveVisualization = null;
                    imuComponent.IsActive = false;
                    copied[index] = imuComponent;
                    index++;
                });
                availableIMUs.Add(imu, copied);
            }
            if (availableIMUs.Count > 0)
            {
                var first = availableIMUs.First();
               SelectedIMU(first.Key);
            }
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
            var sensors = new List<VirtualIMUSensor>();
            var imus = target.GetComponentsInChildren<VirtualIMUSensor>();
            return imus;
        }
        
        public void SelectedIMU(VirtualIMUSensor sensor)
        {
            if (!availableIMUs.ContainsKey(sensor)) return;
            selectedSensor = sensor;
            var actorSensors = availableIMUs[sensor];
            for (var i = 0; i < actors.Length; i++)
            {
                var actorSensor = actorSensors[i];
                var marker = actors[i].GetComponent<IMUMarkerRendering>();
                marker.imuSensor = actorSensor;
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
            if (selectedSensor == null) return;
            if (target == null || !target.hasAnimation) return;
            
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
                selectedSensor.UpdateWorking(time, deltaTime);
                time += deltaTime;
            }
            selectedSensor.StopRecording();
            synthesisIMUData = selectedSensor.Data;
            target.PlayAnimationToTime();
            
            // update chart
            virtualIMUChart.updateIMUData(synthesisIMUData);
        }
    }
    
    [UnityEditor.CustomEditor(typeof(VsensPlatform))]
    public class VsensPlatformEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var platform = (VsensPlatform) target;
            if (GUILayout.Button("Load Target"))
            {
                platform.LoadTarget();
            }
            if (platform.availableIMUs.Count > 0)
            {
                foreach (var entry in platform.availableIMUs)
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
}
