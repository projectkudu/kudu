using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using System.Xml.Serialization;
using NuGet.Client;
using NuGet.Client.VisualStudio;

namespace Kudu.Contracts.SiteExtensions
{
    public class NuGetValue
    {

        public NuGetValue(System.Xml.Linq.XElement xe)
        {
            Id = (xe.Elements().First(e => e.Name.LocalName == "Id")).Value;
            Version = (xe.Elements().First(e => e.Name.LocalName == "Version")).Value;
            Version = (xe.Elements().First(e => e.Name.LocalName == "Version")).Value;
            Title = (xe.Elements().First(e => e.Name.LocalName == "Title")).Value;
            Description = (xe.Elements().First(e => e.Name.LocalName == "Description")).Value;
            Authors = new List<string>((xe.Elements().First(e => e.Name.LocalName == "Authors")).Value.Split(','));
            IconUrl = (xe.Elements().First(e => e.Name.LocalName == "IconUrl")).Value;
            LicenseUrl = (xe.Elements().First(e => e.Name.LocalName == "LicenseUrl")).Value;
            DownloadCount = int.Parse((xe.Elements().First(e => e.Name.LocalName == "DownloadCount")).Value);
            Summary = (xe.Elements().First(e => e.Name.LocalName == "Summary")).Value;
            Published = DateTime.Parse((xe.Elements().First(e => e.Name.LocalName == "Published")).Value);
        }

        //[XmlAttribute("Id", Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices")]

        public string Id { get; set; }

        // [XmlAttribute("Version", Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices")]
        public string Version { get; set; }

        //[XmlAttribute("Version", Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices")]
        public string Description { get; set; }

        // [XmlAttribute("Title", Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices")]
        public string Title { get; set; }

        // [XmlAttribute("Authors", Namespace = "http://schemas.microsoft.com/ado/2007/08/dataservices")]
        public List<string> Authors { get; set; }

        public string IconUrl { get; set; }

        public string LicenseUrl { get; set; }

        public string FeedUrl { get; set; }

        public string ProjectUrl { get; set; }

        public int DownloadCount { get; set; }

        public string Summary { get; set; }

        public DateTime Published { get; set; }
    }
}
