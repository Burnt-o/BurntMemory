namespace BurntMemory
{
    [Serializable]
    public class RPMException : Exception
    {
        public RPMException() : base()
        {
        }

        public RPMException(string message) : base(message)
        {
        }

        public RPMException(string message, Exception inner) : base(message, inner)
        {
        }
    }
}