using System;
using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Xunit.Sdk;

namespace Kudu.TestHarness.Xunit
{
    class KuduXunitTestFrameworkDiscoverer : XunitTestFrameworkDiscoverer
    {

        public KuduXunitTestFrameworkDiscoverer(IAssemblyInfo assemblyInfo,
                                                ISourceInformationProvider sourceProvider,
                                                IMessageSink diagnosticMessageSink,
                                                IXunitTestCollectionFactory collectionFactory = null)
            : base(assemblyInfo, sourceProvider, diagnosticMessageSink, collectionFactory)
        {
        }

        // src\xunit.execution\Sdk\Frameworks\XunitTestFrameworkDiscoverer.cs
        protected override bool FindTestsForMethod(ITestMethod testMethod, bool includeSourceInformation, IMessageBus messageBus, ITestFrameworkDiscoveryOptions discoveryOptions)
        {
            var factAttribute = testMethod.Method.GetCustomAttributes(typeof(FactAttribute)).FirstOrDefault();
            if (factAttribute == null)
                return true;

            var testCaseDiscovererAttribute = factAttribute.GetCustomAttributes(typeof(XunitTestCaseDiscovererAttribute)).FirstOrDefault();
            if (testCaseDiscovererAttribute == null)
                return true;

            var args = testCaseDiscovererAttribute.GetConstructorArguments().Cast<string>().ToList();
            Type discovererType = typeof(FactDiscoverer).Assembly.GetType(args[0]);
            if (discovererType == null)
                return true;

            var discoverer = GetDiscoverer(discovererType);
            if (discoverer == null)
                return true;

            var methodDisplay = discoveryOptions.MethodDisplayOrDefault();
            var testAttribute = testMethod.TestClass.Class.GetCustomAttributes(typeof(KuduXunitTestClassAttribute)).FirstOrDefault();
            foreach (var testCase in discoverer.Discover(discoveryOptions, testMethod, factAttribute).OfType<XunitTestCase>())
            {
                IXunitTestCase kuduTestCase;
                if (testCase is ExecutionErrorTestCase)
                    kuduTestCase = testCase;
                else if (testCase is XunitTheoryTestCase)
                    kuduTestCase = new KuduXunitTheoryTestCase(DiagnosticMessageSink, methodDisplay, testCase.TestMethod, testAttribute);
                else
                    kuduTestCase = new KuduXunitTestCase(DiagnosticMessageSink, methodDisplay, testCase.TestMethod, testCase.TestMethodArguments, testAttribute);

                if (!ReportDiscoveredTestCase(kuduTestCase, includeSourceInformation, messageBus))
                    return false;
            }

            return true;
        }
    }
}