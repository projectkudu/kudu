using System;
using System.IO;
using System.IO.Abstractions;
using System.Xml.Linq;

namespace Kudu.Core.Deployment
{
    /// <summary>
    /// An xml file that keeps track of deployment status
    /// </summary>
    public class DeploymentStatusFile
    {
        private readonly string _path;

        private DeploymentStatusFile(string path)
        {
            _path = path;
        }

        public static DeploymentStatusFile Create(string path)
        {
            return new DeploymentStatusFile(path)
            {
                DeploymentStartTime = DateTime.Now,
                DeploymentReceivedTime = DateTime.Now
            };
        }

        public static DeploymentStatusFile Open(IFileSystem fileSystem, string path)
        {
            XDocument document;

            try
            {
                using (var stream = fileSystem.File.OpenRead(path))
                {
                    document = XDocument.Load(stream);
                }
            }
            catch
            {
                return null;
            }

            int percentage;
            Int32.TryParse(document.Root.Element("percentage").Value, out percentage);

            DeployStatus status;
            Enum.TryParse(document.Root.Element("status").Value, out status);

            string deploymentReceivedTimeValue = GetOptionalElementValue(document.Root, "deploymentReceivedTime");
            string deploymentEndTimeValue = GetOptionalElementValue(document.Root, "deploymentEndTime");
            string deploymentStartTimeValue = GetOptionalElementValue(document.Root, "deploymentStartTime");
            string lastSuccessTimeValue = GetOptionalElementValue(document.Root, "lastSuccessTime");

            bool deploymentComplete = false;
            string deploymentCompleteValue = GetOptionalElementValue(document.Root, "deploymentComplete");

            if (!String.IsNullOrEmpty(deploymentCompleteValue))
            {
                Boolean.TryParse(deploymentCompleteValue, out deploymentComplete);
            }

            var deploymentStartTime = DateTime.Parse(deploymentStartTimeValue);

            return new DeploymentStatusFile(path)
            {
                Id = document.Root.Element("id").Value,
                Author = GetOptionalElementValue(document.Root, "author"),
                AuthorEmail = GetOptionalElementValue(document.Root, "authorEmail"),
                Message = GetOptionalElementValue(document.Root, "message"),
                Status = status,
                StatusText = document.Root.Element("statusText").Value,
                Percentage = percentage,
                DeploymentStartTime = deploymentStartTime,
                DeploymentReceivedTime = String.IsNullOrEmpty(deploymentReceivedTimeValue) ? deploymentStartTime : DateTime.Parse(deploymentReceivedTimeValue),
                DeploymentEndTime = ParseDateTime(deploymentEndTimeValue),
                LastSuccessTime = ParseDateTime(lastSuccessTimeValue),
                Complete = deploymentComplete
            };
        }

        public string Id { get; set; }
        public DeployStatus Status { get; set; }
        public string StatusText { get; set; }
        public string AuthorEmail { get; set; }
        public string Author { get; set; }
        public string Message { get; set; }
        public int Percentage { get; set; }
        public DateTime DeploymentReceivedTime { get; set; }
        public DateTime DeploymentStartTime { get; set; }
        public DateTime? DeploymentEndTime { get; set; }
        public DateTime? LastSuccessTime { get; set; }
        public bool Complete { get; set; }

        public void Save(IFileSystem fileSystem)
        {
            if (String.IsNullOrEmpty(Id))
            {
                throw new InvalidOperationException();
            }

            var document = new XDocument(new XElement("deployment",
                    new XElement("id", Id),
                    new XElement("author", Author),
                    new XElement("authorEmail", AuthorEmail),
                    new XElement("message", Message),
                    new XElement("status", Status),
                    new XElement("statusText", StatusText),
                    new XElement("percentage", Percentage),
                    new XElement("lastSuccessTime", LastSuccessTime),
                    new XElement("deploymentReceivedTime", DeploymentReceivedTime),
                    new XElement("deploymentStartTime", DeploymentStartTime),
                    new XElement("deploymentEndTime", DeploymentEndTime),
                    new XElement("deploymentComplete", Complete.ToString())
                ));

            using (Stream stream = fileSystem.File.Create(_path))
            {
                document.Save(stream);
            }
        }

        private static string GetOptionalElementValue(XElement element, string localName, string namespaceName = null)
        {
            XElement child;
            if (String.IsNullOrEmpty(namespaceName))
            {
                child = element.Element(localName);
            }
            else
            {
                child = element.Element(XName.Get(localName, namespaceName));
            }
            return child != null ? child.Value : null;
        }
        
        private static DateTime? ParseDateTime(string value)
        {
            return !String.IsNullOrEmpty(value) ? DateTime.Parse(value) : (DateTime?)null;
        }
    }
}
