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

namespace Kudu.Services.Performance
{
    internal static class ProfileManager
    {
        private const int ProcessExitTimeoutInSeconds = 180;

        private const string UserModeCustomProviderAgentGuid = "F5091AA9-80DC-49FF-A7CF-BD1103FE149D";
        private const string DetailedTracingAgentGuid = "31003EE3-A8E1-427A-931E-97057D4D2B7D";
        private const string IisWebServerProviderGuid = "3A2A4E84-4C21-4981-AE10-3FDA0D9B0F83";
        private const string DiagnosticsHubAgentGuid = "4EA90761-2248-496C-B854-3C0399A591A4";

        private static ConcurrentDictionary<int, ProfileInfo> _profilingList = new ConcurrentDictionary<int, ProfileInfo>();
        private static object _lockObject = new object();

        // The profiling session timeout, this is temp fix before VS2015 Update 1.
        private static readonly TimeSpan _profilingTimeout = TimeSpan.FromMinutes(15);

        private static readonly TimeSpan _profilingIisTimeout = GetIisProfilingTimeout();

        private static Timer _profilingIdleTimer;

        private static string _processName;

        static ProfileManager()
        {
            if(Environment.OSVersion.Version.Major < 10)
            {
                _processName = Environment.ExpandEnvironmentVariables("%SystemDrive%\\msvsmon\\profiler\\VSStandardCollector.Dev14.exe");
            }
            else
            {
                _processName = Environment.ExpandEnvironmentVariables("%SystemDrive%\\Program Files\\Microsoft Visual Studio 15.0\\Team Tools\\DiagnosticsHub\\Collector\\VSDiagnostics.exe");
            }
        }

        internal static async Task<ProfileResultInfo> StartProfileAsync(int processId, ITracer tracer = null, bool iisProfiling = false)
        {
            tracer = tracer ?? NullTracer.Instance;
            using (tracer.Step("ProfileManager.StartProfileAsync"))
            {
                // Check if the profiling is already running for the given process. If it does, then just return with 200.
                if (_profilingList.ContainsKey(processId))
                {
                    return new ProfileResultInfo(HttpStatusCode.OK, string.Empty);
                }

                int profilingSessionId = GetNextProfilingSessionId();
                ProfileResultInfo profileProcessResponse = null;
                if (iisProfiling)
                {
                    profileProcessResponse = await StartIisSessionAsync(processId, profilingSessionId, tracer);                    
                }
                else
                {
                    string arguments;

                    // This is temp fix while we are in process of retiring Windows 2012 server.
                    if (Environment.OSVersion.Version.Major < 10)
                    {
                        arguments = System.Environment.ExpandEnvironmentVariables(string.Format("start {0} /attach:{1} /loadAgent:{2};DiagnosticsHub.CpuAgent.dll  /scratchLocation:%LOCAL_EXPANDED%\\Temp", profilingSessionId, processId, DiagnosticsHubAgentGuid));
                    }
                    else
                    {
                        arguments = System.Environment.ExpandEnvironmentVariables(string.Format("start {0} /attach:{1} /loadAgent:{2};DiagnosticsHub.CpuAgent.dll;{{\\\"collectNgenPdbs\\\":false}}  /scratchLocation:%LOCAL_EXPANDED%\\Temp", profilingSessionId, processId, DiagnosticsHubAgentGuid));
                    }

                    profileProcessResponse = await ExecuteProfilingCommandAsync(arguments, tracer);
                }
                if (profileProcessResponse.StatusCode != HttpStatusCode.OK)
                {
                    return profileProcessResponse;
                }

                // This may fail if we got 2 requests at the same time to start a profiling session
                // in that case, only 1 will be added and the other one will be stopped.
                if (!_profilingList.TryAdd(processId, new ProfileInfo(profilingSessionId, iisProfiling)))
                {
                    tracer.TraceWarning("A profiling session was already running for process {0}, stopping profiling session {1}", processId, profilingSessionId);
                    await StopProfileInternalAsync(processId, profilingSessionId, true, tracer, iisProfiling);
                    return new ProfileResultInfo(HttpStatusCode.OK, string.Empty);
                }

                tracer.Step("started session id: {0} for pid: {1}", profilingSessionId, processId);

                EnsureIdleTimer(iisProfiling);

                return new ProfileResultInfo(HttpStatusCode.OK, string.Empty);
            }
        }

