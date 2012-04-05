namespace Kudu 
{
    public static class Constants 
    {
        public const string WebRoot = "wwwroot";
        public const string MappedLiveSite = "/_app";
        public const string MappedDevSite = "/_devapp";
        public const string RepositoryPath = "repository";

        public const string LockPath = "locks";
        public const string DeploymentLockFile = "deployments.lock";
        public const string InitLockFile = "init.lock";

        public const string DeploymentCachePath = "deployments";
        public const string TracePath = @"LogFiles\Git\trace";
        public const string DeploySettingsPath = "settings.xml";
        public const string TraceFile = "trace.xml";
    }
}