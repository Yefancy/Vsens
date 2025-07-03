using UnityEngine;

namespace VsensAgent
{
    public abstract class StateObject : MonoBehaviour, IStateHolder
    {
        public abstract string getCurrentState();
        public abstract string[] getAvailableStates();
        public abstract void setCurrentState(string state);
    }
}