        private static async Task<ProfileResultInfo> StartIisSessionAsync(int processId, int profilingSessionId, ITracer tracer = null)
        {
            tracer.Trace("IIS Profiling timeout = {0}s", _profilingIisTimeout.TotalSeconds);

            // Starting a new profiler session with the Detailed Tracing Agent
            string arguments = System.Environment.ExpandEnvironmentVariables($"start {profilingSessionId} /scratchLocation:\"%LOCAL_EXPANDED%\\Temp\" /loadAgent:{DetailedTracingAgentGuid};ServiceProfilerAgent.dll");
            var profileProcessResponse = await ExecuteProfilingCommandAsync(arguments, tracer);
            if (profileProcessResponse.StatusCode != HttpStatusCode.OK)
            {
                return profileProcessResponse;
            }

            // Attach to the process and enable the Custom ETW Provider agent
            arguments = $"update {profilingSessionId} /attach:{processId} /loadAgent:{UserModeCustomProviderAgentGuid};ServiceProfilerAgent.dll";
            profileProcessResponse = await ExecuteProfilingCommandAsync(arguments, tracer);
            if (profileProcessResponse.StatusCode != HttpStatusCode.OK)
            {
                return profileProcessResponse;
            }

            // Adding the IIS WWW Server Provider Events to the profiling session
            arguments = $"postString {profilingSessionId} \"AddProvider:{IisWebServerProviderGuid}:0xFFFFFFFE:5\" /agent:{UserModeCustomProviderAgentGuid}";
            profileProcessResponse = await ExecuteProfilingCommandAsync(arguments, tracer);

            return profileProcessResponse;
        }

        internal static TimeSpan GetIisProfilingTimeout()
        {
            TimeSpan iisProfilingTimeout = TimeSpan.FromMinutes(3);

            int timeout = 0;
            string iisProfilingTimeoutInSeconds = System.Environment.GetEnvironmentVariable("APPSETTING_IIS_PROFILING_TIMEOUT_IN_SECONDS");
            if (!string.IsNullOrEmpty(iisProfilingTimeoutInSeconds))
            {
                if (Int32.TryParse(iisProfilingTimeoutInSeconds, out timeout))
                {
                    if (timeout < _profilingTimeout.TotalSeconds)
                    {
                        iisProfilingTimeout = TimeSpan.FromSeconds(timeout);
                    }
                }
            }
            return iisProfilingTimeout;
        }

        internal static async Task<ProfileResultInfo> StopProfileAsync(int processId, ITracer tracer = null, bool iisProfiling = false)
        {
            int profilingSessionId;

            tracer = tracer ?? NullTracer.Instance;
            using (tracer.Step("ProfileManager.StopProfileAsync"))
            {
                // check if the profiling is running for the given process. If it doesn't return 404.
                if (!_profilingList.ContainsKey(processId))
                {
                    return new ProfileResultInfo(HttpStatusCode.NotFound, string.Format("Profiling for process '{0}' is not running.", processId));
                }
                else
                {
                    profilingSessionId = _profilingList[processId].SessionId;
                }

                var profileProcessResponse = await StopProfileInternalAsync(processId, profilingSessionId, false, tracer, iisProfiling);

                return profileProcessResponse;
            }
        }

        internal static string GetProfilePath(int processId, bool iisProfiling)
        {
            string profileFileName = "";
            
            if (iisProfiling)
            {
                profileFileName = string.Format("profile_{0}_{1}_{2}_{3}.diagsession", InstanceIdUtility.GetShortInstanceId(), "IISProfiling", System.Diagnostics.Process.GetProcessById(processId).ProcessName, processId);
            }
            else
            {
                profileFileName = string.Format("profile_{0}_{1}_{2}.diagsession", InstanceIdUtility.GetShortInstanceId(), System.Diagnostics.Process.GetProcessById(processId).ProcessName, processId);
            }
            return System.Environment.ExpandEnvironmentVariables("%LOCAL_EXPANDED%\\Temp\\" + profileFileName);
        }

