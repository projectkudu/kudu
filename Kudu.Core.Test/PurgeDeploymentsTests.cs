using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Infrastructure;
using Kudu.Contracts.Tracing;
using Kudu.Core.Deployment;
using Kudu.Core.Tracing;
using Moq;
using Xunit;
using Xunit.Extensions;

namespace Kudu.FunctionalTests
{
    public class PurgeDeploymentsTests
    {
        private static int _id;
        private MethodInfo _method;

        public PurgeDeploymentsTests()
        {
            _method = typeof(DeploymentManager).GetMethod("PurgeDeployments",
                                                          BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.InvokeMethod,
                                                          null,
                                                          new[] { typeof(IEnumerable<DeployResult>) },
                                                          null);
        }

        [Theory]
        [PropertyData("DeployResults")]
        public void PurgeBasicTest(IEnumerable<DeployResult> actual)
        {
            // Mock
            var toDelete = actual.Where(r => r.Id.StartsWith("delete-"));
            var expect = actual.Where(r => !r.Id.StartsWith("delete-"));
            var manager = MockDeploymentManager(id =>
            {
                Assert.True(toDelete.Any(r => r.Id == id));
                toDelete = toDelete.Where(r => r.Id != id);
            });

            // Test
            var results = (IEnumerable<DeployResult>)_method.Invoke(manager, new object[] { actual });

            // Assert
            Assert.False(toDelete.Any());
            Assert.Equal(expect.Count(), results.Count());
            for (int i = 0; i < expect.Count(); ++i)
            {
                Assert.Equal(expect.ElementAt(i).Id, results.ElementAt(i).Id);
            }
        }

        public static IEnumerable<object[]> DeployResults
        {
            get
            {
                yield return new[] { GetEmpty() };

                yield return new[] { GetMultipleFails() };
                yield return new[] { GetMultiplePendings() };
                yield return new[] { GetMultipleTemporaryPendings() };

                yield return new[] { GetTooManyItems() };
                yield return new[] { GetTooManyItemsWithActive() };
                yield return new[] { GetTooManyItemsWithPendings() };
            }
        }

        private DeploymentManager MockDeploymentManager(Action<string> onDelete)
        {
            // Mock
            var status = new Mock<IDeploymentStatusManager>();
            var trace = new Mock<ITraceFactory>();
            var tracer = new Mock<ITracer>();
            var manager = new DeploymentManager(null, null, null, trace.Object, null, status.Object, null, null);

            // Setup
            status.Setup(s => s.ActiveDeploymentId)
                  .Returns("activeId");
            status.Setup(s => s.Delete(It.IsAny<string>()))
                  .Callback((string id) => onDelete(id));
            trace.Setup(t => t.GetTracer())
                 .Returns(tracer.Object);

            return manager;
        }

        private static IEnumerable<DeployResult> GetEmpty()
        {
            return Enumerable.Empty<DeployResult>();
        }

        private static IEnumerable<DeployResult> GetMultipleFails()
        {
            return new[]
            {
                CreateResult(DeployStatus.Failed, lastSuccess: false),
                CreateResult(DeployStatus.Failed, lastSuccess: true),
                CreateResult(DeployStatus.Failed, lastSuccess: false, toDelete: true),
                CreateResult(DeployStatus.Failed, lastSuccess: true),
            };
        }

        private static IEnumerable<DeployResult> GetMultiplePendings()
        {
            return new[]
            {
                CreateResult(DeployStatus.Pending, isTemp: true, toDelete: true),
                CreateResult(DeployStatus.Pending),
                CreateResult(DeployStatus.Pending),
            };
        }

        private static IEnumerable<DeployResult> GetMultipleTemporaryPendings()
        {
            return new[]
            {
                CreateResult(DeployStatus.Failed, lastSuccess: false),
                CreateResult(DeployStatus.Pending, lastSuccess: true),
                CreateResult(DeployStatus.Pending, isTemp: true, toDelete: true),
            };
        }

        private static IEnumerable<DeployResult> GetTooManyItems()
        {
            var list = CreateMaxSuccessItems();
            list.Insert(0, CreateResult(DeployStatus.Pending, isTemp: true));
            list.Add(CreateResult(DeployStatus.Success, toDelete: true));
            list.Add(CreateResult(DeployStatus.Success, toDelete: true));
            return list;
        }

        private static IEnumerable<DeployResult> GetTooManyItemsWithActive()
        {
            var list = CreateMaxSuccessItems();
            list.Insert(0, CreateResult(DeployStatus.Pending, isTemp: true));
            list.Add(CreateResult(DeployStatus.Success, toDelete: true));
            list.Add(CreateResult(DeployStatus.Success, isActive: true));
            list.Add(CreateResult(DeployStatus.Success, toDelete: true));
            return list;
        }

        private static IEnumerable<DeployResult> GetTooManyItemsWithPendings()
        {
            var list = CreateMaxSuccessItems();
            list.Insert(0, CreateResult(DeployStatus.Failed, isTemp: true));
            list.Add(CreateResult(DeployStatus.Success, toDelete: true));
            list.Add(CreateResult(DeployStatus.Pending, lastSuccess: true));
            list.Add(CreateResult(DeployStatus.Building, lastSuccess: true));
            list.Add(CreateResult(DeployStatus.Deploying, lastSuccess: true));
            list.Add(CreateResult(DeployStatus.Success, toDelete: true));
            return list;
        }

        private static List<DeployResult> CreateMaxSuccessItems()
        {
            var list = new List<DeployResult>();
            for (int i = 0; i < DeploymentManager.MaxSuccessDeploymentResults; ++i)
            {
                list.Add(CreateResult(i % 2 == 0 ? DeployStatus.Success : DeployStatus.Failed, lastSuccess: true));
            }
            return list;
        }

        private static DeployResult CreateResult(DeployStatus status,
                                                 bool? lastSuccess = null,
                                                 bool isActive = false,
                                                 bool isTemp = false,
                                                 bool toDelete = false)
        {
            var id = ++_id;
            DateTime? lastSuccessTime = null;
            if (lastSuccess.HasValue)
            {
                if (lastSuccess.Value)
                {
                    lastSuccessTime = DateTime.Now;
                }
            }
            else
            {
                if (status == DeployStatus.Success)
                {
                    lastSuccessTime = DateTime.Now;
                }
            }

            var resultId = id.ToString();
            if (isActive)
            {
                resultId = "activeId";
            }
            else if (toDelete)
            {
                resultId = "delete-" + id;
            }

            return new DeployResult
            {
                Id = resultId,
                Status = status,
                LastSuccessEndTime = lastSuccessTime,
                IsTemporary = isTemp
            };
        }
    }
}