using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Http;

namespace Kudu.Services.Performance
{
    internal static class ProfileManager
    {
        private const int ProcessExitTimeoutInSeconds = 20;

        private static ConcurrentDictionary<int, ProfileInfo> _profilingList = new ConcurrentDictionary<int, ProfileInfo>();
        private static object _lockObject = new object();

        // The profiling session timeout, this is temp fix before VS2015 Update 1.
        private static readonly TimeSpan _profilingTimeout = TimeSpan.FromMinutes(15);
        private static Timer _profilingIdleTimer;

        private static string _processName = System.Environment.ExpandEnvironmentVariables("%SystemDrive%\\msvsmon\\profiler\\VSStandardCollector.Dev14.exe");

        internal static async Task<HttpResponseInfo> StartProfileAsync(int processId, ITracer tracer = null)
        {
            tracer = tracer ?? NullTracer.Instance;
            using (tracer.Step("ProcessController.StartProfileAsync"))
            {
                // Check if the profiling is already running for the given process. If it does, then just return with 200.
                if (_profilingList.ContainsKey(processId))
                {
                    return new HttpResponseInfo(HttpStatusCode.OK, string.Empty);
                }

                // Profiling service supports up to 12 concurrent profiling sessions.
                if (_profilingList.Count >= 12)
                {
                    tracer.TraceError("Too many profiling sessions are currently running.");
                    return new HttpResponseInfo(HttpStatusCode.ServiceUnavailable, "Too many profiling sessions are currently running. Please try again later.");
                }

                int profilingSessionId = GetNextProfilingSessionId();
                
                string arguments = System.Environment.ExpandEnvironmentVariables(string.Format("start {0} /attach:{1} /loadAgent:4EA90761-2248-496C-B854-3C0399A591A4;DiagnosticsHub.CpuAgent.dll  /scratchLocation:%LOCAL_EXPANDED%\\Temp", profilingSessionId, processId));

                var profileProcessResponse = await ExecuteProfilingCommandAsync(arguments, tracer);

                if (profileProcessResponse.StatusCode != HttpStatusCode.OK)
                {
                    return profileProcessResponse;
                }

                // This may fail if we got 2 requests at the same time to start a profiling session
                // in that case, only 1 will be added and the other one will be stopped.
                if (!_profilingList.TryAdd(processId, new ProfileInfo(profilingSessionId)))
                {
                    tracer.TraceWarning("A profiling session was already running for process {0}, stopping profiling session {1}", processId, profilingSessionId);
                    await StopProfileInternalAsync(processId, profilingSessionId, true, tracer);
                    return new HttpResponseInfo(HttpStatusCode.OK, string.Empty);
                }

                tracer.Step("started session id: {0} for pid: {1}", profilingSessionId, processId);

                EnsureIdleTimer();

                return new HttpResponseInfo(HttpStatusCode.OK, string.Empty);
            }
        }

        internal static async Task<HttpResponseInfo> StopProfileAsync(int processId, ITracer tracer = null)
        {
            int profilingSessionId;

            tracer = tracer ?? NullTracer.Instance;
            using (tracer.Step("ProcessController.StopProfileAsync"))
            {
                // check if the profiling is running for the given process. If it doesn't return 404.
                if (!_profilingList.ContainsKey(processId))
                {
                    return new HttpResponseInfo(HttpStatusCode.NotFound, string.Format("Profiling for process '{0}' is not running.", processId));
                }
                else
                {
                    profilingSessionId = _profilingList[processId].SessionId;
                }

                var profileProcessResponse = await StopProfileInternalAsync(processId, profilingSessionId, false, tracer);

                return profileProcessResponse;
            }
        }

        internal static string GetProfilePath(int processId)
        {
            string profileFileName = string.Format("profile_{0}_{1}_{2}.diagsession", InstanceIdUtility.GetShortInstanceId(), System.Diagnostics.Process.GetProcessById(processId).ProcessName, processId);
            return System.Environment.ExpandEnvironmentVariables("%LOCAL_EXPANDED%\\Temp\\" + profileFileName);
        }

        internal static bool IsProfileRunnig(int processId)
        {
            return _profilingList.ContainsKey(processId);
        }

        private static async Task<HttpResponseInfo> StopProfileInternalAsync(int processId, int profilingSessionId, bool ignoreProfileFile, ITracer tracer = null)
        {
            tracer = tracer ?? NullTracer.Instance;

            using (tracer.Step("ProcessController.StopProfileAsync"))
            {
                string profileFileFullPath = GetProfilePath(processId);
                string profileFileName = Path.GetFileName(profileFileFullPath);
                string arguments = string.Format("stop {0} /output:{1}", profilingSessionId, profileFileFullPath);

                var profileProcessResponse = await ExecuteProfilingCommandAsync(arguments, tracer);

                ProfileInfo removedId;
                if (profileProcessResponse.StatusCode != HttpStatusCode.OK)
                {
                    _profilingList.TryRemove(processId, out removedId);
                    return profileProcessResponse;
                }

                FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(profileFileFullPath));
                tracer.Step("profile was saved to {0} successfully.", profileFileFullPath);

                _profilingList.TryRemove(processId, out removedId);

                if (ignoreProfileFile)
                {
                    try
                    {
                        FileSystemHelpers.DeleteFile(profileFileFullPath);
                    }
                    catch
                    {
                    }
                }

                DisposeTimerIfNecessary();

                return new HttpResponseInfo(HttpStatusCode.OK, string.Empty);
            }
        }

