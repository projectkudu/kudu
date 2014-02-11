using System;
using System.Threading;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Microsoft.AspNet.SignalR;
using System.Collections.Concurrent;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Kudu.Services.Editor
{
    public class FileSystemHub : Hub
    {
        public const int MaxFileSystemWatchers = 5;

        protected static readonly ConcurrentDictionary<string, SimpleFileSystemWatcher> _fileWatchers =
            new ConcurrentDictionary<string, SimpleFileSystemWatcher>();

        private readonly IEnvironment _environment;
        private readonly ITracer _tracer;

        public FileSystemHub(IEnvironment environment, ITracer tracer)
        {
            _environment = environment;
            _tracer = tracer;
        }

        public void Register(string path)
        {
            path = path ?? _environment.RootPath;
            using (
                _tracer.Step(
                    String.Format("Registering connectionId {0} for FileSystemWatcher on path: {1}",
                        Context.ConnectionId, path)))
            {
                SimpleFileSystemWatcher watcher;
                if (_fileWatchers.TryGetValue(Context.ConnectionId, out watcher))
                {
                    watcher.Path = path;
                }
                else
                {
                    _fileWatchers.TryAdd(Context.ConnectionId, GetFileSystemWatcher(path));
                }
            }
        }

        public override async Task OnDisconnected()
        {
            using (
                _tracer.Step(String.Format("Client with connectionId {0} disconnected.",
                    Context.ConnectionId)))
            {
                RemoveFileSystemWatcher(Context.ConnectionId, _tracer);
                await base.OnDisconnected();
            }
        }

        private async void NotifyGroup(string path)
        {
            await Clients.Caller.fileExplorerChanged();
        }

        private SimpleFileSystemWatcher GetFileSystemWatcher(string path)
        {
            using (_tracer.Step(String.Format("Creating FileSystemWatcher on path {0}", path)))
            {
                var simpleFileSystemWatcher = new SimpleFileSystemWatcher(path)
                {
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };
                simpleFileSystemWatcher.GeneralDirectoryChanged += NotifyGroup;
                EnsureFileSystemWatchersMax();
                return simpleFileSystemWatcher;
            }
        }

        private static void RemoveFileSystemWatcher(string key, ITracer tracer)
        {
            using (tracer.Step(String.Format("Disposing FileSystemWatcher for connectionId {0}", key)))
            {
                SimpleFileSystemWatcher temp;
                if (_fileWatchers.TryRemove(key, out temp))
                {
                    temp.Dispose();
                }
            }
        }

        private void EnsureFileSystemWatchersMax()
        {
            while (_fileWatchers.Count >= MaxFileSystemWatchers)
            {
                var toRemove = _fileWatchers.OrderBy(p => p.Value.LastFired).LastOrDefault();
                if (String.IsNullOrEmpty(toRemove.Key))
                {
                    break;
                }
                RemoveFileSystemWatcher(toRemove.Key, _tracer);
            }
        }

        protected class SimpleFileSystemWatcher : FileSystemWatcher
        {
            public delegate void SimpleFileSystemEventHandler(string path);

            public event SimpleFileSystemEventHandler GeneralDirectoryChanged;
            public DateTime LastFired { get; private set; }

            public SimpleFileSystemWatcher(string path)
                : base(path)
            {
                Changed += HandleChange;
                Created += HandleChange;
                Deleted += HandleChange;
                Renamed += HandleRename;
                LastFired = DateTime.MinValue;
            }

            private void HandleGeneralChange(string fullPath)
            {
                GeneralDirectoryChanged.Invoke(System.IO.Path.GetDirectoryName(fullPath));
                LastFired = DateTime.UtcNow;
            }

            private void HandleChange(object sender, FileSystemEventArgs args)
            {
                HandleGeneralChange(args.FullPath);
            }

            private void HandleRename(object sender, RenamedEventArgs args)
            {
                HandleGeneralChange(args.FullPath);
            }

            protected override void Dispose(bool disposing)
            {
                GeneralDirectoryChanged = null;
                base.Dispose(disposing);
            }
        }
    }
}