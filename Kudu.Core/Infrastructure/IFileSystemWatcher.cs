namespace Kudu.Core.Infrastructure
{
    using System.IO;

    public interface IFileSystemWatcher
    {
        void Start();
        void Stop();

        string Path { get; }

        event FileSystemEventHandler Deleted;
        event FileSystemEventHandler Changed;
        event RenamedEventHandler Renamed;
        event ErrorEventHandler Error;
    }
}
