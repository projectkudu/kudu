namespace Kudu.Core.Deployment
{
    public class CascadeLogger : ILogger
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
            return new CascadeLogger(_primary.Log(value, type), _secondary.Log(value, type));
        }
    }
}
