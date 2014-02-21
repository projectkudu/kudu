using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using System.Web;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
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

        public static string GetJobExtraInfoUrlFilePath(string jobsSpecificDataPath)
        {
            return Path.Combine(jobsSpecificDataPath, "job.extra_info_url.template");
        }
    }

    public abstract class JobsManagerBase<TJob> : JobsManagerBase, IJobsManager<TJob> where TJob : JobBase, new()
    {
        private const string DefaultScriptFileName = "run";

        private readonly string _jobsTypePath;

        private string _appBaseUrlPrefix;
        private string _urlPrefix;
        private string _vfsUrlPrefix;

        protected IEnvironment Environment { get; private set; }

        protected IDeploymentSettingsManager Settings { get; private set; }

        protected ITraceFactory TraceFactory { get; private set; }

        protected string JobsBinariesPath { get; private set; }

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

        public async Task DeleteJobAsync(string jobName)
        {
            var jobDirectory = GetJobDirectory(jobName);
            if (!jobDirectory.Exists)
            {
                return;
            }

            string jobsSpecificDataPath = Path.Combine(JobsDataPath, jobName);

            // Remove both job binaries and data directories
            await OperationManager.AttemptAsync(() =>
            {
                FileSystemHelpers.DeleteDirectorySafe(jobDirectory.FullName, ignoreErrors: false);
                FileSystemHelpers.DeleteDirectorySafe(jobsSpecificDataPath, ignoreErrors: false);
                return Task.FromResult(true);
            }, retries: 3, delayBeforeRetry: 2000);
        }

        protected TJob GetJobInternal(string jobName)
        {
            DirectoryInfoBase jobDirectory = GetJobDirectory(jobName);
            return BuildJob(jobDirectory);
        }

        protected IEnumerable<TJob> ListJobsInternal()
        {
            var jobs = new List<TJob>();

            if (!FileSystemHelpers.DirectoryExists(JobsBinariesPath))
            {
                return Enumerable.Empty<TJob>();
            }

            DirectoryInfoBase jobsDirectory = FileSystemHelpers.DirectoryInfoFromDirectoryName(JobsBinariesPath);
            DirectoryInfoBase[] jobDirectories = jobsDirectory.GetDirectories("*", SearchOption.TopDirectoryOnly);
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
            if (!jobDirectory.Exists)
            {
                return null;
            }

            string jobName = jobDirectory.Name;
            FileInfoBase[] files = jobDirectory.GetFiles("*.*", SearchOption.TopDirectoryOnly);
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
                ScriptHost = scriptHost
            };

            UpdateJob(job);

            return job;
        }

        public JobSettings GetJobSettings(string jobName)
        {
            return OperationManager.Attempt(() =>
            {
                var jobDirectory = GetJobDirectory(jobName);
                if (!jobDirectory.Exists)
                {
                    throw new JobNotFoundException();
                }

                var jobSettingsPath = GetJobSettingsPath(jobDirectory);
                if (!FileSystemHelpers.FileExists(jobSettingsPath))
                {
                    return new JobSettings();
                }

                string jobSettingsContent = FileSystemHelpers.ReadAllTextFromFile(jobSettingsPath);
                return JsonConvert.DeserializeObject<JobSettings>(jobSettingsContent);
            });
        }

        public void SetJobSettings(string jobName, JobSettings jobSettings)
        {
            var jobDirectory = GetJobDirectory(jobName);
            if (!jobDirectory.Exists)
            {
                throw new JobNotFoundException();
            }

            var jobSettingsPath = GetJobSettingsPath(jobDirectory);
            string jobSettingsContent = JsonConvert.SerializeObject(jobSettings);
            FileSystemHelpers.WriteAllTextToFile(jobSettingsPath, jobSettingsContent);
        }

        private static string GetJobSettingsPath(DirectoryInfoBase jobDirectory)
        {
            return Path.Combine(jobDirectory.FullName, "settings.job");
        }

        protected abstract void UpdateJob(TJob job);

        protected TJobStatus GetStatus<TJobStatus>(string statusFilePath) where TJobStatus : class, IJobStatus, new()
        {
            return JobLogger.ReadJobStatusFromFile<TJobStatus>(TraceFactory, statusFilePath) ?? new TJobStatus();
        }

        protected Uri BuildJobsUrl(string relativeUrl)
        {
            if (_urlPrefix == null)
            {
                if (AppBaseUrlPrefix == null)
                {
                    return null;
                }

                _urlPrefix = "{0}/jobs/{1}/".FormatInvariant(AppBaseUrlPrefix, _jobsTypePath);
            }

            return new Uri(_urlPrefix + relativeUrl);
        }

        protected Uri BuildVfsUrl(string relativeUrl)
        {
            if (_vfsUrlPrefix == null)
            {
                if (AppBaseUrlPrefix == null)
                {
                    return null;
                }

                _vfsUrlPrefix = "{0}/vfs/data/jobs/{1}/".FormatInvariant(AppBaseUrlPrefix, _jobsTypePath);
            }

            return new Uri(_vfsUrlPrefix + relativeUrl);
        }

        protected Uri BuildExtraInfoUrl(string jobName)
        {
            try
            {
                string jobsSpecificDataPath = Path.Combine(JobsDataPath, jobName);
                string extraInfoUrlTemplate = LoadExtraInfoUrlTemplateFromFile(jobsSpecificDataPath);
                if (extraInfoUrlTemplate != null)
                {
                    extraInfoUrlTemplate = extraInfoUrlTemplate.Replace("{jobName}", jobName);
                    extraInfoUrlTemplate = extraInfoUrlTemplate.Replace("{jobType}", _jobsTypePath);
                    if (extraInfoUrlTemplate.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        return new Uri(extraInfoUrlTemplate);
                    }

                    if (AppBaseUrlPrefix == null)
                    {
                        return null;
                    }

                    return new Uri(AppBaseUrlPrefix + extraInfoUrlTemplate);
                }
            }
            catch (Exception ex)
            {
                // On exception trace and use the default extra info url
                TraceFactory.GetTracer().TraceError(ex);
            }

            return BuildDefaultExtraInfoUrl(jobName);
        }

        /// <summary>
        /// Load the extra url template from a file
        /// </summary>
        /// <remarks>
        /// As each job has an extra information url, it is possible to use specific url for a job
        /// By providing the url as content in a file called "job.extra_info_url.template" under the job's data directory.
        /// Sample file content:
        /// /sb?jobName={jobName}&jobType={jobType}
        /// </remarks>
        private string LoadExtraInfoUrlTemplateFromFile(string jobsSpecificDataPath)
        {
            try
            {
                string jobExtraInfoUrlFilePath = GetJobExtraInfoUrlFilePath(jobsSpecificDataPath);
                if (FileSystemHelpers.FileExists(jobExtraInfoUrlFilePath))
                {
                    string jobExtraInfoUrlFileContent = FileSystemHelpers.ReadAllText(jobExtraInfoUrlFilePath);
                    jobExtraInfoUrlFileContent = jobExtraInfoUrlFileContent.Trim();
                    if (!String.IsNullOrEmpty(jobExtraInfoUrlFileContent))
                    {
                        return jobExtraInfoUrlFileContent.Split('\n')[0];
                    }
                }
            }
            catch (Exception ex)
            {
                TraceFactory.GetTracer().TraceError(ex);
            }

            return null;
        }

        protected abstract Uri BuildDefaultExtraInfoUrl(string jobName);

        protected string AppBaseUrlPrefix
        {
            get
            {
                if (_appBaseUrlPrefix == null)
                {
                    if (HttpContext.Current == null)
                    {
                        return null;
                    }

                    _appBaseUrlPrefix = HttpContext.Current.Request.Url.GetLeftPart(UriPartial.Authority);
                }

                return _appBaseUrlPrefix;
            }
        }

        private DirectoryInfoBase GetJobDirectory(string jobName)
        {
            string jobPath = Path.Combine(JobsBinariesPath, jobName);
            return FileSystemHelpers.DirectoryInfoFromDirectoryName(jobPath);
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