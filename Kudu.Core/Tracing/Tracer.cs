using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Xml.Linq;
using Kudu.Contracts.Tracing;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Tracing
{
    public class Tracer : ITracer
    {
        // TODO: Make this configurable
        private const int MaxLogEntries = 1000;

        private readonly Stack<TraceStep> _currentSteps = new Stack<TraceStep>();
        private readonly List<TraceStep> _steps = new List<TraceStep>();
        private readonly Stack<XElement> _elements = new Stack<XElement>();

        private readonly string _path;
        private readonly IFileSystem _fileSystem;

        private const string TraceRoot = "trace";

        private static readonly ConcurrentDictionary<string, object> _pathLocks = new ConcurrentDictionary<string, object>();

        public Tracer(string path)
            : this(new FileSystem(), path)
        {

        }

        public Tracer(IFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path;

            if (!_pathLocks.ContainsKey(path))
            {
                _pathLocks.TryAdd(path, new object());
            }
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
                                                      new XAttribute("date", DateTime.Now.ToString("MM/dd H:mm:ss")));

            foreach (var pair in attributes)
            {
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
                    else
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

        private void Save(XElement stepElement)
        {
            lock (_pathLocks[_path])
            {
                XDocument document = GetDocument();

                // Make sure the size of the log doesn't go over the limit
                EnsureSize(document);

                document.Root.Add(stepElement);
                document.Save(_path);
            }
        }

        public void Trace(string value, IDictionary<string, string> attributes)
        {
            // Add a fake step
            using (Step(value, attributes)) { }
        }

        private void EnsureSize(XDocument document)
        {
            try
            {
                var entries = document.Root.Elements().ToList();

                if (entries.Count <= MaxLogEntries)
                {
                    return;
                }

                // Amount of entries we have to trim. It should be 1 all of the time
                // but just in case something went wrong, we're going to try to fix it this time around
                int trim = entries.Count - MaxLogEntries;

                foreach (var e in entries.Take(trim))
                {
                    e.Remove();
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
            if (!_fileSystem.File.Exists(_path))
            {
                _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_path));
                return CreateDocumentRoot();
            }

            try
            {
                XDocument document;
                using (var stream = _fileSystem.File.OpenRead(_path))
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
