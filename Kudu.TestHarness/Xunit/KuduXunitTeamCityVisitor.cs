using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Xml.Linq;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Xunit;
using Xunit.Abstractions;
using Xunit.Runner.MSBuild;

namespace Kudu.TestHarness.Xunit
{
    // copied from https://github.com/xunit/xunit/blob/master/src/xunit.runner.msbuild/Visitors/TeamCityVisitor.cs
    // customized for kudu
    // 1. use different test name if rerun test
    // 2. dump output for skipped test
    // 3. suppress collection output
    public class KuduXunitTeamCityVisitor : MSBuildVisitor
    {
        readonly TeamCityDisplayNameFormatter displayNameFormatter;
        readonly ConcurrentDictionary<string, string> flowMappings = new ConcurrentDictionary<string, string>();
        readonly Dictionary<string, string> displayNames = new Dictionary<string, string>();
        readonly Dictionary<string, StringBuilder> buildLogs = new Dictionary<string, StringBuilder>();
        readonly Func<string, string> flowIdMapper;

        public KuduXunitTeamCityVisitor(TaskLoggingHelper log,
                                        XElement assemblyElement,
                                        Func<bool> cancelThunk,
                                        Func<string, string> flowIdMapper = null,
                                        TeamCityDisplayNameFormatter displayNameFormatter = null)
            : base(log, assemblyElement, cancelThunk)
        {
            this.flowIdMapper = flowIdMapper ?? (_ => Guid.NewGuid().ToString("N"));
            this.displayNameFormatter = displayNameFormatter ?? new TeamCityDisplayNameFormatter();
        }

        void LogFinish(ITestResultMessage testResult)
        {
            var displayName = TeamCityEscape(GetDisplayName(testResult.Test));
            if (!String.IsNullOrEmpty(testResult.Output))
            {
                LogMessage(displayName, false, "##teamcity[testStdOut name='{0}' out='{1}' flowId='{2}']",
                               displayName,
                               TeamCityEscape(testResult.Output),
                               ToFlowId(testResult.TestCollection.DisplayName));
            }
            else if (testResult is ITestSkipped)
            {
                var skipped = (ITestSkipped)testResult;
                LogMessage(displayName, false, "##teamcity[testStdOut name='{0}' out='{1}' flowId='{2}']",
                               displayName,
                               TeamCityEscape(skipped.Reason),
                               ToFlowId(testResult.TestCollection.DisplayName));
            }

            LogMessage(displayName, true, "##teamcity[testFinished name='{0}' duration='{1}' flowId='{2}']",
                           displayName,
                           (int)(testResult.ExecutionTime * 1000M),
                           ToFlowId(testResult.TestCollection.DisplayName));
        }

        protected override bool Visit(ITestCollectionFinished testCollectionFinished)
        {
            // Base class does computation of results, so call it first.
            var result = base.Visit(testCollectionFinished);

            // to avoid mangling log
            //var displayName = TeamCityEscape(displayNameFormatter.DisplayName(testCollectionFinished.TestCollection));
            //LogMessage(displayName, true, "##teamcity[testSuiteFinished name='{0}' flowId='{1}']",
            //               displayName,
            //               ToFlowId(testCollectionFinished.TestCollection.DisplayName));

            return result;
        }

        protected override bool Visit(ITestCollectionStarting testCollectionStarting)
        {
            // to avoid mangling log
            //var displayName = TeamCityEscape(displayNameFormatter.DisplayName(testCollectionStarting.TestCollection));
            //LogMessage(displayName, false, "##teamcity[testSuiteStarted name='{0}' flowId='{1}']",
            //               displayName,
            //               ToFlowId(testCollectionStarting.TestCollection.DisplayName));

            return base.Visit(testCollectionStarting);
        }

        protected override bool Visit(ITestFailed testFailed)
        {
            var displayName = TeamCityEscape(GetDisplayName(testFailed.Test));
            LogMessage(displayName, false, "##teamcity[testFailed name='{0}' details='{1}|r|n{2}' flowId='{3}']",
                           displayName,
                           TeamCityEscape(ExceptionUtility.CombineMessages(testFailed)),
                           TeamCityEscape(ExceptionUtility.CombineStackTraces(testFailed)),
                           ToFlowId(testFailed.TestCollection.DisplayName));

            LogFinish(testFailed);

            return base.Visit(testFailed);
        }

        protected override bool Visit(ITestPassed testPassed)
        {
            LogFinish(testPassed);

            return base.Visit(testPassed);
        }

