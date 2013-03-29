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
        public const string TraceLevel = "SCM_TRACE_LEVEL";
        public const string CommandIdleTimeout = "SCM_COMMAND_IDLE_TIMEOUT";
        public const string LogStreamTimeout = "SCM_LOGSTREAM_TIMEOUT";
        public const string SiteBuilderFactory = "SCM_SITE_BUILDER_FACTORY";
        public const string GitUsername = "SCM_GIT_USERNAME";
        public const string GitEmail = "SCM_GIT_EMAIL";
        public const string DeploymentFile = "SCM_DEPLOYMENT_FILE";
        public const string ScmType = "ScmType";
        public const string UseShallowClone = "SCM_USE_SHALLOW_CLONE";
    }
}
