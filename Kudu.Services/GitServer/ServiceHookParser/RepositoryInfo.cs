namespace Kudu.Services.GitServer.ServiceHookParser
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
    }
}