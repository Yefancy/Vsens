using UnityEngine;

namespace Sensor
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Light))]
    public class LightRegistration : MonoBehaviour
    {
        private Light cachedLight;

        private void Awake()
        {
            cachedLight = GetComponent<Light>();
        }

        private void OnEnable()
        {
            EnsureLight();
            LightManager.RegisterLight(cachedLight);
        }

        private void OnDisable()
        {
            LightManager.UnregisterLight(cachedLight);
        }

        private void OnDestroy()
        {
            LightManager.UnregisterLight(cachedLight);
        }

        private void EnsureLight()
        {
            if (cachedLight == null)
            {
                cachedLight = GetComponent<Light>();
            }
        }
    }
}