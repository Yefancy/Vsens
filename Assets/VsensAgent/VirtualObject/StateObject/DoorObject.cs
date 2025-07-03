using SojaExiles;
using UnityEngine;

namespace VsensAgent
{
    public class DoorObject : StateObject
    {
        private static readonly string[] STATES = { "open", "closed" };
        
        [SerializeField] private opencloseDoor _doorController;

        private void Awake()
        {
            _doorController = GetOrCreateDoorController();
        }
        
        private opencloseDoor GetOrCreateDoorController()
        {
            if (_doorController != null) return _doorController;
            _doorController = GetComponent<opencloseDoor>();
            if (_doorController ==null)
            {
                _doorController = gameObject.AddComponent<opencloseDoor>();
            }
            return _doorController;
        } 
        
        public override string getCurrentState()
        {
            var doorController = GetOrCreateDoorController();
            return doorController.open ? "open" : "closed";
        }

        public override string[] getAvailableStates()
        {
            return STATES;
        }

        public override void setCurrentState(string state)
        {
            var doorController = GetOrCreateDoorController();
            if (state == "open")
            {
                doorController.ToggleDoor(true);
            }
            else if (state == "closed")
            {
                doorController.ToggleDoor(false);
            }
            else
            {
                Debug.LogWarning($"Invalid state: {state}. Available states are: {string.Join(", ", STATES)}");
            }
        }
    }
}