namespace Kudu.Core.Deployment
{
    public static class WellKnownEnvironmentVariables
    {
        public const string NuGetPackageRestoreKey = "EnableNuGetPackageRestore";
        public const string SourcePath = "DEPLOYMENT_SOURCE";
        public const string TargetPath = "DEPLOYMENT_TARGET";
        public const string WebRootPath = "WEBROOT_PATH";
        public const string MSBuildPath = "MSBUILD_PATH";
        public const string KuduSyncCommandKey = "KUDU_SYNC_CMD";
        public const string SelectNodeVersionCommandKey = "KUDU_SELECT_NODE_VERSION_CMD";
        public const string NpmJsPathKey = "NPM_JS_PATH";
        public const string BuildTempPath = "DEPLOYMENT_TEMP";
        public const string PreviousManifestPath = "PREVIOUS_MANIFEST_PATH";
        public const string NextManifestPath = "NEXT_MANIFEST_PATH";
        public const string InPlaceDeployment = "IN_PLACE_DEPLOYMENT";
    }
}
