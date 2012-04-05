namespace Kudu.Core.Deployment
{
    internal class CascadeLogger : ILogger
    {
        private readonly ILogger _primary;
        private readonly ILogger _secondary;

        public CascadeLogger(ILogger primary, ILogger secondary)
        {
            _primary = primary;
            _secondary = secondary;
        }

        public ILogger Log(string value, LogEntryType type)
        {
            _secondary.Log(value, type);
            return _primary.Log(value, type);
        }
    }
}
