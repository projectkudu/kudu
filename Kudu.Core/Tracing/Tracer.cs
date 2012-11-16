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
        private readonly TraceLevel _level;

        private const string TraceRoot = "trace";

        private static readonly ConcurrentDictionary<string, object> _pathLocks = new ConcurrentDictionary<string, object>();

        public Tracer(string path, TraceLevel level)
            : this(new FileSystem(), path, level)
        {

        }

        public Tracer(IFileSystem fileSystem, string path, TraceLevel level)
        {
            _fileSystem = fileSystem;
            _path = path;
            _level = level;

            if (!_pathLocks.ContainsKey(path))
            {
                _pathLocks.TryAdd(path, new object());
            }
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
                                                      new XAttribute("date", DateTime.UtcNow.ToString("MM/dd H:mm:ss")));

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
                List<XElement> entries = document.Root.Elements().ToList();

                // Amount of entries we have to trim. It should be 1 all of the time
                // but just in case something went wrong, we're going to try to fix it this time around
                int trim = entries.Count - MaxLogEntries + 1;

                if (trim <= 0)
                {
                    return;
                }

                // Search for all skippable requests first
                var filteredEntries = entries.Take(MaxLogEntries / 2)
                                             .Where(Skippable)
                                             .ToList();

                // If we didn't find skippable entries just remove the oldest
                if (filteredEntries.Count == 0)
                {
                    // If there's none just use the full list
                    filteredEntries = entries;
                }

                foreach (var e in filteredEntries.Take(trim))
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

        private static bool Skippable(XElement e)
        {
            // Git requests have type git="true"
            bool isGit = e.Attribute("git") != null;

            // The only top level exe is kudu
            bool isKudu = e.Attribute("type") != null && e.Attribute("path") != null;

            return !isGit && !isKudu;
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
