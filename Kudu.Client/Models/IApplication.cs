namespace Kudu.Client.Models {
    public interface IApplication {
        string Name { get; }
        string ServiceUrl { get; }
        string SiteUrl { get; }
    }
}
