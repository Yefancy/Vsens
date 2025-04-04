using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Serialization;

namespace Scenes.Replay.sensor
{
    public class SimulationConfig
    {
        public float startTime;
        public float endTime;
        public float speed; // range 0.5 - 1.5, dur 0.1
        public float deltaTime;
        public int samplingRate; // 50hz
        public float smoothWindowSize;
        public float amplitude;
        public Tuple<int, float>[] bodyShapes;
        
        public static string GetCsvHeader()
        {
            return "startTime,endTime,speed,deltaTime,samplingRate,smoothWindowSize,amplitude," +
                   "bodyShapes_0_pos,bodyShapes_0_neg," +
                   "bodyShapes_1_pos,bodyShapes_1_neg," +
                   "bodyShapes_2_pos,bodyShapes_2_neg," +
                   "bodyShapes_3_pos,bodyShapes_3_neg," +
                   "bodyShapes_4_pos,bodyShapes_4_neg," +
                   "bodyShapes_5_pos,bodyShapes_5_neg," +
                   "bodyShapes_6_pos,bodyShapes_6_neg," +
                   "bodyShapes_7_pos,bodyShapes_7_neg," +
                   "bodyShapes_8_pos,bodyShapes_8_neg," +
                   "bodyShapes_9_pos,bodyShapes_9_neg";
        }
        
        public string ToCsvLine()
        {
            Dictionary<int, float> shapes = new Dictionary<int, float>();
            foreach (var shape in bodyShapes)
            {
                shapes[shape.Item1] = shape.Item2;
            }
            return $"{startTime},{endTime},{speed},{deltaTime},{samplingRate},{smoothWindowSize},{amplitude}," +
                   $"{shapes.GetValueOrDefault(0, 0)},{shapes.GetValueOrDefault(1, 0)}," +
                   $"{shapes.GetValueOrDefault(2, 0)},{shapes.GetValueOrDefault(3, 0)}," +
                   $"{shapes.GetValueOrDefault(4, 0)},{shapes.GetValueOrDefault(5, 0)}," +
                   $"{shapes.GetValueOrDefault(6, 0)},{shapes.GetValueOrDefault(7, 0)}," +
                   $"{shapes.GetValueOrDefault(8, 0)},{shapes.GetValueOrDefault(9, 0)}," +
                   $"{shapes.GetValueOrDefault(10, 0)},{shapes.GetValueOrDefault(11, 0)}," +
                   $"{shapes.GetValueOrDefault(12, 0)},{shapes.GetValueOrDefault(13, 0)}," +
                   $"{shapes.GetValueOrDefault(14, 0)},{shapes.GetValueOrDefault(15, 0)}," +
                   $"{shapes.GetValueOrDefault(16, 0)},{shapes.GetValueOrDefault(17, 0)}," +
                   $"{shapes.GetValueOrDefault(18, 0)},{shapes.GetValueOrDefault(19, 0)}";

        }
    }
}