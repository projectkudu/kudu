using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Deployment;
using Kudu.Core.Deployment.Generator;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Kudu.Services.Commands;
using Microsoft.AspNet.SignalR;

namespace Kudu.Services
{
    public class PersistentCommandController : PersistentConnection
    {
        public const int MaxProcesses = 5;
        protected static readonly ConcurrentDictionary<string, ProcessInfo> _processes = new ConcurrentDictionary<string, ProcessInfo>();
        private static readonly TimeSpan _cmdWaitTimeSpan = TimeSpan.FromSeconds(2);
        private static readonly TimeSpan _idleTimeout = TimeSpan.FromMinutes(30);
        private static Timer _idleTimer;

        private readonly ITracer _tracer;
        private readonly IEnvironment _environment;
        private readonly IDeploymentSettingsManager _settings;

        public PersistentCommandController(IEnvironment environment, IDeploymentSettingsManager settings, ITracer tracer)
        {
            _environment = environment;
            _tracer = tracer;
            _settings = settings;
        }

        protected override Task OnConnected(IRequest request, string connectionId)
        {
            var shell = request.QueryString != null ? request.QueryString["shell"] : null;
            using (_tracer.Step("Client connected with connectionId = " + connectionId))
            {
                _processes.GetOrAdd(connectionId, cId => StartProcess(cId, shell));

                return base.OnConnected(request, connectionId);
            }
        }

        protected override Task OnDisconnected(IRequest request, string connectionId)
        {
            using (_tracer.Step("Client Disconected with connectionId = " + connectionId))
            {
                KillProcess(connectionId, _tracer);

                return base.OnDisconnected(request, connectionId);
            }
        }

        protected override Task OnReceived(IRequest request, string connectionId, string data)
        {
            ProcessInfo process;
            var shell = request.QueryString != null ? request.QueryString["shell"] : null;
            if (!_processes.TryGetValue(connectionId, out process) || process.Process.HasExited)
            {
                process = _processes.AddOrUpdate(connectionId, cId => StartProcess(cId, shell), (s, p) => StartProcess(s, shell));
            }
            else
            {
                if (data == "\x3")
                {
                    // If the user hit CTRL+C we sent the ^C character "\x3" from the client
                    // If the data is just ^C we then attach to the console and generate a CTRL_C signal (SIGINT)
                    CommandsNativeMethods.SetConsoleCtrlHandler(null, true);
                    CommandsNativeMethods.AttachConsole((uint)process.Process.Id);
                    CommandsNativeMethods.GenerateConsoleCtrlEvent(CommandsNativeMethods.ConsoleCtrlEvent.CTRL_C, 0);
                    Thread.Sleep(_cmdWaitTimeSpan);
                    CommandsNativeMethods.FreeConsole();
                    CommandsNativeMethods.SetConsoleCtrlHandler(null, false);
                }
                else
                {
                    process.Process.StandardInput.WriteLine(data.Replace("\n", ""));
                    process.Process.StandardInput.Flush();
                }
                process.LastInputTime = DateTime.UtcNow;
            }

            return base.OnReceived(request, connectionId, data);
        }

        protected virtual IProcess CreateProcess(string connectionId, string shell)
        {
            var externalCommandFactory = new ExternalCommandFactory(_environment, _settings, _environment.RootPath);
            var exe = externalCommandFactory.BuildExternalCommandExecutable(_environment.RootPath, _environment.WebRootPath, NullLogger.Instance);
            var startInfo = new ProcessStartInfo()
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                WorkingDirectory = _environment.RootPath
            };

            if (shell.Equals("powershell", StringComparison.OrdinalIgnoreCase))
            {
                startInfo.FileName = System.Environment.ExpandEnvironmentVariables(@"%windir%\System32\WindowsPowerShell\v1.0\powershell.exe");
                startInfo.Arguments = "-File -";
            }
            else
            {
                startInfo.FileName = System.Environment.ExpandEnvironmentVariables(@"%windir%\System32\cmd.exe");
                startInfo.Arguments = "/Q";
            }

            foreach (var environmentVariable in exe.EnvironmentVariables)
            {
                startInfo.EnvironmentVariables[environmentVariable.Key] = environmentVariable.Value;
            }

            // add '>' to distinguish PROMPT from other output
            startInfo.EnvironmentVariables["PROMPT"] = "$P$G";

            // dir cmd would list folders then files alpabetically
            // consistent with FileBrowser ui.
            startInfo.EnvironmentVariables["DIRCMD"] = "/OG /ON";

            var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.Exited += delegate
            {
                SafeInvoke(() =>
                {
                    ProcessInfo temp;
                    _processes.TryRemove(connectionId, out temp);
                    Connection.Send(connectionId, new { Output = "\r\nprocess [" + process.Id + "] terminated!  Press ENTER to start a new cmd process.\r\n", RunningProcessesCount = _processes.Count }).Wait();
                });
            };

            process.Start();

            EnsureIdleTimer();

            HookProcessStreamsToConnection(process, connectionId);

            return new ProcessWrapper(process);
        }

