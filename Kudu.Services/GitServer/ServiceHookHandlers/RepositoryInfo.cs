namespace Kudu.Services.GitServer.ServiceHookHandlers
{
    public class RepositoryInfo
    {
        public string RepositoryUrl { get; set; }
        public bool IsPrivate { get; set; }
        public bool UseSSH { get; set; }
        public string Host { get; set; }
        public string OldRef { get; set; }
        public string NewRef { get; set; }
        public string Deployer { get; set; }
        public IServiceHookHandler Handler { get; set; }
    }
}