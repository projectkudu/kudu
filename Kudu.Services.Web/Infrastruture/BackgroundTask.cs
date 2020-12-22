using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Kudu.Contracts.Settings;
using Kudu.Core.Helpers;
using Kudu.Core.Infrastructure;
using Kudu.Core.Settings;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Services.Web.Infrastruture
{
    public static class BackgroundTask
    {
        public const int DefaulTelemetryIntervalMinutes = 30;

        public readonly static Lazy<string> AppServiceVersion = new Lazy<string>(() =>
        {
            var assembly = Assembly.Load("Microsoft.Web.Hosting, Version=7.1.0.0, Culture=neutral, PublicKeyToken=31bf3856ad364e35");
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        });

        public readonly static Lazy<string> KuduVersion = new Lazy<string>(() =>
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            return fvi.FileVersion;
        });

        private static int _running = 0;
        private static ManualResetEvent _shutdownEvent;
        private static DateTime _startupDateTime = DateTime.UtcNow;
        private static DateTime _nextTelemetryDateTime = DateTime.MinValue;

        public static void Start()
        {
            if (!Kudu.Core.Environment.IsAzureEnvironment()
                || !OSDetector.IsOnWindows())
            {
                return;
            }

            if (Interlocked.Exchange(ref _running, 1) != 0)
            {
                return;
            }

            // Cleanup Sentinel on Startup
            OperationManager.SafeExecute(() => 
            {
                FileSystemHelpers.DeleteDirectorySafe(Path.Combine(PathUtilityFactory.Instance.ResolveLocalSitePath(), @"ShutdownSentinel"));
            });

            ThreadPool.QueueUserWorkItem(BackgroundTaskProc, new ManualResetEvent(initialState: false));
        }

        public static void Shutdown()
        {
            if (_shutdownEvent != null)
            {
                _shutdownEvent.Set();
            }
        }

        private static void BackgroundTaskProc(object state)
        {
            var shutdownEvent = (ManualResetEvent)state;
            _shutdownEvent = shutdownEvent;

            do
            {
                OperationManager.SafeExecute(EmitTelemetry);
            } while (!shutdownEvent.WaitOne(TimeSpan.FromMinutes(10)));
        }

        private static void EmitTelemetry()
        {
            int interval = DefaulTelemetryIntervalMinutes;

            var now = DateTime.UtcNow;
            if (_nextTelemetryDateTime < now)
            {
                interval = GetTelemetryIntervalMinutes(interval);
                _nextTelemetryDateTime = now.AddMinutes(interval > 0 ? interval : DefaulTelemetryIntervalMinutes);
                if (interval <= 0)
                {
                    return;
                }

                var telemetry = new Dictionary<string, object>();
                AddProcessInfo(telemetry);
                AddSiteInfo(telemetry);
                AddDiskSpaceInfo(telemetry);

                KuduEventSource.Log.GenericEvent(
                    ServerConfiguration.GetApplicationName(),
                    $"Telemetry: {JsonConvert.SerializeObject(telemetry)}",
                    string.Empty,
                    Environment.GetEnvironmentVariable(SettingsKeys.ScmType),
                    Environment.GetEnvironmentVariable(SettingsKeys.WebSiteSku),
                    KuduVersion.Value);
            }
        }

        private static int GetTelemetryIntervalMinutes(int defaultInterval)
        {
            try
            {
                var value = ScmHostingConfigurations.GetValue("TelemetryIntervalMinutes", $"{DefaulTelemetryIntervalMinutes}");
                return int.TryParse(value, out int interval) ? interval : 0;
            }
            catch (Exception)
            {
            }

            return defaultInterval;
        }

        private static void AddProcessInfo(Dictionary<string, object> telemetry)
        {
            telemetry["processId"] = Process.GetCurrentProcess().Id;
            telemetry["appDomainId"] = AppDomain.CurrentDomain.Id;
            telemetry["startupTime"] = $"{ _startupDateTime:s}Z";
            telemetry["appServiceVersion"] = AppServiceVersion.Value;
            telemetry["kuduVersion"] = KuduVersion.Value;
        }

        private static void AddSiteInfo(Dictionary<string, object> telemetry)
        {
            var ownerName = Environment.GetEnvironmentVariable(SettingsKeys.WebSiteOwnerName);
            if (!string.IsNullOrEmpty(ownerName))
            {
                var parts = ownerName.Split('+');
                telemetry["subscriptionId"] = parts.FirstOrDefault();
                telemetry["webspaceName"] = parts.Length > 1 ? parts[1] : null;
            }

            telemetry["runtimeSiteName"] = Environment.GetEnvironmentVariable(SettingsKeys.WebSiteName);
            telemetry["defaultHostName"] = Environment.GetEnvironmentVariable(SettingsKeys.WebSiteHostName);
            telemetry["scmType"] = Environment.GetEnvironmentVariable(SettingsKeys.ScmType);
            telemetry["sku"] = Environment.GetEnvironmentVariable(SettingsKeys.WebSiteSku);
            telemetry["scmHostingConfigurations"] = FileSystemHelpers.FileExists(ScmHostingConfigurations.ConfigsFile) ? FileSystemHelpers.ReadAllText(ScmHostingConfigurations.ConfigsFile) : null;
        }

        private static void AddDiskSpaceInfo(Dictionary<string, object> telemetry)
        {
            if (Kudu.Core.Helpers.EnvironmentHelper.IsWindowsContainers())
            {
                return;
            }

            ulong freeBytes;
            ulong totalBytes;
            var homePath = Environment.GetEnvironmentVariable("HOME");
            var localPath = Environment.ExpandEnvironmentVariables("%SystemDrive%\\local");
            var userProfilePath = Environment.ExpandEnvironmentVariables("%SystemDrive%\\users\\%WEBSITE_SITE_NAME%");

            OperationManager.SafeExecute(() =>
            {
                telemetry["homePath"] = homePath;
                Kudu.Core.Environment.GetDiskFreeSpace(homePath, out freeBytes, out totalBytes);
                telemetry["homeFreeMB"] = freeBytes / (1024 * 1024);
                telemetry["homeTotalMB"] = totalBytes / (1024 * 1024);
            });

            OperationManager.SafeExecute(() =>
            {
                telemetry["localPath"] = localPath;
                Kudu.Core.Environment.GetDiskFreeSpace(localPath, out freeBytes, out totalBytes);
                telemetry["localFreeMB"] = freeBytes / (1024 * 1024);
                telemetry["localTotalMB"] = totalBytes / (1024 * 1024);
            });

            OperationManager.SafeExecute(() =>
            {
                if (FileSystemHelpers.DirectoryExists(userProfilePath))
                {
                    telemetry["userProfilePath"] = userProfilePath;
                    Kudu.Core.Environment.GetDiskFreeSpace(userProfilePath, out freeBytes, out totalBytes);
                    telemetry["userProfileFreeMB"] = freeBytes / (1024 * 1024);
                    telemetry["userProfileTotalMB"] = totalBytes / (1024 * 1024);
                }
            });
        }
    }
}