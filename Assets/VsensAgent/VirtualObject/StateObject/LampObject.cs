using UnityEditor;
using UnityEngine;

namespace VsensAgent
{
    public class LampObject : StateObject
    {
        private static readonly string[] STATES = { "open", "closed" };

        [SerializeField] private Light _light;
        [SerializeField] private Material _onMaterial;
        [SerializeField] private Material _offMaterial;
        [SerializeField] private Renderer _renderer;
        
        public bool isOpen => _light.enabled;
        
        public void ToggleLamp(bool open)
        {
            _light.enabled = open;
            _renderer.material = open ? _onMaterial : _offMaterial;
        }
        
        public override string getCurrentState()
        {
            return isOpen ? "open" : "closed";
        }

        public override string[] getAvailableStates()
        {
            return STATES;
        }

        public override void setCurrentState(string state)
        {
            if (state == "open")
            {
                ToggleLamp(true);
            }
            else if (state == "closed")
            {
                ToggleLamp(false);
            }
            else
            {
                Debug.LogWarning($"Invalid state: {state}. Available states are: {string.Join(", ", STATES)}");
            }
        }
    }
    
#if UNITY_EDITOR
    [CustomEditor(typeof(LampObject))]
    public class LampObjectEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            LampObject describer = (LampObject)target;

            GUILayout.Space(10);
            if (GUILayout.Button("Toggle Lamp"))
            {
                describer.ToggleLamp(!describer.isOpen);
            }
        }
    }
#endif
}
