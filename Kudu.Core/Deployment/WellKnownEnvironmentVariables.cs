namespace Kudu.Core.Deployment
{
    internal static class WellKnownEnvironmentVariables
    {
        public const string NuGetPackageRestoreKey = "EnableNuGetPackageRestore";
        public const string SourcePath = "DEPLOYMENT_SOURCE";
        public const string TargetPath = "DEPLOYMENT_TARGET";
        public const string WebRootPath = "WEBROOT_PATH";
        public const string MSBuildPath = "MSBUILD_PATH";
        public const string KuduSyncCommandKey = "KUDU_SYNC_CMD";
        public const string NuGetExeCommandKey = "NUGET_EXE";
        public const string PostDeploymentActionsCommandKey = "POST_DEPLOYMENT_ACTION";
        public const string SelectNodeVersionCommandKey = "KUDU_SELECT_NODE_VERSION_CMD";
        public const string NpmJsPathKey = "NPM_JS_PATH";
        public const string BuildTempPath = "DEPLOYMENT_TEMP";
        public const string PreviousManifestPath = "PREVIOUS_MANIFEST_PATH";
        public const string NextManifestPath = "NEXT_MANIFEST_PATH";
        public const string InPlaceDeployment = "IN_PLACE_DEPLOYMENT";
        public const string PostDeploymentActionsDirectoryKey = "POST_DEPLOYMENT_ACTIONS_DIR";
        public const string WebJobDeployCommandKey = "WEB_JOB_DEPLOY_CMD";

        public const string JobRootPath = "JOB_ROOT";
        public const string JobName = "JOB_NAME";
        public const string JobType = "JOB_TYPE";
        public const string JobDataPath = "JOB_DATA_PATH";
        public const string JobExtraUrlPath = "JOB_EXTRA_INFO_URL_PATH";
        public const string JobRunId = "JOB_RUN_ID";

        public const string CommitId = "SCM_COMMIT_ID";
    }
}