        internal static bool IsProfileRunning(int processId)
        {
            return _profilingList.ContainsKey(processId);
        }
        internal static bool IsIisProfileRunning(int processId)
        {
            bool iisProfiling = false;
            if (_profilingList != null)
            {
                ProfileInfo profilingId;
                _profilingList.TryGetValue(processId, out profilingId);
                if (profilingId != null)
                {
                    iisProfiling = profilingId.IsIisProfiling;
                }
            }
            return iisProfiling;
        }
        internal static double IisProfileTimeoutInSeconds
        {
            get
            {
                return _profilingIisTimeout.TotalSeconds;
            }
        }

        private static async Task<ProfileResultInfo> StopProfileInternalAsync(int processId, int profilingSessionId, bool ignoreProfileFile, ITracer tracer = null, bool iisProfiling = false)
        {
            tracer = tracer ?? NullTracer.Instance;

            using (tracer.Step("ProfileManager.StopProfileInternalAsync"))
            {
                string profileFileFullPath = GetProfilePath(processId, iisProfiling);
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

                return new ProfileResultInfo(HttpStatusCode.OK, string.Empty);
            }
        }

        private static async Task<ProfileResultInfo> ExecuteProfilingCommandAsync(string arguments, ITracer tracer)
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
                    return new ProfileResultInfo(HttpStatusCode.InternalServerError, "Profiling process failed with the following error code: " + exitCode);
                }
                else if (!string.IsNullOrEmpty(error))
                {
                    tracer.TraceError(error);
                    return new ProfileResultInfo(HttpStatusCode.InternalServerError, "Profiling process failed with the following error: " + error);
                }

                return new ProfileResultInfo(HttpStatusCode.OK, string.Empty);
            }
            catch (Exception ex)
            {
                tracer.TraceError(ex);
                return new ProfileResultInfo(HttpStatusCode.InternalServerError, ex.Message);
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
            var lookup = new HashSet<int>(_profilingList.Values.Select( v=> v.SessionId));

            while (lookup.Contains(sessionId))
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

        private static void EnsureIdleTimer(bool iisProfiling)
        {
            lock (_lockObject)
            {
                if (_profilingIdleTimer == null)
                {
                    if (iisProfiling)
                    {
                        _profilingIdleTimer = new Timer(_ => SafeInvoke(() => OnIdleTimer(iisProfiling)), null, _profilingIisTimeout, _profilingIisTimeout);
                    }
                    else
                    {
                        _profilingIdleTimer = new Timer(_ => SafeInvoke(() => OnIdleTimer()), null, _profilingTimeout, _profilingTimeout);
                    }
                    
                }
            }
        }

        private static void OnIdleTimer(bool iisProfiling = false)
        {
            lock (_lockObject)
            {
                if (_profilingList.Count > 0)
                {
                    var tasks = new List<Task>();

                    // Manually stop any profiling session which has exceeded the timeout period.
                    // TODO: VS 2015 Update 1 should have a better way to handle this.
                    foreach (var item in _profilingList)
                    {
                        if (iisProfiling)
                        {
                            if (DateTime.UtcNow - item.Value.StartTime > _profilingIisTimeout)
                            {
                                tasks.Add(Task.Run(() => StopProfileInternalAsync(item.Key, item.Value.SessionId, true, null, iisProfiling)));
                            }
                        }
                        else
                        {
                            if (DateTime.UtcNow - item.Value.StartTime > _profilingTimeout)
                            {
                                tasks.Add(Task.Run(() => StopProfileInternalAsync(item.Key, item.Value.SessionId, true)));
                            }
                        }
                    }

                    Task.WaitAll(tasks.ToArray());
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

        internal class ProfileResultInfo
        {
            public ProfileResultInfo(HttpStatusCode statusCode, string message)
            {
                this.StatusCode = statusCode;
                this.Message = message;
            }

            public HttpStatusCode StatusCode { get; set; }

            public string Message { get; set; }
        }

        private class ProfileInfo
        {
            public ProfileInfo(int sessionId, bool iisProfiling)
            {
                this.SessionId = sessionId;
                this.StartTime = DateTime.UtcNow;
                this.IsIisProfiling = iisProfiling;
            }

            public int SessionId { get; set; }
           
            public DateTime StartTime { get; set; }

            public bool IsIisProfiling { get; set; }
        }
    }
}
