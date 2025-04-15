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
                    var headers = lines[0].Split(',');
                    var headerMap = new Dictionary<string, int>();
                    for (var i = 0; i < headers.Length; i++)
                    {
                        headerMap[headers[i]] = i;
                    }
                    var ex = headerMap["ex"];
                    var ey = headerMap["ey"];
                    var ez = headerMap["ez"];
                    var ax = headerMap["ax"];
                    var ay = headerMap["ay"];
                    var az = headerMap["az"];
                    var lx = headerMap["lx"];
                    var ly = headerMap["ly"];
                    var lz = headerMap["lz"];
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
                                Orientation = new Vector3(float.Parse(line[ex]), float.Parse(line[ey]), float.Parse(line[ez])),
                                Acceleration = new Vector3(float.Parse(line[ax]), float.Parse(line[ay]), float.Parse(line[az])),
                                LocalAcceleration = new Vector3(float.Parse(line[lx]), float.Parse(line[ly]), float.Parse(line[lz])),
                                Location = Vector3.zero,
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
