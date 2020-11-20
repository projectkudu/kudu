using Kudu.Contracts.Tracing;
using Kudu.Core.Helpers;
using Kudu.Core.Tracing;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;

namespace Kudu.Core.Infrastructure
{
    internal class ShutdownDelaySemaphore
    {
        private const int InitialAndMaxCount = 8;

        private static ShutdownDelaySemaphore _instance;

        private readonly SemaphoreSlim _shutdownSemaphore = new SemaphoreSlim(InitialAndMaxCount, InitialAndMaxCount);
        private readonly object _lock = new object();

        private ShutdownDelaySemaphore() { }

        public static ShutdownDelaySemaphore GetInstance()
        {
            if (_instance == null)
            {
                _instance = new ShutdownDelaySemaphore();
            }

            return _instance;
        }

        public bool Acquire(ITracer tracer)
        {
            lock (_lock)
            {
                try
                {
                    // No timeout makes this call instant. No waiting for acquistion
                    bool acquired = _shutdownSemaphore.Wait(millisecondsTimeout: 0);

                    if (!acquired)
                    {
                        tracer.Trace("Could not acquire shutdown semaphore", new Dictionary<string, string>
                        {
                            { "SemaphoreCount", _shutdownSemaphore.CurrentCount.ToString() }
                        });

                        return false;
                    }

                    //tracer.Trace("Acquired shutdown semaphore", new Dictionary<string, string>
                    //{
                    //    { "SemaphoreCount", _shutdownSemaphore.CurrentCount.ToString() }
                    //});

                    OperationManager.SafeExecute(() => {
                        if (Environment.IsAzureEnvironment() && OSDetector.IsOnWindows())
                        {
                            string siteRoot = PathUtilityFactory.Instance.ResolveLocalSitePath();

                            // Create Sentinel file for DWAS to check
                            // DWAS will check for presence of this file incase a an app setting based recycle needs to be performed in middle of deployment
                            // If this is present, DWAS will postpone the recycle so that deployment goes through first
                            if (!string.IsNullOrEmpty(siteRoot))
                            {
                                FileSystemHelpers.CreateDirectory(Path.Combine(siteRoot, @"ShutdownSentinel"));
                                string sentinelPath = Path.Combine(siteRoot, @"ShutdownSentinel\Sentinel.txt");

                                if (!FileSystemHelpers.FileExists(sentinelPath))
                                {
                                    //tracer.Trace("Creating shutdown sentinel file", new Dictionary<string, string>
                                    //{
                                    //    { "SemaphoreCount", _shutdownSemaphore.CurrentCount.ToString() }
                                    //});
                                    var file = FileSystemHelpers.CreateFile(sentinelPath);
                                    file.Close();
                                }

                                tracer.Trace("Updating shutdown sentinel last write time", new Dictionary<string, string>
                                {
                                    { "SemaphoreCount", _shutdownSemaphore.CurrentCount.ToString() }
                                });
                                // DWAS checks if write time of this file is in the future then only postpones the recycle
                                FileInfoBase sentinelFileInfo = FileSystemHelpers.FileInfoFromFileName(sentinelPath);
                                sentinelFileInfo.LastWriteTimeUtc = DateTime.UtcNow.AddMinutes(20);
                            }
                        }
                    });
                }
                catch (Exception ex)
                {
                    tracer.TraceError(ex);
                    return false;
                }

                return true;
            }
        }

        public void Release(ITracer tracer)
        {
            lock (_lock)
            {
                _shutdownSemaphore.Release();
                //tracer.Trace("Released shut down semaphore", new Dictionary<string, string>
                //{
                //    { "SemaphoreCount", _shutdownSemaphore.CurrentCount.ToString() }
                //});

                if (_shutdownSemaphore.CurrentCount == InitialAndMaxCount)
                {
                    OperationManager.SafeExecute(() => {
                        string siteRoot = PathUtilityFactory.Instance.ResolveLocalSitePath();

                        // Delete the Sentinel file to signal DWAS that deployment is complete
                        if (!string.IsNullOrEmpty(siteRoot))
                        {
                            string sentinelPath = Path.Combine(siteRoot, @"ShutdownSentinel\Sentinel.txt");

                            if (FileSystemHelpers.FileExists(sentinelPath))
                            {
                                tracer.Trace("Deleting shutdown sentinel file", new Dictionary<string, string>
                                {
                                    { "SemaphoreCount", _shutdownSemaphore.CurrentCount.ToString() }
                                });
                                FileSystemHelpers.DeleteFile(sentinelPath);
                            }
                        }
                    });
                }
            }
        }
    }
}
