namespace Kudu.Contracts.Settings
{
    public static class SettingsKeys
    {
        /// <remarks>
        /// Legacy value that is superseded by DeploymentBranch
        /// </remarks>
        internal const string Branch = "branch";

        public const string DeploymentBranch = "deployment_branch";
        public const string BuildArgs = "SCM_BUILD_ARGS";
        public const string ScriptGeneratorArgs = "SCM_SCRIPT_GENERATOR_ARGS";
        public const string TraceLevel = "SCM_TRACE_LEVEL";
        public const string CommandIdleTimeout = "SCM_COMMAND_IDLE_TIMEOUT";
        public const string LogStreamTimeout = "SCM_LOGSTREAM_TIMEOUT";
        public const string GitUsername = "SCM_GIT_USERNAME";
        public const string GitEmail = "SCM_GIT_EMAIL";
        public const string ScmType = "ScmType";
        public const string UseShallowClone = "SCM_USE_SHALLOW_CLONE";
        public const string Command = "COMMAND";
        public const string Project = "PROJECT";
        public const string WorkerCommand = "WORKER_COMMAND";
        public const string TargetPath = "SCM_TARGET_PATH";
        public const string WebJobsRootPath = "WEBJOBS_ROOT_PATH";
        public const string RepositoryPath = "SCM_REPOSITORY_PATH";
        public const string NoRepository = "SCM_NO_REPOSITORY";
        public const string UseLibGit2SharpRepository = "SCM_USE_LIBGIT2SHARP_REPOSITORY";
        public const string SkipSslValidation = "SCM_SKIP_SSL_VALIDATION";
        // Free, Shared, Basic, Standard, Premium
        public const string WebSiteSku = "WEBSITE_SKU";
        public const string WebJobsRestartTime = "WEBJOBS_RESTART_TIME";
        public const string WebJobsIdleTimeoutInSeconds = "WEBJOBS_IDLE_TIMEOUT";
        public const string WebJobsHistorySize = "WEBJOBS_HISTORY_SIZE";
        public const string WebJobsStopped = "WEBJOBS_STOPPED";
        public const string WebJobsDisableSchedule = "WEBJOBS_DISABLE_SCHEDULE";
        public const string WebJobsLogTriggeredJobsToAppLogs = "WEBJOBS_LOG_TRIGGERED_JOBS_TO_APP_LOGS";
        public const string PostDeploymentActionsDirectory = "SCM_POST_DEPLOYMENT_ACTIONS_PATH";
        public const string DisableSubmodules = "SCM_DISABLE_SUBMODULES";
        public const string SiteExtensionsFeedUrl = "SCM_SITEEXTENSIONS_FEED_URL";
        public const string DisableDeploymentOnPush = "SCM_DISABLE_DEPLOY_ON_PUSH";
        public const string TouchWebConfigAfterDeployment = "SCM_TOUCH_WEBCONFIG_AFTER_DEPLOYMENT";
        public const string MaxRandomDelayInSec = "SCM_MAX_RANDOM_START_DELAY";
        public const string DockerCiEnabled = "DOCKER_ENABLE_CI";
        public const string LinuxRestartAppContainerAfterDeployment = "SCM_RESTART_APP_CONTAINER_AFTER_DEPLOYMENT";
        public const string DoBuildDuringDeployment = "SCM_DO_BUILD_DURING_DEPLOYMENT";
        public const string RunFromZipOld = "WEBSITE_RUN_FROM_ZIP";  // Old name, will eventually go away
        public const string RunFromZip = "WEBSITE_RUN_FROM_PACKAGE";
    }
}
