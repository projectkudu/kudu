using System;
using System.ComponentModel.DataAnnotations;
using Kudu.Client.Models;

namespace Kudu.Web.Models {
    public class Application : IApplication {
        [Key]
        public string Name { get; set; }
        public string Slug { get; set; }
        public string SiteName { get; set; }
        public string ServiceAppName { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
        public string DeveloperSiteUrl { get; set; }
        public int DeveloperSiteState { get; set; }
        public Guid UniqueId { get; set; }
        public DateTime Created { get; set; }
    }
}