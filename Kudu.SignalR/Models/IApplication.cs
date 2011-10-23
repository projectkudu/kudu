namespace Kudu.SignalR.Models {
    public interface IApplication {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }
        string DeveloperSiteUrl { get; }
    }
}
