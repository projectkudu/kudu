namespace Kudu.Client.Infrastructure
{
    public interface IEventProvider
    {
        void Start();
        void Stop();
        bool IsActive { get; }
    }
}
