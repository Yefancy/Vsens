using System.Collections.Generic;
using UnityEngine;

namespace Sensor
{
    public static class LightManager
    {
        private static readonly List<Light> lights = new();
        
        public static void RegisterLight(Light light)
        {
            if (light == null)
            {
                return;
            }

            if (!lights.Contains(light))
            {
                lights.Add(light);
            }
        }
        
        public static void UnregisterLight(Light light)
        {
            if (light == null)
            {
                return;
            }

            lights.Remove(light);
        }
        
        public static IReadOnlyList<Light> GetLights()
        {
            lights.RemoveAll(light => light == null);
            return lights;
        }
    }
}
