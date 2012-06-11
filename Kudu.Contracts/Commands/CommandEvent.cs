namespace Kudu.Core.Commands
{
    public class CommandEvent
    {
        public CommandEvent()
        {
        }

        public CommandEvent(CommandEventType eventType)
            : this(eventType, null)
        {
        }

        public CommandEvent(CommandEventType eventType, string data)
        {
            EventType = eventType;
            Data = data;
        }

        public CommandEventType EventType { get; set; }
        public string Data { get; set; }
        public int ExitCode { get; set; }
    }
}
