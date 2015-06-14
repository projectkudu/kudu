using System;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Xunit.Abstractions;

namespace Kudu.TestHarness
{
    public class TestContext
    {
        public const string DataSlotName = "TestContext";

        private readonly ITest _test;
        private readonly StringBuilder _traces;

        public TestContext(ITest testCase)
        {
            _test = testCase;
            _traces = new StringBuilder();
        }

        public ITest Test
        {
            get { return _test; }
        }

        public StringBuilder Traces
        {
            get { return _traces; }
        }

        public static TestContext Current
        {
            get { return (TestContext)CallContext.LogicalGetData(TestContext.DataSlotName); ; }
        }

        public static void InitializeContext(ITest test)
        {
            CallContext.LogicalSetData(TestContext.DataSlotName, new TestContext(test));
        }

        public static void FreeContext()
        {
            CallContext.FreeNamedDataSlot(TestContext.DataSlotName);
        }
    }
}