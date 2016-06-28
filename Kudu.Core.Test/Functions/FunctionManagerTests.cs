using System.Collections.Generic;
using Kudu.Core.Functions;
using Kudu.Core.Tracing;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Kudu.Core.Test.Functions
{
    public class FunctionManagerTests
    {
        [Theory]
        [InlineData(null, null, false)]
        [InlineData(false, null, false)]
        [InlineData(null, false, false)]
        [InlineData(false, false, false)]
        [InlineData(null, true, true)]
        [InlineData(true, null, true)]
        [InlineData(true, true, true)]
        public void FunctionIsDisabled_ReturnsExpectedResult(bool? disabledValue, bool? excludedValue, bool expected)
        {
            JObject functionConfig = new JObject();
            if (disabledValue.HasValue)
            {
                functionConfig.Add("disabled", disabledValue.Value);
            }
            if (excludedValue.HasValue)
            {
                functionConfig.Add("excluded", excludedValue.Value);
            }
            Assert.Equal(expected, FunctionManager.FunctionIsDisabled(functionConfig));
        }

        [Fact]
        public void GetTriggers_ReturnsExpectedResults()
        {
            FunctionEnvelope excludedFunction = new FunctionEnvelope
            {
                Name = "TestExcludedFunction",
                Config = new JObject
                {
                    { "excluded", true }
                }
            };
            FunctionEnvelope disabledFunction = new FunctionEnvelope
            {
                Name = "TestDisabledFunction",
                Config = new JObject
                {
                    { "disabled", true }
                }
            };

            FunctionEnvelope invalidFunction = new FunctionEnvelope
            {
                Name = "TestInvalidFunction",
                Config = new JObject
                {
                    { "bindings", "invalid" }
                }
            };

            var queueTriggerFunction = new FunctionEnvelope
            {
                Name = "TestQueueFunction",
                Config = new JObject
                {
                    { "bindings", new JArray
                        {
                            new JObject
                            {
                                { "type", "queueTrigger" },
                                { "direction", "in" },
                                { "queueName", "test" }
                            },
                            new JObject
                            {
                                { "type", "blob" },
                                { "direction", "out" },
                                { "path", "test" }
                            }
                        }
                    }
                }
            };

            List<FunctionEnvelope> functions = new List<FunctionEnvelope>
            {
                excludedFunction,
                disabledFunction,
                invalidFunction,
                queueTriggerFunction
            };

            var triggers = FunctionManager.GetTriggers(functions, NullTracer.Instance);

            Assert.Equal(1, triggers.Count);

            var trigger = triggers[0];
            Assert.Equal(queueTriggerFunction.Name, (string)trigger["functionName"]);
            Assert.Equal(queueTriggerFunction.Config["bindings"][0], trigger);
        }
    }
}
