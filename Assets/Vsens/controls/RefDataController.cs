using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Sensor;
using UnityEngine;
using UnityEngine.UI;

namespace Vsens.controls
{
    [RequireComponent(typeof(ToggleGroup))]
    public class RefDataController : MonoBehaviour
    {
        public VsensPlatform VsensPlatform;
        public RefDataToggle templateToggle;
        private ToggleGroup _toggleGroup;

        private void Awake()
        {
            _toggleGroup = GetComponent<ToggleGroup>();
        }

        private void Start()
        {
            StartCoroutine(LoadRefData());
        }
        
        private IEnumerator LoadRefData()
        {
            // find animation files under the StreamingAssets folder
            var refPath = Path.Combine(Application.streamingAssetsPath, "RefIMUData");
            if (!Directory.Exists(refPath))
            {
                Directory.CreateDirectory(refPath);
            }
            var files = Directory.GetFiles(refPath, "*.csv");
            foreach (var file in files)
            {
                var refDataName = Path.GetFileNameWithoutExtension(file);
                // loading animation async
                List<SensorData> refData = null;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    var data = new List<SensorData>();
                    var lines = File.ReadAllLines(file);
                    for (var i = 0; i < lines.Length; i++)
                    {
                        if (i == 0 || lines[i].StartsWith("#")) continue;
                        // "tag,time,ex,ey,ez,ax,ay,az,lx,ly,lz,x,y,z"
                        var line = lines[i].Split(',');
                        data.Add(new SensorData
                        {
                            sensorID = line[0],
                            time = float.Parse(line[1]),
                            data = new VirtualIMUSensor.IMUSensorData
                            {
                                Orientation = new Vector3(float.Parse(line[2]), float.Parse(line[3]), float.Parse(line[4])),
                                Acceleration = new Vector3(float.Parse(line[5]), float.Parse(line[6]), float.Parse(line[7])),
                                LocalAcceleration = new Vector3(float.Parse(line[8]), float.Parse(line[9]), float.Parse(line[10])),
                                Location = new Vector3(float.Parse(line[11]), float.Parse(line[12]), float.Parse(line[13]))
                            }
                        });
                    }
                    refData = data;
                });
                yield return new WaitUntil(() => refData != null);
                AddRefData(refDataName, refData);
            }
        }

        public void AddRefData(String refDataName, List<SensorData> refData)
        {
            var animationToggle = Instantiate(templateToggle, templateToggle.transform.parent);
            animationToggle.gameObject.SetActive(true);
            animationToggle.SetRefData(refDataName, refData);
            animationToggle.GetComponent<Toggle>().group = _toggleGroup;
            animationToggle.GetComponent<Toggle>().onValueChanged.AddListener(isOn =>
            {
                if (isOn)
                {
                    VsensPlatform.SetRefData(refData);
                }
            });
        }

        public void SetActive(bool isActive)
        {
            gameObject.SetActive(isActive);
            VsensPlatform.ApplyToAllActor((index, actor) =>
            {
                if (index < VsensPlatform.actors.Length - 1) actor.gameObject.SetActive(!isActive);
            });
        }

    }
}
