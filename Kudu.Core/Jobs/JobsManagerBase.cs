using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Hosting;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;
using Newtonsoft.Json;

namespace Kudu.Core.Jobs
{
    public abstract class JobsManagerBase
    {
        protected static readonly IScriptHost[] ScriptHosts = new IScriptHost[]
        {
            new WindowsScriptHost(),
            new PowerShellScriptHost(),
            new BashScriptHost(),
            new PythonScriptHost(),
            new PhpScriptHost(),
            new NodeScriptHost()
        };

        public static bool IsUsingSdk(string specificJobDataPath)
        {
            try
            {
                string webJobsSdkMarkerFilePath = Path.Combine(specificJobDataPath, "webjobssdk.marker");
                return FileSystemHelpers.FileExists(webJobsSdkMarkerFilePath);
            }
            catch
            {
                return false;
            }
        }
    }

    public abstract class JobsManagerBase<TJob> : JobsManagerBase, IJobsManager<TJob>, IRegisteredObject where TJob : JobBase, new()
    {
        private const string DefaultScriptFileName = "run";

        private readonly string _jobsTypePath;

        private string _lastKnownAppBaseUrlPrefix;

        protected IEnvironment Environment { get; private set; }

        protected IDeploymentSettingsManager Settings { get; private set; }

        protected ITraceFactory TraceFactory { get; private set; }

        public string JobsBinariesPath { get; private set; }

        protected string JobsDataPath { get; private set; }

        protected IAnalytics Analytics { get; private set; }

        protected JobsManagerBase(ITraceFactory traceFactory, IEnvironment environment, IDeploymentSettingsManager settings, IAnalytics analytics, string jobsTypePath)
        {
            TraceFactory = traceFactory;
            Environment = environment;
            Settings = settings;
            Analytics = analytics;

            _jobsTypePath = jobsTypePath;

            JobsBinariesPath = Path.Combine(Environment.JobsBinariesPath, jobsTypePath);
            JobsDataPath = Path.Combine(Environment.JobsDataPath, jobsTypePath);

            HostingEnvironment.RegisterObject(this);
        }

        public abstract IEnumerable<TJob> ListJobs();

        public abstract TJob GetJob(string jobName);

        public TJob CreateOrReplaceJobFromZipStream(Stream zipStream, string jobName)
        {
            return CreateOrReplaceJob(jobName,
                (jobDirectory) =>
                {
                    using (var zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Read))
                    {
                        zipArchive.Extract(jobDirectory.FullName);
                    }
                });
        }

        public TJob CreateOrReplaceJobFromFileStream(Stream scriptFileStream, string jobName, string scriptFileName)
        {
            return CreateOrReplaceJob(jobName,
                (jobDirectory) =>
                {
                    string filePath = Path.Combine(jobDirectory.FullName, scriptFileName);
                    using (Stream destinationFileStream = FileSystemHelpers.OpenFile(filePath, FileMode.Create))
                    {
                        scriptFileStream.CopyTo(destinationFileStream);
                    }
                });
        }

        private TJob CreateOrReplaceJob(string jobName, Action<DirectoryInfoBase> writeJob)
        {
            DirectoryInfoBase jobDirectory = GetJobDirectory(jobName);
            if (jobDirectory.Exists)
            {
                // If job binaries already exist, remove them to make place for new job binaries
                OperationManager.Attempt(
                    () => FileSystemHelpers.DeleteDirectorySafe(jobDirectory.FullName, ignoreErrors: false));
            }

            jobDirectory.Create();
            jobDirectory = GetJobDirectory(jobName); // regenerating the DirectoryInfoBase instance to populate the Exists method with true.

            writeJob(jobDirectory);

            return BuildJob(jobDirectory, nullJobOnError: false);
        }

