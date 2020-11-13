namespace slowapp
{
    public enum States
    {
        Starting,
        Running,
        AfterSigterm
    }

    public static class State
    {
        private static States _currentState = States.Starting;
        
        public static States GetState()
        {
            return _currentState;
        }
        
        public static void SetState(this States state)
        {
            _currentState = state;
        }
    }
}