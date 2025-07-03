namespace VsensAgent
{
    public interface IStateHolder
    {
        string getCurrentState();
        string[] getAvailableStates();
        void setCurrentState(string state);
    }
}