        public void DeleteJob(string jobName)
        {
            try
            {
                var jobDirectory = GetJobDirectory(jobName);
                if (!jobDirectory.Exists)
                {
                    return;
                }

                var jobsSpecificDataPath = GetSpecificJobDataPath(jobName);

                // Remove both job binaries and data directories
                OperationManager.Attempt(() =>
                {
                    FileSystemHelpers.DeleteDirectorySafe(jobDirectory.FullName, ignoreErrors: false);
                    FileSystemHelpers.DeleteDirectorySafe(jobsSpecificDataPath, ignoreErrors: false);
                }, retries: 3, delayBeforeRetry: 2000);
            }
            catch (Exception ex)
            {
                // Ignore failure to remove job here
                TraceFactory.GetTracer().TraceError(ex.ToString());
            }
        }

        public void CleanupDeletedJobs()
        {
            IEnumerable<TJob> jobs = ListJobs();
            IEnumerable<string> jobNames = jobs.Select(j => j.Name);
            DirectoryInfoBase jobsDataDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(JobsDataPath);
            if (jobsDataDirectory.Exists)
            {
                DirectoryInfoBase[] jobDataDirectories = jobsDataDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly);
                IEnumerable<string> allJobDataDirectories = jobDataDirectories.Select(j => j.Name);
                IEnumerable<string> directoriesToRemove = allJobDataDirectories.Except(jobNames, StringComparer.OrdinalIgnoreCase);
                foreach (string directoryToRemove in directoriesToRemove)
                {
                    TraceFactory.GetTracer().Trace("Removed job data path as the job was already deleted: " + directoryToRemove);
                    FileSystemHelpers.DeleteDirectorySafe(Path.Combine(JobsDataPath, directoryToRemove));
                }
            }
        }

        private string GetSpecificJobDataPath(string jobName)
        {
            return Path.Combine(JobsDataPath, jobName);
        }

        protected TJob GetJobInternal(string jobName)
        {
            DirectoryInfoBase jobDirectory = GetJobDirectory(jobName);
            return BuildJob(jobDirectory);
        }

        protected IEnumerable<TJob> ListJobsInternal()
        {
            var jobs = new List<TJob>();

            IEnumerable<DirectoryInfoBase> jobDirectories = ListJobDirectories(JobsBinariesPath);
            foreach (DirectoryInfoBase jobDirectory in jobDirectories)
            {
                TJob job = BuildJob(jobDirectory);
                if (job != null)
                {
                    jobs.Add(job);
                }
            }

            return jobs;
        }

        protected TJob BuildJob(DirectoryInfoBase jobDirectory, bool nullJobOnError = true)
        {
            try
            {
                if (!jobDirectory.Exists)
                {
                    return null;
                }

                DirectoryInfoBase jobScriptDirectory = GetJobScriptDirectory(jobDirectory);

                string jobName = jobDirectory.Name;
                FileInfoBase[] files = jobScriptDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly);
                IScriptHost scriptHost;
                string scriptFilePath = FindCommandToRun(files, out scriptHost);

                if (scriptFilePath == null)
                {
                    // Return a job representing an error for no runnable script file found for job
                    if (nullJobOnError)
                    {
                        return null;
                    }

                    return new TJob
                    {
                        Name = jobName,
                        JobType = _jobsTypePath,
                        Error = Resources.Error_NoRunnableScriptForJob,
                    };
                }

                string runCommand = scriptFilePath.Substring(jobDirectory.FullName.Length + 1);

                var job = new TJob
                {
                    Name = jobName,
                    Url = BuildJobsUrl(jobName),
                    ExtraInfoUrl = BuildExtraInfoUrl(jobName),
                    ScriptFilePath = scriptFilePath,
                    RunCommand = runCommand,
                    JobType = _jobsTypePath,
                    ScriptHost = scriptHost,
                    UsingSdk = IsUsingSdk(GetSpecificJobDataPath(jobName)),
                    JobBinariesRootPath = jobScriptDirectory.FullName,
                    Settings = GetJobSettings(jobName)
                };

                UpdateJob(job);

                return job;
            }
            catch (Exception ex)
            {
                Analytics.UnexpectedException(ex);

                // Return a job representing an error for no runnable script file found for job
                if (nullJobOnError)
                {
                    return null;
                }

                return new TJob
                {
                    JobType = _jobsTypePath,
                    Error = ex.Message,
                };
            }
        }

        /// <summary>
        /// Deploy (external) jobs from {sourcePath} by moving them over to the main jobs directory (JobsBinariesPath)
        /// These external jobs usually come from site extensions
        /// </summary>
        public void SyncExternalJobs(string sourcePath, string sourceName)
        {
            sourcePath = Path.Combine(sourcePath, "App_Data\\jobs", _jobsTypePath);

            CleanupExternalJobs(sourceName);

            // Move jobs from source path
            IEnumerable<DirectoryInfoBase> sourceJobDirectories = ListJobDirectories(sourcePath);
            foreach (DirectoryInfoBase sourceJobDirectory in sourceJobDirectories)
            {
                // Check whether job was already copied by checking existence of file job.copied
                string jobPath = sourceJobDirectory.FullName;
                MoveExternalJob(jobPath, sourceName);
            }
        }

        public void CleanupExternalJobs(string sourceName)
        {
            // Find all jobs for the source name provided
            // Job name will look like: {source name}({job name}) for example: "daas(sitepinger)"
            IEnumerable<DirectoryInfoBase> jobDirectories = ListJobDirectories(JobsBinariesPath, sourceName + "(*)");
            foreach (DirectoryInfoBase jobDirectory in jobDirectories)
            {
                DeleteJob(jobDirectory.Name);
            }
        }

        private void MoveExternalJob(string sourcePath, string sourceName)
        {
            string jobName = "{0}({1})".FormatInvariant(sourceName, Path.GetFileName(sourcePath));
            string toPath = Path.Combine(JobsBinariesPath, jobName);
            FileSystemHelpers.DeleteDirectorySafe(toPath);
            FileSystemHelpers.EnsureDirectory(Path.GetDirectoryName(toPath));
            Directory.Move(sourcePath, toPath);
        }

        public JobSettings GetJobSettings(string jobName)
        {
            JobSettings jobSettings;

            try
            {
                jobSettings = OperationManager.Attempt(() =>
                {
                    var jobDirectory = GetJobBinariesDirectory(jobName);

                    var jobSettingsPath = GetJobSettingsPath(jobDirectory);
                    if (!FileSystemHelpers.FileExists(jobSettingsPath))
                    {
                        return null;
                    }

                    string jobSettingsContent = FileSystemHelpers.ReadAllTextFromFile(jobSettingsPath);
                    return JsonConvert.DeserializeObject<JobSettings>(jobSettingsContent);
                });
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex.ToString());
                jobSettings = null;
            }

            return jobSettings ?? new JobSettings();
        }

        public void SetJobSettings(string jobName, JobSettings jobSettings)
        {
            var jobDirectory = GetJobBinariesDirectory(jobName);

            var jobSettingsPath = GetJobSettingsPath(jobDirectory);
            string jobSettingsContent = JsonConvert.SerializeObject(jobSettings);
            FileSystemHelpers.WriteAllTextToFile(jobSettingsPath, jobSettingsContent);
        }

        public void Stop(bool immediate)
        {
            if (IsShuttingdown)
            {
                return;
            }

            IsShuttingdown = true;
            OnShutdown();
            HostingEnvironment.UnregisterObject(this);
        }

        protected abstract void OnShutdown();

        protected bool IsShuttingdown { get; private set; }

        private static string GetJobSettingsPath(DirectoryInfoBase jobDirectory)
        {
            return Path.Combine(jobDirectory.FullName, JobSettings.JobSettingsFileName);
        }

        protected abstract void UpdateJob(TJob job);

        protected TJobStatus GetStatus<TJobStatus>(string statusFilePath) where TJobStatus : class, IJobStatus, new()
        {
            return JobLogger.ReadJobStatusFromFile<TJobStatus>(TraceFactory, statusFilePath) ?? new TJobStatus();
        }

        protected Uri BuildJobsUrl(string relativeUrl)
        {
            if (AppBaseUrlPrefix == null)
            {
                return null;
            }

            return new Uri("{0}/api/{1}webjobs/{2}".FormatInvariant(AppBaseUrlPrefix, _jobsTypePath, relativeUrl));
        }

        protected Uri BuildVfsUrl(string relativeUrl)
        {
            if (AppBaseUrlPrefix == null)
            {
                return null;
            }

            return new Uri("{0}/vfs/data/jobs/{1}/{2}".FormatInvariant(AppBaseUrlPrefix, _jobsTypePath, relativeUrl));
        }

        private Uri BuildExtraInfoUrl(string jobName)
        {
            if (AppBaseUrlPrefix == null)
            {
                return null;
            }

            return new Uri("{0}/azurejobs/#/jobs/{1}/{2}".FormatInvariant(AppBaseUrlPrefix, _jobsTypePath, jobName));
        }

        protected string AppBaseUrlPrefix
        {
            get
            {
                if (HttpContext.Current == null)
                {
                    return _lastKnownAppBaseUrlPrefix;
                }

                _lastKnownAppBaseUrlPrefix = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);
                return _lastKnownAppBaseUrlPrefix;
            }
        }

        private static IEnumerable<DirectoryInfoBase> ListJobDirectories(string path, string searchPattern = "*")
        {
            if (!FileSystemHelpers.DirectoryExists(path))
            {
                return Enumerable.Empty<DirectoryInfoBase>();
            }

            DirectoryInfoBase jobsDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(path);
            return jobsDirectory.GetDirectories(searchPattern, SearchOption.TopDirectoryOnly);
        }

        private DirectoryInfoBase GetJobDirectory(string jobName)
        {
            string jobPath = Path.Combine(JobsBinariesPath, jobName);
            return FileSystemHelpers.DirectoryInfoFromDirectoryName(jobPath);
        }

        private DirectoryInfoBase GetJobBinariesDirectory(string jobName)
        {
            DirectoryInfoBase jobDirectory = GetJobDirectory(jobName);
            if (!jobDirectory.Exists)
            {
                throw new JobNotFoundException();
            }

            return GetJobScriptDirectory(jobDirectory);
        }

        private DirectoryInfoBase GetJobScriptDirectory(DirectoryInfoBase jobDirectory)
        {
            // Return the directory where the script should be found using the following logic:
            // If current directory (jobDirectory) has only one sub-directory and no files recurse this using that sub-directory
            // Otherwise return current directory
            if (jobDirectory != null && jobDirectory.Exists)
            {
                var jobFiles = jobDirectory.GetFileSystemInfos();
                if (jobFiles.Length == 1 && jobFiles[0] is DirectoryInfoBase)
                {
                    return GetJobScriptDirectory(jobFiles[0] as DirectoryInfoBase);
                }
            }

            return jobDirectory;
        }

        private static string FindCommandToRun(FileInfoBase[] files, out IScriptHost scriptHostFound)
        {
            string secondaryScriptFound = null;

            scriptHostFound = null;

            foreach (IScriptHost scriptHost in ScriptHosts)
            {
                if (String.IsNullOrEmpty(scriptHost.HostPath))
                {
                    continue;
                }

                foreach (string supportedExtension in scriptHost.SupportedExtensions)
                {
                    var supportedFiles = files.Where(f => String.Equals(f.Extension, supportedExtension, StringComparison.OrdinalIgnoreCase));
                    if (supportedFiles.Any())
                    {
                        var scriptFound =
                            supportedFiles.FirstOrDefault(f => String.Equals(f.Name, DefaultScriptFileName + supportedExtension, StringComparison.OrdinalIgnoreCase));

                        if (scriptFound != null)
                        {
                            scriptHostFound = scriptHost;
                            return scriptFound.FullName;
                        }

                        if (secondaryScriptFound == null)
                        {
                            scriptHostFound = scriptHost;
                            secondaryScriptFound = supportedFiles.First().FullName;
                        }
                    }
                }
            }

            if (secondaryScriptFound != null)
            {
                return secondaryScriptFound;
            }

            return null;
        }
    }
}