using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using Kudu.Contracts.Diagnostics;
using Kudu.Core;

namespace Kudu.Services.Diagnostics
{
    public interface IApplicationLogsReader
    {
        IEnumerable<ApplicationLogEntry> GetRecentLogs(int top);
    }

    public class ApplicationLogsReader : IApplicationLogsReader
    {
        internal const int ReadBatchSize = 5;
        internal const int FileOpenLimit = 50;
        internal const string LogFilenamePattern = "*-*.txt";
        internal const string LogErrorsSuffix = "-logging-errors.txt";
        internal const string LogEntryRegexPattern = @"^(\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d)\s+PID\[(\d+)\]\s(Warning|Information|Error)\s+(.*)";
        
        private readonly string _logsFolder;
        private readonly LogFileFinder _logFinder;            

        public ApplicationLogsReader(IFileSystem fileSystem, IEnvironment environment)
        {              
            _logsFolder = fileSystem.Path.Combine(environment.RootPath, Constants.ApplicationLogFilesPath);
            _logFinder = new LogFileFinder(fileSystem, this._logsFolder);
        }       

        public IEnumerable<ApplicationLogEntry> GetRecentLogs(int top)
        {
            if (top <= 0)
            {
                throw new ArgumentOutOfRangeException("top", "Number of logs to return must be a positive integer.");
            }

            List<ResumableLogFileReader> logReaders = null;
            try
            {
                logReaders = _logFinder
                    .FindLogFiles()                    
                    .Select(f => new ResumableLogFileReader(f))                    
                    .ToList();

                List<ApplicationLogEntry> logs = new List<ApplicationLogEntry>();
                while (logs.Count < top && logReaders.Count > 0)
                {
                    ResumableLogFileReader reader = logReaders.OrderByDescending(f => f.LastTime).First();
                    var logBatch = reader.ReadNextBatch(ReadBatchSize);
                    if (logBatch.Count > 0)
                    {
                        logs.AddRange(logBatch);
                    }
                    else
                    {
                        reader.Dispose();
                        logReaders.Remove(reader);
                    }
                }

                return logs
                    .OrderByDescending(e => e.TimeStamp)
                    .Take(top);
            }
            finally
            {
                if (logReaders != null)
                {
                    foreach (var reader in logReaders)
                    {
                        reader.Dispose();
                    }
                }
            }
        }
        

        internal class LogFileFinder
        {            
            private readonly DirectoryInfoBase _directory;
            private readonly LogFileAccessStats _stats;

            internal HashSet<string> ExcludedFiles { get; private set; }
            internal HashSet<string> IncludedFiles { get; private set; }

            public LogFileFinder(IFileSystem fileSystem, string logsFolder, LogFileAccessStats stats = null)
            {
                ExcludedFiles = new HashSet<string>();
                IncludedFiles = new HashSet<string>();                
                _directory = fileSystem.DirectoryInfo.FromDirectoryName(logsFolder);
                _stats = stats;                
            }

            public IEnumerable<FileInfoBase> FindLogFiles()
            {
                if (!_directory.Exists)
                {
                    return new List<FileInfoBase>();
                }

                var files = _directory.GetFiles(LogFilenamePattern, SearchOption.TopDirectoryOnly)
                    .Where(f => !f.FullName.EndsWith(LogErrorsSuffix, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .Take(FileOpenLimit)
                    .ToList();

                var fileNames = files.Select(f => f.FullName);

                // Remove any deleted files
                var newIncludedFiles = new HashSet<string>(IncludedFiles.Intersect(fileNames));
                var newExcludedFiles = new HashSet<string>(ExcludedFiles.Intersect(fileNames));                
                
                foreach (var file in files)
                {
                    if (newIncludedFiles.Contains(file.FullName) || newExcludedFiles.Contains(file.FullName))
                    {
                        continue;
                    }

                    var line = ReadFirstLine(file);
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        if (IsLineInExpectedFormat(line))
                        {
                            newIncludedFiles.Add(file.FullName);
                        }
                        else
                        {
                            newExcludedFiles.Add(file.FullName);
                        }
                    }
                }

                var results = files.Where(f => newIncludedFiles.Contains(f.FullName));
                IncludedFiles = newIncludedFiles;
                ExcludedFiles = newExcludedFiles;
                return results;          
            }

            private static bool IsLineInExpectedFormat(string line)
            {
                return Regex.Match(line, LogEntryRegexPattern, RegexOptions.IgnoreCase).Success;
            }

            private string ReadFirstLine(FileInfoBase fileInfo)
            {
                using (var reader = fileInfo.OpenText())
                {
                    if (_stats != null)
                    {
                        _stats.IncrementOpenTextCount(fileInfo.Name);
                    }

                    return reader.ReadLine();
                }
            }
        }

