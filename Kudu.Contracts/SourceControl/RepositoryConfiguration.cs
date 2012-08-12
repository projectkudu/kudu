namespace Kudu.Contracts.SourceControl
{
    public class RepositoryConfiguration
    {
        public string Username { get; set; }
        public string Email { get; set; }

        // REVIEW: This really doesn't belong on "RepositoryConfiguration"
        // we could rename it or create a new type
        public int TraceLevel { get; set; }
    }
}
