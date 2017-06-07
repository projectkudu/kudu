namespace Kudu.Core.Infrastructure
{
    using System;
    using System.IO;

    public class FileSystemWatcherWrapper : IFileSystemWatcher
    {
        private readonly FileSystemWatcher inner;
        private readonly object eventLock = new Object();

        public FileSystemWatcherWrapper(string path, bool includeSubdirectories)
        {
            this.inner = new FileSystemWatcher(path);
            this.inner.IncludeSubdirectories = includeSubdirectories;
        }

        public void Start()
        {
            this.inner.EnableRaisingEvents = true;
        }

        public void Stop()
        {
            this.inner.EnableRaisingEvents = false;
        }

        public string Path
        {
            get { return this.inner.Path; }
        }

        public event FileSystemEventHandler Deleted
        {
            add
            {
                this.inner.Deleted += value;
            }
            remove
            {
                this.inner.Deleted -= value;
            }
        }
        public event FileSystemEventHandler Changed
        {
            add
            {
                this.inner.Changed += value;
            }
            remove
            {
                this.inner.Changed -= value;
            }
        }

        public event RenamedEventHandler Renamed
        {
            add
            {
                this.inner.Renamed += value;
            }
            remove
            {
                this.inner.Renamed -= value;
            }
        }

        public event ErrorEventHandler Error
        {
            add
            {
                this.inner.Error += value;
            }
            remove
            {
                this.inner.Error -= value;
            }
        }

    }
}