        internal class ResumableLogFileReader : IDisposable
        {
            private IEnumerable<string> _lines;
            private IEnumerator<string> _enumerator;
            private readonly FileInfoBase _fileInfo;
            private readonly LogFileAccessStats _stats;
            private bool _disposed;            

            public DateTimeOffset LastTime { get; private set; }            

            public ResumableLogFileReader(FileInfoBase fileInfo, LogFileAccessStats stats = null)
            {
                LastTime = fileInfo.LastWriteTimeUtc;
                _fileInfo = fileInfo;
                _stats = stats;
                _lines = CreateReverseLineReader();
            }

            internal ResumableLogFileReader(DateTimeOffset lastWrite, IEnumerable<string> lines)
            {
                LastTime = lastWrite;
                _lines = lines;
            }

            public List<ApplicationLogEntry> ReadNextBatch(int batchSize)
            {
                if (_disposed)
                {
                    throw new ObjectDisposedException("_enumerator");
                }

                ApplicationLogEntry currentEntry = new ApplicationLogEntry();
                List<ApplicationLogEntry> entries = new List<ApplicationLogEntry>();

                if (_enumerator == null)
                {
                    _enumerator = _lines.GetEnumerator();                    
                }

                while (entries.Count < batchSize && _enumerator.MoveNext())
                {
                    string line = _enumerator.Current;
                    var match = Regex.Match(line, ApplicationLogsReader.LogEntryRegexPattern, RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        currentEntry.TimeStamp = DateTimeOffset.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);
                        currentEntry.PID = int.Parse(match.Groups[2].Value);
                        currentEntry.Level = match.Groups[3].Value;                        
                        currentEntry.AddMessageLine(match.Groups[4].Value);
                        entries.Add(currentEntry);
                        currentEntry = new ApplicationLogEntry();
                    }
                    else
                    {
                        currentEntry.AddMessageLine(line);
                    }
                }

                if (entries.Count > 0)
                {
                    this.LastTime = entries.Last().TimeStamp;
                }

                return entries;
            }

            public void Dispose()
            {
                if (!_disposed)
                {
                    if (_enumerator != null)
                    {
                        _enumerator.Dispose();
                    }
                    _disposed = true;
                }
            }

            private IEnumerable<string> CreateReverseLineReader()
            {
                return new MiscUtil.IO.ReverseLineReader(() =>
                {
                    var stream = _fileInfo.OpenRead();
                    if (_stats != null)
                    {
                        _stats.IncrementOpenReadCount(_fileInfo.Name);
                    }
                    return stream;
                });
            }
        }

        internal class LogFileAccessStats
        {
            private readonly Dictionary<string, SingleFileStats> _stats;

            public LogFileAccessStats()
            {
                _stats = new Dictionary<string, SingleFileStats>();
            }

            public int GetOpenReadCount(string file)
            {
                EnsureKey(file);
                return _stats[file].OpenReadCount;
            }

            public int GetOpenTextCount(string file)
            {
                EnsureKey(file);
                return _stats[file].OpenTextCount;
            }

            public void IncrementOpenReadCount(string file)
            {
                EnsureKey(file);
                _stats[file].OpenReadCount++;                
            }

            public void IncrementOpenTextCount(string file)
            {
                EnsureKey(file);
                _stats[file].OpenTextCount++;
            }

            private void EnsureKey(string file)
            {
                if (!_stats.ContainsKey(file))
                {
                    _stats[file] = new SingleFileStats();
                }
            }

            private class SingleFileStats
            {
                public int OpenTextCount;
                public int OpenReadCount;
            }
        }
    }
}
