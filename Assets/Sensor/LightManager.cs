using System.Collections.Generic;
using UnityEngine;

namespace Sensor
{
    public static class LightManager
    {
        private static List<Light> lights = new();
        
        public static void RegisterLight(Light light)
        {
            lights.Add(light);
        }
        
        public static void UnregisterLight(Light light)
        {
            lights.Remove(light);
        }
        
        public static List<Light> GetLights()
        {
            return lights;
        }
    }
}
