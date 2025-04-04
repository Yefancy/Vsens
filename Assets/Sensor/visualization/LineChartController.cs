using System;
using System.Collections.Generic;
using UnityEngine;
using XCharts.Runtime;

namespace Sensor.visualization
{
    public class LineChartController : GraphController<float[]>
    {
        [SerializeField] 
        private LineChart chart;
        

        protected override void Start()
        {
            base.Start();
            if (chart == null) return;
            chart.ClearData();
        }
        
        public override void UploadData(float time, float[] data)
        {
            base.UploadData(time, data);
            UpdateLineChart(chart, cacheData);
        }
        
        
        public void UpdateLineChart(LineChart lineChartController, List<Tuple<float, float[]>> data) {
            if (lineChartController == null) return;
            lineChartController.ClearData();
            data.ForEach(item => {
                lineChartController.AddXAxisData(item.Item1.ToString());
                for (var i = 0; i < item.Item2.Length; i++)
                {
                    lineChartController.AddData(i, item.Item2[i]);
                }
            });
        }
    }
}
