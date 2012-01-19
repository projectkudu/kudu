using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Xml.Linq;
using Kudu.Contracts;
using Kudu.Core.Infrastructure;

namespace Kudu.Core.Performance
{
    public class Profiler : IProfiler
    {
        private readonly Stack<ProfilerStep> _currentSteps = new Stack<ProfilerStep>();
        private readonly List<ProfilerStep> _steps = new List<ProfilerStep>();
        private readonly Stack<XElement> _elements = new Stack<XElement>();

        private readonly string _path;
        private readonly IFileSystem _fileSystem;

        private static readonly ConcurrentDictionary<string, object> _pathLocks = new ConcurrentDictionary<string, object>();

        public Profiler(string path)
            : this(new FileSystem(), path)
        {

        }

        public Profiler(IFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path;

            if (!_pathLocks.ContainsKey(path))
            {
                _pathLocks.TryAdd(path, new object());
            }
        }

        public IEnumerable<ProfilerStep> Steps
        {
            get
            {
                return _steps.AsReadOnly();
            }
        }

        public IDisposable Step(string title)
        {
            var newStep = new ProfilerStep(title);
            var newStepElement = new XElement("step", new XAttribute("title", title),
                                                      new XAttribute("date", DateTime.Now.ToString("MM/dd H:mm:ss")));

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
                // If there's no steps then do nothing (guard against double dispose)
                if (_currentSteps.Count == 0)
                {
                    return;
                }

                // Stop the current step
                _currentSteps.Peek().Stop();

                ProfilerStep current = _currentSteps.Pop();
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
                    ProfilerStep parent = _currentSteps.Peek();
                    parent.Children.Add(current);
                }
            });
        }

        private void Save(XElement stepElement)
        {
            lock (_pathLocks[_path])
            {
                XDocument document = GetDocument();
                document.Root.Add(stepElement);
                document.Save(_path);
            }
        }

        private XDocument GetDocument()
        {
            if (!_fileSystem.File.Exists(_path))
            {
                _fileSystem.Directory.CreateDirectory(Path.GetDirectoryName(_path));
                return new XDocument(new XElement("profile"));
            }

            XDocument document;
            using (var stream = _fileSystem.File.OpenRead(_path))
            {
                document = XDocument.Load(stream);
            }

            return document;
        }
    }
}
