namespace Kudu.Contracts.Settings
{
    public static class SettingsKeys
    {
        /// <remarks>
        /// Legacy value that is superseded by DeploymentBranch
        /// </remarks>
        internal const string Branch = "branch";

        public const string DeploymentBranch = "deployment_branch";
        public const string BuildArgs = "build_args";
        public const string TraceLevel = "trace_level";
        public const string CommandIdleTimeout = "command_idle_timeout";
        public const string LogStreamTimeout = "logstream_timeout";
        public const string SiteBuilderFactory = "site_builder_factory";
        public const string GitUsername = "git.username";
        public const string GitEmail = "git.email";
        public const string ScmType = "ScmType";
    }
}
