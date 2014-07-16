using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Text.RegularExpressions;
using Kudu.Contracts.Diagnostics;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.Infrastructure;
using Kudu.Core.Tracing;

namespace Kudu.Services.Diagnostics
{
    public interface IApplicationLogsReader
    {
        IEnumerable<ApplicationLogEntry> GetRecentLogs(int top);
    }

    public class ApplicationLogsReader : IApplicationLogsReader
    {
        internal const int TopLimit = 1000;
        internal const int ReadBatchSize = 5;
        internal const int FileOpenLimit = 50;
        internal const string LogFilenamePattern = "*-*.txt";
        internal const string LogErrorsSuffix = "-logging-errors.txt";
        internal const string LogEntryRegexPattern = @"^(\d{4}-[01]\d-[0-3]\dT[0-2]\d:[0-5]\d:[0-5]\d)\s+PID\[(\d+)\]\s(Warning|Information|Error)\s+(.*)";
                
        private readonly LogFileFinder _logFinder;
        private readonly ITracer _tracer;

        public ApplicationLogsReader(IEnvironment environment, ITracer tracer)
        {
            _tracer = tracer;
            _logFinder = new LogFileFinder(environment, tracer);            
        }       

        public IEnumerable<ApplicationLogEntry> GetRecentLogs(int top)
        {
            if (top <= 0 || top > TopLimit)
            {
                throw new ArgumentOutOfRangeException("top", "Number of logs to return must be positive and not exceed 1000.");
            }

            List<ResumableLogFileReader> logReaders = null;
            try
            {
                logReaders = _logFinder
                    .FindLogFiles()                    
                    .Select(f => new ResumableLogFileReader(f, _tracer))                    
                    .ToList();                

                List<ApplicationLogEntry> logs = new List<ApplicationLogEntry>();
                while (logs.Count < top && logReaders.Count > 0)
                {
                    logReaders.Sort((reader1, reader2) => reader2.LastTime.CompareTo(reader1.LastTime));
                    ResumableLogFileReader reader = logReaders.First();
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
            private readonly ITracer _tracer;

            internal HashSet<string> ExcludedFiles { get; private set; }
            internal HashSet<string> IncludedFiles { get; private set; }

            public LogFileFinder(IEnvironment env, ITracer tracer, LogFileAccessStats stats = null)
            {
                ExcludedFiles = new HashSet<string>();
                IncludedFiles = new HashSet<string>();

                _stats = stats;
                _tracer = tracer;
                _directory = FileSystemHelpers.DirectoryInfoFromDirectoryName(env.ApplicationLogFilesPath);
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
                Stream stream = null;
                StreamReader reader = null;
                try
                {
                    stream = FileSystemHelpers.OpenFile(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    reader = new StreamReader(stream);

                    if (_stats != null)
                    {
                        _stats.IncrementOpenTextCount(fileInfo.Name);
                    }

                    return reader.ReadLine();
                }
                catch (UnauthorizedAccessException e)
                {
                    // If we are unable to open the file, silently skip it this time
                    _tracer.TraceError(e);
                    return null;
                }
                catch (IOException e)
                {
                    _tracer.TraceError(e);
                    return null;
                }
                finally
                {
                    if(reader != null)
                    {
                        reader.Dispose();
                        stream = null;
                    }

                    if (stream != null)
                    {
                        stream.Dispose();
                    }
                }
            }
        }

        internal class ResumableLogFileReader : IDisposable
        {
            private IEnumerable<string> _lines;
            private IEnumerator<string> _enumerator;
            private readonly LogFileAccessStats _stats;
            private readonly ITracer _tracer;
            private bool _disposed;            

            public DateTimeOffset LastTime { get; private set; }            

            public ResumableLogFileReader(FileInfoBase fileInfo, ITracer tracer, LogFileAccessStats stats = null)
            {              
                _stats = stats;
                _tracer = tracer;                
                _lines = CreateReverseLineReader(fileInfo);
                LastTime = fileInfo.LastWriteTimeUtc;
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

            private IEnumerable<string> CreateReverseLineReader(FileInfoBase fileInfo)
            {
                return new MiscUtil.IO.ReverseLineReader(() =>
                {
                    try
                    {
                        var stream = FileSystemHelpers.OpenFile(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        if (_stats != null)
                        {
                            _stats.IncrementOpenReadCount(fileInfo.Name);
                        }
                        return stream;
                    }
                    catch (UnauthorizedAccessException e)
                    {
                        // If we are unable to open the file, silently skip it this time
                        _tracer.TraceError(e);                        
                        return Stream.Null;
                    }
                    catch (IOException e)
                    {
                        _tracer.TraceError(e);
                        return Stream.Null;
                    }
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
