using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class Tracer : ITracer
    {
        // TODO: Make this configurable
        public const int MaxLogEntries = 200;
        private readonly Stack<TraceStep> _currentSteps = new Stack<TraceStep>();
        private readonly List<TraceStep> _steps = new List<TraceStep>();
        private readonly Stack<XElement> _elements = new Stack<XElement>();

        private readonly string _path;
        private readonly TraceLevel _level;
        private readonly IOperationLock _traceLock;

        private const string TraceRoot = "trace";

        public Tracer(string path, TraceLevel level, IOperationLock traceLock)
        {
            _path = path;
            _level = level;
            _traceLock = traceLock;
        }

        public TraceLevel TraceLevel
        {
            get { return _level; }
        }

        public IEnumerable<TraceStep> Steps
        {
            get
            {
                return _steps.AsReadOnly();
            }
        }

        public IDisposable Step(string title, IDictionary<string, string> attributes)
        {
            var newStep = new TraceStep(title);
            var newStepElement = new XElement("step", new XAttribute("title", title),
                                                      new XAttribute("date", DateTime.UtcNow.ToString("MM/dd H:mm:ss")),
                                                      new XAttribute("instance", InstanceIdUtility.GetShortInstanceId()));

            foreach (var pair in attributes)
            {
                if (TraceExtensions.IsNonDisplayableAttribute(pair.Key)) continue;

                string safeValue = XmlUtility.Sanitize(pair.Value);
                newStepElement.Add(new XAttribute(pair.Key, safeValue));
            }

            if (_currentSteps.Count == 0)
            {
                // Add a new top level step
                _steps.Add(newStep);
            }

            _currentSteps.Push(newStep);
            _elements.Push(newStepElement);

            // Start profiling
            newStep.Start();

            return new DisposableAction(() =>
            {
                try
                {
                    // If there's no steps then do nothing (guard against double dispose)
                    if (_currentSteps.Count == 0)
                    {
                        return;
                    }

                    // Stop the current step
                    _currentSteps.Peek().Stop();

                    TraceStep current = _currentSteps.Pop();
                    XElement stepElement = _elements.Pop();

                    stepElement.Add(new XAttribute("elapsed", current.ElapsedMilliseconds));

                    if (_elements.Count > 0)
                    {
                        XElement parent = _elements.Peek();
                        parent.Add(stepElement);
                    }
                    else if (ShouldTrace(stepElement.LastNode as XElement))
                    {
                        // Add this element to the list
                        Save(stepElement);
                    }

                    if (_currentSteps.Count > 0)
                    {
                        TraceStep parent = _currentSteps.Peek();
                        parent.Children.Add(current);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                }
            });
        }

        private bool ShouldTrace(XElement element)
        {
            // if traceLevel is off, never trace anything
            // we should not be here in the first place
            if (this.TraceLevel <= TraceLevel.Off)
            {
                return false;
            }

            // if traceLevel is verbose or no last child, always trace
            // No last child indicates this is not a request-related trace
            if (this.TraceLevel >= TraceLevel.Verbose || element == null)
            {
                return true;
            }

            // only trace if not NotModified
            var statusCode = element.Attributes("statusCode").FirstOrDefault();
            if (statusCode == null || statusCode.Value != "304")
            {
                return true;
            }

            return false;
        }

        private void Save(XElement stepElement)
        {
            _traceLock.LockOperation(() =>
            {
                XDocument document = GetDocument();

                // Make sure the size of the log doesn't go over the limit
                EnsureSize(document);

                document.Root.Add(stepElement);

                using (var stream = FileSystemHelpers.OpenFile(_path, FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    document.Save(stream);
                }

            }, TimeSpan.FromMinutes(1));
        }

        public void Trace(string value, IDictionary<string, string> attributes)
        {
            // Add a fake step
            using (Step(value, attributes)) { }
        }

        private static void EnsureSize(XDocument document)
        {
            try
            {
                List<XElement> entries = document.Root.Elements().ToList();

                // Amount of entries we have to trim. It should be 1 all of the time
                // but just in case something went wrong, we're going to try to fix it this time around
                int trim = entries.Count - MaxLogEntries + 1;

                if (trim >= 0)
                {
                    foreach (var e in entries.Take(trim))
                    {
                        e.Remove();
                    }
                }
            }
            catch (Exception ex)
            {
                // Something went wrong so just continue
                Debug.WriteLine(ex.Message);
            }
        }

        private XDocument GetDocument()
        {
            if (!FileSystemHelpers.FileExists(_path))
            {
                FileSystemHelpers.CreateDirectory(Path.GetDirectoryName(_path));
                return CreateDocumentRoot();
            }

            try
            {
                XDocument document;
                using (var stream = FileSystemHelpers.OpenRead(_path))
                {
                    document = XDocument.Load(stream);
                }

                return document;
            }
            catch
            {
                // If the profile gets corrupted then delete it
                FileSystemHelpers.DeleteFileSafe(_path);

                // Return a new document
                return CreateDocumentRoot();
            }
        }

        private static XDocument CreateDocumentRoot()
        {
            return new XDocument(new XElement(TraceRoot));
        }
    }
}
