using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;

namespace Sensor
{
    public class SensorAttachable : MonoBehaviour
    {
        public enum HandCondition
        {
            Left,
            Right,
            Both,
            None
        }
        
        [Tooltip("The parent of the placed sensor.")]
        public GameObject Parent;
        [AllowNull, Tooltip("If set, it will be active while its hovered.")]
        public GameObject HoverEffect;
        [Tooltip("The hand condition that the sensor can attach by.")]
        public HandCondition handCondition = HandCondition.Both;
        [Tooltip("The hand condition that the sensor can be controlled by.")]
        public HandCondition controlledHand = HandCondition.Both;
        public HashSet<VirtualSensor> sensors = new();

        public virtual bool CanAttachTo(VirtualSensor sensor)
        {
            return true;
        }
        
        public void OnAttachTo(VirtualSensor sensor)
        {
            if (OnAttachInternal(sensor))
            {
                sensor.OnAttachTo(this);
                sensors.Add(sensor);
            }
        }

        protected virtual bool OnAttachInternal(VirtualSensor sensor)
        {
            if (Parent)
            {
                sensor.transform.SetParent(Parent.transform);
            }
            else
            {
                sensor.transform.SetParent(transform);
            }
            sensor.controlledHand = controlledHand;
            return true;
        }
        
        public virtual void OnAttachHover(VirtualSensor sensor)
        {
            if (HoverEffect == null) return;
            HoverEffect?.SetActive(true);
        }
        
        public virtual void OnAttachHoverExit(VirtualSensor sensor)
        {
            if (HoverEffect == null) return;
            HoverEffect?.SetActive(false);
        }
        
        public virtual void OnSensorDetach(VirtualSensor sensor)
        {
            sensors.Remove(sensor);
        }
    }
}