        protected override bool Visit(ITestSkipped testSkipped)
        {
            var displayName = TeamCityEscape(GetDisplayName(testSkipped.Test));
            LogMessage(displayName, false, "##teamcity[testIgnored name='{0}' message='{1}' flowId='{2}']",
                           displayName,
                           TeamCityEscape(testSkipped.Reason),
                           ToFlowId(testSkipped.TestCollection.DisplayName));
            
            LogFinish(testSkipped);

            return base.Visit(testSkipped);
        }

        protected override bool Visit(ITestStarting testStarting)
        {
            var displayName = TeamCityEscape(GetDisplayName(testStarting.Test, createNew: true));
            LogMessage(displayName, false, "##teamcity[testStarted name='{0}' flowId='{1}']",
                           displayName,
                           ToFlowId(testStarting.TestCollection.DisplayName));

            return base.Visit(testStarting);
        }

        protected override bool Visit(IErrorMessage error)
        {
            WriteError("FATAL", error);

            return base.Visit(error);
        }

        protected override bool Visit(ITestAssemblyCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Assembly Cleanup Failure ({0})", cleanupFailure.TestAssembly.Assembly.AssemblyPath), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCaseCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Case Cleanup Failure ({0})", cleanupFailure.TestCase.DisplayName), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestClassCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Class Cleanup Failure ({0})", cleanupFailure.TestClass.Class.Name), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCollectionCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Collection Cleanup Failure ({0})", cleanupFailure.TestCollection.DisplayName), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Cleanup Failure ({0})", cleanupFailure.Test.DisplayName), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestMethodCleanupFailure cleanupFailure)
        {
            WriteError(String.Format("Test Method Cleanup Failure ({0})", cleanupFailure.TestMethod.Method.Name), cleanupFailure);

            return base.Visit(cleanupFailure);
        }

        protected override bool Visit(ITestAssemblyFinished assemblyFinished)
        {
            var result = base.Visit(assemblyFinished);

            // to avoid mangling log
            //var displayName = TeamCityEscape(assemblyFinished.TestAssembly.Assembly.Name);
            //LogMessage(displayName, true, "##teamcity[testSuiteFinished name='{0}' flowId='{1}']", 
            //                displayName, 
            //                ToFlowId(assemblyFinished.TestAssembly.Assembly.Name));

            return result;
        }

        protected override bool Visit(ITestAssemblyStarting assemblyStarting)
        {
            // to avoid mangling log
            //var displayName = TeamCityEscape(assemblyStarting.TestAssembly.Assembly.Name);
            //LogMessage(displayName, false, "##teamcity[testSuiteStarted name='{0}' flowId='{1}']", 
            //                displayName, 
            //                ToFlowId(assemblyStarting.TestAssembly.Assembly.Name));

            return base.Visit(assemblyStarting);
        }

        void WriteError(string messageType, IFailureInformation failureInfo)
        {
            var message = String.Format("[{0}] {1}: {2}", messageType, failureInfo.ExceptionTypes[0], ExceptionUtility.CombineMessages(failureInfo));
            var stack = ExceptionUtility.CombineStackTraces(failureInfo);

            Log.LogMessage(MessageImportance.High, "##teamcity[message text='{0}' errorDetails='{1}' status='ERROR']", TeamCityEscape(message), TeamCityEscape(stack));
        }

        static string TeamCityEscape(string value)
        {
            if (value == null)
                return String.Empty;

            return value.Replace("|", "||")
                        .Replace("'", "|'")
                        .Replace("\r", "|r")
                        .Replace("\n", "|n")
                        .Replace("]", "|]")
                        .Replace("[", "|[")
                        .Replace("\u0085", "|x")
                        .Replace("\u2028", "|l")
                        .Replace("\u2029", "|p");
        }

        string ToFlowId(string testCollectionName)
        {
            return flowMappings.GetOrAdd(testCollectionName, flowIdMapper);
        }

        void LogMessage(string displayName, bool flush, string format, params object[] args)
        {
            lock (buildLogs)
            {
                StringBuilder strb;
                if (!buildLogs.TryGetValue(displayName, out strb))
                {
                    strb = buildLogs[displayName] = new StringBuilder();
                }

                strb.AppendFormat(format, args);
                strb.AppendLine();

                if (flush)
                {
                    buildLogs.Remove(displayName);

                    Log.LogMessage(MessageImportance.High, strb.ToString());
                }
            }
        }

        string GetDisplayName(ITest test, bool createNew = false)
        {
            lock (displayNames)
            {
                var displayName = displayNameFormatter.DisplayName(test);
                if (!displayName.Contains("Kudu.FunctionalTests"))
                {
                    return displayName;
                }

                string name;
                if (displayNames.TryGetValue(displayName, out name))
                {
                    if (createNew)
                    {
                        name += "(#1)";
                        displayNames[displayName] = name;
                    }
                }
                else
                {
                    name = displayName;
                    displayNames[displayName] = name;
                }

                return name;
            }
        }
    }
}