        private static async Task<HttpResponseInfo> ExecuteProfilingCommandAsync(string arguments, ITracer tracer)
        {
            MemoryStream outputStream = null;
            MemoryStream errorStream = null;
            try
            {
                tracer.Step("ProcessName:" + _processName + "   arguments:" + arguments);
                var exe = new Executable(_processName, Path.GetDirectoryName(_processName), TimeSpan.FromSeconds(ProcessExitTimeoutInSeconds));

                outputStream = new MemoryStream();
                errorStream = new MemoryStream();

                tracer.Step("Path:" + exe.Path + " working directory:" + exe.WorkingDirectory);

                int exitCode = await exe.ExecuteAsync(tracer, arguments, outputStream, errorStream);

                string output = GetString(outputStream);
                string error = GetString(errorStream);

                tracer.Step(output);

                if (exitCode != 0)
                {
                    tracer.TraceError(string.Format(CultureInfo.InvariantCulture, "Starting process {0} failed with the following error code '{1}'.", _processName, exitCode));
                    return new HttpResponseInfo(HttpStatusCode.InternalServerError, "Profiling process failed with the following error code: " + exitCode);
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    tracer.TraceError(error);
                    return new HttpResponseInfo(HttpStatusCode.InternalServerError, "Profiling process failed with the following error: " + error);
                }

                return new HttpResponseInfo(HttpStatusCode.OK, string.Empty);
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);
                return new HttpResponseInfo(HttpStatusCode.InternalServerError, ex.Message);
            }
            finally
            {
                if (outputStream != null)
                {
                    outputStream.Dispose();
                }

                if (errorStream != null)
                {
                    errorStream.Dispose();
                }
            }
        }

        private static int GetNextProfilingSessionId()
        {
            // TODO: This is not a good way to track active profiling sessions, but as of VS2015 RTM, the profiling service does not provide any API to track the current active sessions.  
            // This is planned to be fixed in VS2015 Update 1.
            var r = new Random();

            int sessionId = r.Next(1, 255);
            List<ProfileInfo> list = _profilingList.Values.ToList<ProfileInfo>();

            while (list.Exists(item => item.SessionId == sessionId))
            {
                sessionId = r.Next(1, 255);
            }

            return sessionId;
        }

        private static string GetString(MemoryStream stream)
        {
            if (stream.Length > 0)
            {
                return Encoding.UTF8.GetString(stream.GetBuffer(), 0, (int)stream.Length);
            }

            return String.Empty;
        }

        private static void EnsureIdleTimer()
        {
            lock (_lockObject)
            {
                if (_profilingIdleTimer == null)
                {
                    _profilingIdleTimer = new Timer(_ => SafeInvoke(() => OnIdleTimer()), null, _profilingTimeout, _profilingTimeout);
                }
            }
        }

        private static void OnIdleTimer()
        {
            lock (_lockObject)
            {
                if (_profilingList.Count > 0)
                {
                    // Manually stop any profiling session which has exceeded the timeout period.
                    // TODO: VS 2015 Update 1 should have a better way to handle this.
                    foreach (var item in _profilingList)
                    {
                        if (DateTime.UtcNow - item.Value.StartTime > _profilingTimeout)
                        {
                            Task.Run(() => StopProfileInternalAsync(item.Key, item.Value.SessionId, true));
                        }
                    }

                    DisposeTimerIfNecessary();
                }
            }
        }

        private static void DisposeTimerIfNecessary()
        {
            if(_profilingList.Count == 0)
            {
                if (_profilingIdleTimer != null)
                {
                    _profilingIdleTimer.Dispose();
                    _profilingIdleTimer = null;
                }
            }
        }

        private static void SafeInvoke(Action func)
        {
            try
            {
                func();
            }
            catch (Exception)
            {
                // no-op
            }
        }

        internal class HttpResponseInfo
        {
            public HttpResponseInfo(HttpStatusCode statusCode, string message)
            {
                this.StatusCode = statusCode;
                this.Message = message;
            }

            public HttpStatusCode StatusCode { get; set; }

            public string Message { get; set; }
        }

        private class ProfileInfo
        {
            public ProfileInfo(int sessionId)
            {
                this.SessionId = sessionId;
                this.StartTime = DateTime.UtcNow;
            }

            public int SessionId { get; set; }

            public DateTime StartTime { get; set; }
        }
    }
}
