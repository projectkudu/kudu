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
                StartTime = DateTime.Now,
                ReceivedTime = DateTime.Now
            };
        }

        public static DeploymentStatusFile Open(IFileSystem fileSystem, string path)
        {
            XDocument document;

            try
            {
                if (!fileSystem.File.Exists(path))
                {
                    return null;
                }

                using (var stream = fileSystem.File.OpenRead(path))
                {
                    document = XDocument.Load(stream);
                }
            }
            catch
            {
                return null;
            }

            DeployStatus status;
            Enum.TryParse(document.Root.Element("status").Value, out status);

            string receivedTimeValue = GetOptionalElementValue(document.Root, "receivedTime");
            string endTimeValue = GetOptionalElementValue(document.Root, "endTime");
            string startTimeValue = GetOptionalElementValue(document.Root, "startTime");
            string lastSuccessEndTimeValue = GetOptionalElementValue(document.Root, "lastSuccessEndTime");

            bool complete = false;
            string completeValue = GetOptionalElementValue(document.Root, "complete");

            if (!String.IsNullOrEmpty(completeValue))
            {
                Boolean.TryParse(completeValue, out complete);
            }

            DateTime startTime;
            DateTime.TryParse(startTimeValue, out startTime);

            return new DeploymentStatusFile(path)
            {
                Id = document.Root.Element("id").Value,
                Author = GetOptionalElementValue(document.Root, "author"),
                Deployer = GetOptionalElementValue(document.Root, "deployer"),
                AuthorEmail = GetOptionalElementValue(document.Root, "authorEmail"),
                Message = GetOptionalElementValue(document.Root, "message"),
                Status = status,
                StatusText = document.Root.Element("statusText").Value,
                StartTime = startTime,
                ReceivedTime = String.IsNullOrEmpty(receivedTimeValue) ? startTime : DateTime.Parse(receivedTimeValue),
                EndTime = ParseDateTime(endTimeValue),
                LastSuccessEndTime = ParseDateTime(lastSuccessEndTimeValue),
                Complete = complete
            };
        }

        public string Id { get; set; }
        public DeployStatus Status { get; set; }
        public string StatusText { get; set; }
        public string AuthorEmail { get; set; }
        public string Author { get; set; }
        public string Message { get; set; }
        public string Deployer { get; set; }
        public DateTime ReceivedTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? LastSuccessEndTime { get; set; }
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
                    new XElement("deployer", Deployer),
                    new XElement("authorEmail", AuthorEmail),
                    new XElement("message", Message),
                    new XElement("status", Status),
                    new XElement("statusText", StatusText),
                    new XElement("lastSuccessEndTime", LastSuccessEndTime),
                    new XElement("receivedTime", ReceivedTime),
                    new XElement("startTime", StartTime),
                    new XElement("endTime", EndTime),
                    new XElement("complete", Complete.ToString())
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
