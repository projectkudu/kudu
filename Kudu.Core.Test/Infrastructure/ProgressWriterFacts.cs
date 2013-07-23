using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using Xunit;

namespace Kudu.Core.Infrastructure.Test
{
    /// <summary>
    /// Verifies that <see cref="ProgressWriter"/> produces expected result.
    /// </summary>
    [SuppressMessage("Microsoft.Design", "CA1001:TypesThatOwnDisposableFieldsShouldBeDisposable", Justification = "This is a test.")]
    public class ProgressWriterFacts : IDisposable
    {
        private const int MaxIterations = 100;

        private static readonly TimeSpan _idlingStart = TimeSpan.FromMilliseconds(10);
        private static readonly TimeSpan _idlingDelay = TimeSpan.FromMilliseconds(5);

        private readonly Random _random = new Random(0);

        private readonly StringWriter _output;
        private readonly StringWriter _error;

        private readonly ProgressWriter _progressWriter;
        private readonly ManualResetEventSlim _done;

        private System.Timers.Timer _timer;
        private int counter;

        public ProgressWriterFacts()
        {
            _output = new StringWriter();
            _error = new StringWriter();
            _progressWriter = new ProgressWriter(_output, _error, _idlingStart, _idlingDelay);
            _done = new ManualResetEventSlim(false);
        }

        [Fact]
        public void ProgressWriter_Generates_ExpectedOutput()
        {
            // Arrange
            int delay;
            GetNextDelay(out delay);

            // Act
            StartTimer(delay);

            _done.Wait();

            // Assert
            VerifyResult(_output.ToString());
        }

        [Fact]
        public async Task ProgressWriter_Dispose()
        {
            // Arrange
            int delay;
            GetNextDelay(out delay);

            // Act
            StartTimer(delay);

            await Task.Delay(501);

            _progressWriter.Dispose();
            _done.Wait();

            // Assert
            VerifyResult(_output.ToString());
        }

        private void StartTimer(int delay)
        {
            _timer = new System.Timers.Timer(delay);
            _timer.Elapsed += OutputWriter;
            _timer.Start();
        }

        private void OutputWriter(object sender, ElapsedEventArgs e)
        {
            _progressWriter.WriteOutLine("t");
            int delay;
            if (GetNextDelay(out delay))
            {
                _timer.Interval = delay;
            }
        }

        private bool GetNextDelay(out int delay)
        {
            if (counter++ < MaxIterations)
            {
                delay = _random.Next(1, 25);
                return true;
            }

            _done.Set();
            delay = -1;
            return false;
        }

        private void VerifyResult(string result)
        {
            // Make sure the last line always ends in \r\n (it could just be a "." as we don't know when we end writing).
            if (result.EndsWith("."))
            {
                result = result + "\r\n";
            }

            StringReader reader = new StringReader(result);
            List<string> lines = new List<string>();
            string rawLine;
            int lineCount = 0;
            while ((rawLine = reader.ReadLine()) != null)
            {
                lineCount++;

                // Verify that each line is terminated with a '\r\n'
                Assert.True('\r' == rawLine[rawLine.Length - 2], GetErrorMessage("Did not find \\r in line" + lineCount, rawLine, result));
                Assert.True('\n' == rawLine[rawLine.Length - 1], GetErrorMessage("Did not fine \\n in line" + lineCount, rawLine, result));
                string line = rawLine.Substring(0, rawLine.Length - 2);
                lines.Add(line);
            }

            // Verify that each line either contains a single 't' or N number of "."
            // Also verify that we get both kinds of lines
            int tCount = 0, dotCount = 0;
            foreach (string line in lines)
            {
                var distinct = line.Distinct();
                Assert.True(1 == distinct.Count(), GetErrorMessage("More than one type of character", line, result));
                if (line.Equals("t", StringComparison.Ordinal))
                {
                    tCount++;
                    Assert.True(1 == line.Length, GetErrorMessage("Length bigger than 1", line, result));
                }
                else if (line[line.Length - 1] == '.')
                {
                    dotCount++;
                }
                else
                {
                    Assert.False(false, GetErrorMessage("Unexpected content", line, result));
                }
            }

            Assert.True(tCount > 0, GetErrorMessage("No lines with 't' detected.", String.Empty, result));
            Assert.True(dotCount > 0, GetErrorMessage("No lines with one or more '.' detected.", String.Empty, result));
            Assert.True(lines.Count == tCount + dotCount, GetErrorMessage("Lines do not add up.", String.Empty, result));
        }

        private static string GetErrorMessage(string message, string error, string sequence)
        {
            return String.Format("{0}: Error: '{1}' in sequence '{2}'", message, Escape(error), Escape(sequence));
        }

        private static string Escape(string value)
        {
            return value.Replace("\r\n", "\\r\\n");
        }

        public void Dispose()
        {
            if (_timer != null)
            {
                _timer.Dispose();
            }

            _progressWriter.Dispose();
            _output.Dispose();
            _error.Dispose();
            _done.Dispose();
        }
    }
}