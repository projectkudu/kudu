using System;
using System.Collections.Generic;
using System.Reflection;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    public static class KuduXunitExtensions
    {
        public static void SetOutput(this TestResultMessage message, string output)
        {
            var prop = typeof(TestResultMessage).GetProperty("Output", BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.NonPublic | BindingFlags.Public);
            prop.SetValue(message, output);
        }

        public static void SetMessageBus(this XunitTestRunner runner, IMessageBus messageBus)
        {
            var prop = typeof(TestRunner<IXunitTestCase>).GetProperty("MessageBus", BindingFlags.Instance | BindingFlags.SetProperty | BindingFlags.NonPublic | BindingFlags.Public);
            prop.SetValue(runner, messageBus);
        }

        public static ITest GetTest(this XunitTestRunner runner)
        {
            var prop = typeof(TestRunner<IXunitTestCase>).GetProperty("Test", BindingFlags.Instance | BindingFlags.GetProperty | BindingFlags.NonPublic | BindingFlags.Public);
            return (ITest)prop.GetValue(runner);
        }

        public static RunSummary RunTest_DataDiscoveryException(this KuduXunitTheoryTestCaseRunner runner)
        {
            var method = typeof(XunitTheoryTestCaseRunner).GetMethod("RunTest_DataDiscoveryException", BindingFlags.Instance | BindingFlags.InvokeMethod | BindingFlags.NonPublic | BindingFlags.Public);
            return (RunSummary)method.Invoke((XunitTheoryTestCaseRunner)runner, new object[0]);
        }

        public static Exception GetDataDiscoveryException(this KuduXunitTheoryTestCaseRunner runner)
        {
            var field = typeof(XunitTheoryTestCaseRunner).GetField("dataDiscoveryException", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Public);
            return (Exception)field.GetValue((XunitTheoryTestCaseRunner)runner);
        }

        public static List<XunitTestRunner> GetTestRunners(this KuduXunitTheoryTestCaseRunner runner)
        {
            var field = typeof(XunitTheoryTestCaseRunner).GetField("testRunners", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Public);
            return (List<XunitTestRunner>)field.GetValue((XunitTheoryTestCaseRunner)runner);
        }

        public static List<IDisposable> GetToDispose(this KuduXunitTheoryTestCaseRunner runner)
        {
            var field = typeof(XunitTheoryTestCaseRunner).GetField("toDispose", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Public);
            return (List<IDisposable>)field.GetValue((XunitTheoryTestCaseRunner)runner);
        }

        public static ExceptionAggregator GetCleanupAggregator(this KuduXunitTheoryTestCaseRunner runner)
        {
            var field = typeof(XunitTheoryTestCaseRunner).GetField("cleanupAggregator", BindingFlags.Instance | BindingFlags.GetField | BindingFlags.NonPublic | BindingFlags.Public);
            return (ExceptionAggregator)field.GetValue((XunitTheoryTestCaseRunner)runner);
        }
    }
}
