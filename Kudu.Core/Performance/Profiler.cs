using System;
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

        public Profiler(string path)
            : this(new FileSystem(), path)
        {

        }

        public Profiler(IFileSystem fileSystem, string path)
        {
            _fileSystem = fileSystem;
            _path = path;
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
            XDocument document = GetDocument();

            var newStep = new ProfilerStep(title);
            var newStepElement = new XElement("step", new XAttribute("title", title));

            if (_currentSteps.Count == 0)
            {
                // Add a new top level step
                _steps.Add(newStep);

                document.Root.Add(newStepElement);
                newStepElement.Add(new XAttribute("date", DateTime.Now));
            }

            _currentSteps.Push(newStep);
            _elements.Push(newStepElement);

            // Start profiling
            newStep.Start();

            return new DisposableAction(() =>
            {
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
                    document.Save(_path);
                }

                if (_currentSteps.Count > 0)
                {
                    ProfilerStep parent = _currentSteps.Peek();
                    parent.Children.Add(current);
                }                
            });
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
