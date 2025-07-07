using SojaExiles;
using UnityEngine;

namespace VsensAgent
{
    public class WindowObject : StateObject
    {
        private static readonly string[] STATES = { "open", "closed" };
        
        [SerializeField] private opencloseWindowApt _windowController;

        private void Awake()
        {
            _windowController = GetOrCreateWindowController();
        }
        
        private opencloseWindowApt GetOrCreateWindowController()
        {
            if (_windowController != null) return _windowController;
            _windowController = GetComponent<opencloseWindowApt>();
            if (_windowController ==null)
            {
                _windowController = gameObject.AddComponent<opencloseWindowApt>();
            }
            return _windowController;
        } 
        
        public override string getCurrentState()
        {
            var windowController = GetOrCreateWindowController();
            return windowController.open ? "open" : "closed";
        }

        public override string[] getAvailableStates()
        {
            return STATES;
        }

        public override void setCurrentState(string state)
        {
            var windowController = GetOrCreateWindowController();
            if (state == "open")
            {
                windowController.ToggleWindow(true);
            }
            else if (state == "closed")
            {
                windowController.ToggleWindow(false);
            }
            else
            {
                Debug.LogWarning($"Invalid state: {state}. Available states are: {string.Join(", ", STATES)}");
            }
        }
    }
}