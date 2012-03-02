﻿using System.ComponentModel.DataAnnotations;

namespace Kudu.Web.Models
{
    public class KuduApplication : IApplication
    {
        [Key]
        public string Name { get; set; }
        public string ServiceUrl { get; set; }
        public string SiteUrl { get; set; }
        public string Username { get; set; }
    }
}