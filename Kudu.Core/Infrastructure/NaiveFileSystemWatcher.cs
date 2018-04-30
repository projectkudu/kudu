namespace Kudu.Core.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    // This is a stopgap solution for watching logs on Linux. It is designed for use in
    // LogStreamManager and is NOT a general purpose implementation.
    //
    // On Linux, when a site process holds a file on the CIFS volume open and continues to write
    // to it, the data is written to the volume, but the timestamp is not updated until the
    // process closes the file, and other consumers of the volume (the Kudu container) do not
    // see any changes unless they actually open the file.
    //
    // This implementation takes advantage of the fact that LogStreamManager is responsible for
    // opening the files and efficiently determining if they have actually had new data written
    // to them, and so it simply events out all eligible files as "changed" every interval.
    //
    // Overall, there is no solution that doesn't involve some kind of polling with the current
    // CIFS limitations.

    public class NaiveFileSystemWatcher : IFileSystemWatcher, IDisposable
    {
        private static readonly TimeSpan INTERVAL = TimeSpan.FromSeconds(1.5);

        private readonly CancellationTokenSource cts;
        private readonly string[] logFileExtensions;
        private Task pollingTask;

        public NaiveFileSystemWatcher(string path, IEnumerable<string> logFileExtensions)
        {
            this.Path = path;
            this.cts = new CancellationTokenSource();
            this.logFileExtensions = logFileExtensions.ToArray();
        }

        public string Path { get; }

        public event FileSystemEventHandler Changed;

        public event FileSystemEventHandler Deleted { add { } remove { } }
        public event RenamedEventHandler Renamed { add { } remove { } }
        public event ErrorEventHandler Error { add { } remove { } }

        public void Start()
        {
            this.pollingTask = Task.Run(Poll, cts.Token);
        }

        public void Stop()
        {
            cts.Cancel();
            this.pollingTask.Wait();
        }

        private async Task Poll()
        {
            while (!cts.IsCancellationRequested)
            {
                var handler = Changed;
                if (handler != null)
                {
                    var filepaths = Directory.EnumerateFiles(Path, "*.*", SearchOption.AllDirectories)
                        .Where(p => logFileExtensions.Any(ext => ext.Equals(System.IO.Path.GetExtension(p), StringComparison.OrdinalIgnoreCase)));

                    foreach (var filepath in filepaths)
                    {
                        var dir = System.IO.Path.GetDirectoryName(filepath);
                        var name = System.IO.Path.GetFileName(filepath);
                        handler(this, new FileSystemEventArgs(WatcherChangeTypes.Changed, dir, name));
                    }
                }

                await Task.Delay(INTERVAL, cts.Token).ContinueWith(t => { });
            }
        }

        private bool disposedValue = false;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (this.cts != null)
                    {
                        this.cts.Dispose();
                    }

                    if (this.pollingTask != null)
                    {
                        this.pollingTask.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