        private void HookProcessStreamsToConnection(Process process, string connectionId)
        {
            var thread = new Thread(() =>
            {
                ListenAndSendStreamAsync(process, process.StandardOutput, connectionId, isError: false);
                ListenAndSendStreamAsync(process, process.StandardError, connectionId, isError: true);
            });

            thread.Start();
            thread.Join();
        }

        private ProcessInfo StartProcess(string connectionId, string shell)
        {
            using (_tracer.Step("start process for connectionId = " + connectionId))
            {
                var process = CreateProcess(connectionId, shell);
                _tracer.Trace("process " + process.Id + " started");
                EnsureMaxProcesses();
                return new ProcessInfo(process);
            }
        }

        private async void ListenAndSendStreamAsync(Process process, TextReader textReader, string connectionId, bool isError)
        {
            var strb = new StringBuilder(1024);
            try
            {
                while (!process.HasExited)
                {
                    StreamResult line;
                    while ((line = await ReadLineAsync(textReader, strb.Clear())) != null)
                    {

                        if (isError)
                        {
                            lock (Connection)
                            {
                                do
                                {
                                    Connection.Send(connectionId, new { Error = line.Value, ProcessId = process.Id, RunningProcessesCount = _processes.Count }).Wait();
                                    Thread.Sleep(10);
                                } while (line.HasNext && (line = ReadLineAsync(textReader, strb.Clear()).Result) != null);
                            }
                        }
                        else
                        {
                            lock (Connection)
                            {
                                do
                                {
                                    Connection.Send(connectionId, new { Output = line.Value, ProcessId = process.Id, RunningProcessesCount = _processes.Count }).Wait();
                                    Thread.Sleep(10);
                                } while (line.HasNext && (line = ReadLineAsync(textReader, strb.Clear()).Result) != null);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                SafeInvoke(() => KillProcess(connectionId));
            }
        }

        // Unlike normal ReadLine, this returns the line content with new line characters.
        public static async Task<StreamResult> ReadLineAsync(TextReader reader, StringBuilder builder)
        {
            bool written = false;
            char[] chars = new char[1];
            while (true)
            {
                int num = await reader.ReadAsync(chars, 0, chars.Length);
                if (num <= 0)
                {
                    return written ? new StreamResult(builder.ToString(), hasMore:reader.Peek() != -1) : null;
                }

                if (chars[0] == '\r' || chars[0] == '\n')
                {
                    builder.Append(chars[0]);
                    if (chars[0] == '\r' && reader.Peek() == (int)'\n')
                    {
                        builder.Append((char)reader.Read());
                    }

                    return new StreamResult(builder.ToString(), hasMore: reader.Peek() != -1);
                }

                written = true;
                builder.Append(chars[0]);

                // to anticipate last non-ending line
                if (reader.Peek() == -1)
                {
                    return written ? new StreamResult(builder.ToString(), hasMore: reader.Peek() != -1) : null;
                }
            }
        }

        private void EnsureMaxProcesses()
        {
            // Keep ones with most recent input
            while (_processes.Count >= MaxProcesses)
            {
                var toRemove = _processes.OrderBy(p => p.Value.LastInputTime).LastOrDefault();
                if (String.IsNullOrEmpty(toRemove.Key))
                {
                    break;
                }

                KillProcess(toRemove.Key, _tracer);
            }
        }

        private static void KillProcess(string connectionId, ITracer tracer = null)
        {
            ProcessInfo process;
            if (_processes.TryRemove(connectionId, out process))
            {
                tracer = tracer ?? NullTracer.Instance;
                using (tracer.Step("process " + process.Process.Id + " killed!"))
                {
                    process.Process.Kill(tracer);
                }

                lock (_processes)
                {
                    if (_processes.Count == 0)
                    {
                        if (_idleTimer != null)
                        {
                            _idleTimer.Dispose();
                            _idleTimer = null;
                        }
                    }
                }
            }
        }

        private static void EnsureIdleTimer()
        {
            lock (_processes)
            {
                if (_idleTimer == null)
                {
                    _idleTimer = new Timer(_ => SafeInvoke(() => OnIdleTimer()), null, _idleTimeout, _idleTimeout);
                }
            }
        }

        private static void OnIdleTimer()
        {
            lock (_processes)
            {
                if (_processes.Count == 0)
                {
                    if (_idleTimer != null)
                    {
                        _idleTimer.Dispose();
                        _idleTimer = null;
                    }
                }
                else
                {
                    var lastInputTime = DateTime.UtcNow - _idleTimeout;
                    foreach (var toRemove in _processes.Where(p => p.Value.LastInputTime < lastInputTime))
                    {
                        KillProcess(toRemove.Key);
                    }
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

        public class StreamResult
        {
            public StreamResult(string value, bool hasMore)
            {
                Value = value;
                HasNext = hasMore;
            }

            public string Value { get; private set; }
            public bool HasNext { get; private set; }
        }

        public class ProcessInfo
        {
            public ProcessInfo(IProcess process)
            {
                this.Process = process;
                this.LastInputTime = DateTime.UtcNow;
            }

            public IProcess Process
            {
                get;
                set;
            }

            public DateTime LastInputTime
            {
                get;
                set;
            }
        }
    }
}
