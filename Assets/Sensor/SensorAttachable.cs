using System.Diagnostics.CodeAnalysis;
using Oculus.Interaction.Input;
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
        
        public virtual bool CanAttachTo(VirtualSensor sensor, IHand usedHand = null)
        {
            if (usedHand == null) return true;
            if (handCondition == HandCondition.Left && usedHand == DevicesRef.Instance.LeftHand)
            {
                return true;
            }

            if (handCondition == HandCondition.Right && usedHand == DevicesRef.Instance.RightHand)
            {
                return true;
            }

            if (handCondition == HandCondition.Both)
            {
                return true;
            }
            
            return false;
        }
        
        public virtual void OnAttachTo(VirtualSensor sensor)
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
        }
        
        public virtual void OnAttachHover(VirtualSensor sensor)
        {
            HoverEffect?.SetActive(true);
        }
        
        public virtual void OnAttachHoverExit(VirtualSensor sensor)
        {
            HoverEffect?.SetActive(false);
        }
    }
}
