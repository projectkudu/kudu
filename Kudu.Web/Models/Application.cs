using System;
using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class Application : IApplication
    {
        [Key]
        public string Name { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
        public DateTime Created { get; set; }
    }
}