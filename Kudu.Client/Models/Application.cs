namespace Kudu.Client.Models {
    public class Application : IApplication {
        public string Name { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
    }
}
