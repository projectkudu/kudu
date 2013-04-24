using System;
using System.IO;
using System.IO.Abstractions;
using System.Xml.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Core.Deployment;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Deployment
{
    /// <summary>
    /// An xml file that keeps track of deployment status
    /// </summary>
    public class DeploymentStatusFile : IDeploymentStatusFile
    {
        private const string StatusFile = "status.xml";

        private readonly string _activeFile;
        private readonly string _statusFile;
        private readonly IFileSystem _fileSystem;
        private readonly IOperationLock _statusLock;

        private DeploymentStatusFile(string id, IEnvironment environment, IFileSystem fileSystem, IOperationLock statusLock, XDocument document = null)
        {
            _activeFile = Path.Combine(environment.DeploymentsPath, Constants.ActiveDeploymentFile);
            _statusFile = Path.Combine(environment.DeploymentsPath, id, StatusFile);
            _fileSystem = fileSystem;
            _statusLock = statusLock;

            Id = id;

            if (document != null)
            {
                Initialize(document);
            }
        }

        public static DeploymentStatusFile Create(string id, IFileSystem fileSystem, IEnvironment environment, IOperationLock statusLock)
        {
            string path = Path.Combine(environment.DeploymentsPath, id);

            FileSystemHelpers.EnsureDirectory(fileSystem, path);

            DateTime utcNow = DateTime.UtcNow;
            return new DeploymentStatusFile(id, environment, fileSystem, statusLock)
            {
                StartTime = utcNow,
                ReceivedTime = utcNow
            };
        }

        public static DeploymentStatusFile Open(string id, IFileSystem fileSystem, IEnvironment environment, IOperationLock statusLock)
        {
            return statusLock.LockOperation(() =>
            {
                string path = Path.Combine(environment.DeploymentsPath, id, StatusFile);
                XDocument document = null;

                if (!fileSystem.File.Exists(path))
                {
                    return null;
                }

                using (var stream = fileSystem.File.OpenRead(path))
                {
                    document = XDocument.Load(stream);
                }

                return new DeploymentStatusFile(id, environment, fileSystem, statusLock, document);
            }, DeploymentStatusManager.LockTimeout);
        }

        private void Initialize(XDocument document)
        {
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

            bool isTemporary = false;
            string isTemporaryValue = GetOptionalElementValue(document.Root, "is_temp");

            if (!String.IsNullOrEmpty(isTemporaryValue))
            {
                Boolean.TryParse(isTemporaryValue, out isTemporary);
            }

            Id = document.Root.Element("id").Value;
            Author = GetOptionalElementValue(document.Root, "author");
            Deployer = GetOptionalElementValue(document.Root, "deployer");
            AuthorEmail = GetOptionalElementValue(document.Root, "authorEmail");
            Message = GetOptionalElementValue(document.Root, "message");
            Progress = GetOptionalElementValue(document.Root, "progress");
            Status = status;
            StatusText = document.Root.Element("statusText").Value;
            StartTime = ParseDateTime(startTimeValue).Value;
            ReceivedTime = ParseDateTime(receivedTimeValue).Value;
            EndTime = ParseDateTime(endTimeValue);
            LastSuccessEndTime = ParseDateTime(lastSuccessEndTimeValue);
            Complete = complete;
            IsTemporary = isTemporary;
        }

        public string Id { get; set; }
        public DeployStatus Status { get; set; }
        public string StatusText { get; set; }
        public string AuthorEmail { get; set; }
        public string Author { get; set; }
        public string Message { get; set; }
        public string Progress { get; set; }
        public string Deployer { get; set; }
        public DateTime ReceivedTime { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public DateTime? LastSuccessEndTime { get; set; }
        public bool Complete { get; set; }
        public bool IsTemporary { get; set; }

        public void Save()
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
                    new XElement("progress", Progress),
                    new XElement("status", Status),
                    new XElement("statusText", StatusText),
                    new XElement("lastSuccessEndTime", LastSuccessEndTime),
                    new XElement("receivedTime", ReceivedTime),
                    new XElement("startTime", StartTime),
                    new XElement("endTime", EndTime),
                    new XElement("complete", Complete.ToString()),
                    new XElement("is_temp", IsTemporary.ToString())
                ));

            _statusLock.LockOperation(() =>
            {
                using (Stream stream = _fileSystem.File.Create(_statusFile))
                {
                    document.Save(stream);
                }

                // Used for ETAG
                if (_fileSystem.File.Exists(_activeFile))
                {
                    _fileSystem.File.SetLastWriteTimeUtc(_activeFile, DateTime.UtcNow);
                }
                else
                {
                    _fileSystem.File.WriteAllText(_activeFile, String.Empty);
                }
            }, DeploymentStatusManager.LockTimeout);
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
            return !String.IsNullOrEmpty(value) ? DateTime.Parse(value).ToUniversalTime() : (DateTime?)null;
        }
    }
}
