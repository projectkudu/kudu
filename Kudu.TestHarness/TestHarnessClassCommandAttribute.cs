using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using Xunit;
using Xunit.Sdk;

namespace Kudu.TestHarness
{
    public class TestHarnessClassCommandAttribute : RunWithAttribute
    {
        // Number of retries.  When test failed, it will be re-tried
        // based on this value.  Default 0 means never retry.
        public const int DefaultRetries = 0;

        // If retry successful, suppress any error and report as success result.
        // Default is false means always report as error.
        public const bool DefaultSuppressError = false;

        // Number of run iterations. this is used when you want to stress 
        // the test and running it in a loop.  Default is 1 means run only once.
        // Regardless of this settings, it will always stop on first failure.
        public const int DefaultRuns = 1;

        // Override via Test assembly's appSettings 
        public const string RunsSettingKey = "TestHarness.Runs";
        public const string RetriesSettingKey = "TestHarness.Retries";
        public const string SuppressErrorSettingKey = "TestHarness.SuppressError";

        public const string DividerBar = "====================================================================================";

        public TestHarnessClassCommandAttribute()
            : base(typeof(TestHarnessClassCommand))
        {
            Retries = DefaultRetries;
            SuppressError = DefaultSuppressError;
            Runs = DefaultRuns;
        }

        public int Retries
        {
            get;
            set;
        }

        public bool SuppressError
        {
            get;
            set;
        }

        public int Runs
        {
            get;
            set;
        }

        class TestHarnessClassCommand : TestClassCommand
        {
            public override IEnumerable<ITestCommand> EnumerateTestCommands(IMethodInfo testMethod)
            {
                var attribute = (TestHarnessClassCommandAttribute)this.TypeUnderTest.Type.GetCustomAttributes(typeof(TestHarnessClassCommandAttribute), true)[0];
                foreach (ITestCommand testCommand in base.EnumerateTestCommands(testMethod))
                {
                    if (testCommand is SkipCommand)
                    {
                        yield return testCommand;
                    }
                    else
                    {
                        yield return new TestHarnessCommand(attribute, testCommand, testMethod);
                    }
                }
            }

            class TestHarnessCommand : DelegatingTestCommand
            {
                private readonly int _runs;
                private readonly int _retries;
                private readonly bool _suppressError;
                private readonly IMethodInfo _testMethod;

                public TestHarnessCommand(TestHarnessClassCommandAttribute attribute, ITestCommand inner, IMethodInfo testMethod)
                    : base(inner)
                {
                    _testMethod = testMethod;

                    // prefer imperative over config settings
                    if (attribute.Runs != DefaultRuns || !Int32.TryParse(KuduUtils.GetTestSetting(RunsSettingKey), out _runs))
                    {
                        _runs = attribute.Runs;
                    }
                    _runs = Math.Max(DefaultRuns, _runs);

                    if (attribute.Retries != DefaultRetries || !Int32.TryParse(KuduUtils.GetTestSetting(RetriesSettingKey), out _retries))
                    {
                        _retries = attribute.Retries;
                    }
                    _retries = Math.Max(DefaultRetries, _retries);

                    if (attribute.SuppressError != DefaultSuppressError || !Boolean.TryParse(KuduUtils.GetTestSetting(SuppressErrorSettingKey), out _suppressError))
                    {
                        _suppressError = attribute.SuppressError;
                    }
                }

                public override MethodResult Execute(object testClass)
                {
                    MethodResult result = null;
                    for (int i = 0; i < _runs; ++i)
                    {
                        result = ExecuteInternal(testClass);
                        if (result is FailedResult)
                        {
                            break;
                        }

                        TraceIf(_runs > 1, "Run {0}/{1} passed successfully.", i + 1, _runs);
                    }

                    return result;
                }

                private MethodResult ExecuteInternal(object testClass)
                {
                    Exception exception = null;
                    MethodResult result = null;
                    MethodResult failedResult = null;
                    string retryMessage = null;
                    for (int i = 0; i < _retries + 1; ++i)
                    {
                        try
                        {
                            result = InnerCommand.Execute(testClass);
                            if (result is FailedResult)
                            {
                                TraceIf(_retries > 0, "Retry {0}/{1} failed.", i, _retries);
                                failedResult = (FailedResult)result;
                            }
                            else
                            {
                                retryMessage = String.Format("Retry {0}/{1} passed successfully.", i, _retries);
                                TraceIf(_retries > 0 && i > 0, retryMessage);
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            TraceIf(_retries > 0, "Retry {0}/{1} failed with {2}", i, _retries, ex);

                            // optimize to preserve stacktrace
                            if (i >= _retries)
                            {
                                throw;
                            }

                            if (exception == null)
                            {
                                exception = ex;
                            }
                        }
                    }

                    if (_suppressError && result is PassedResult)
                    {
                        return result;
                    }

                    if (exception != null)
                    {
                        if (String.IsNullOrEmpty(retryMessage))
                        {
                            ExceptionUtility.RethrowWithNoStackTraceLoss(exception);
                        }

                        if (failedResult == null)
                        {
                            failedResult = new FailedResult(_testMethod, new RetrySuccessfulException(retryMessage, exception), DisplayName);
                        }
                    }

                    return failedResult ?? result;
                }
            }
        }

        public static void TraceIf(bool condition, string messageFormat, params object[] args)
        {
            if (condition)
            {
                TestTracer.Trace(messageFormat, args);
                TestTracer.Trace("{0}{1}", TestHarnessClassCommandAttribute.DividerBar, System.Environment.NewLine);
            }
        }
    }

    [Serializable]
    public class RetrySuccessfulException : AssertException
    {
        public RetrySuccessfulException(string message, Exception innerException)
            : base(String.Format("{0}{1}{2}", message, System.Environment.NewLine, TestHarnessClassCommandAttribute.DividerBar), innerException)
        {
        }

        protected RetrySuccessfulException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}