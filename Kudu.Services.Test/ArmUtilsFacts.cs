using System;
using System.Net.Http;
using Kudu.Contracts.Infrastructure;
using Kudu.Services.Arm;
using Xunit;

namespace Kudu.Services.Test
{
    public class ArmUtilsFacts
    {
        private const string _payloadId = "foo";
        private const string _payloadName = "bar";
        private const string _defaultArmRequest = "https://test.arm.com/subscriptions/b0019e1d-2829-4226-9356-4a57a4a5cc90/resourcegroups/MyRG/providers/Microsoft.Web/sites/MySite/extensions/SettingsAPISample/settings/bla?api-version=2015-01-01";
        private const string _defaultArmListRequest = "https://test.arm.com/subscriptions/b0019e1d-2829-4226-9356-4a57a4a5cc90/resourcegroups/MyRG/providers/Microsoft.Web/sites/MySite/extensions/SettingsAPISample/settings?api-version=2015-01-01";
        private const string _defaultArmId = "/subscriptions/b0019e1d-2829-4226-9356-4a57a4a5cc90/resourcegroups/MyRG/providers/Microsoft.Web/sites/MySite/extensions/SettingsAPISample/settings/bla";
        private const string _defaultArmName = "MySite/SettingsAPISample/bla";
        private const string _defaultArmType = "Microsoft.Web/sites/extensions/settings";
        private const string _defaultGeoLocation = "somewhere";

        [Fact]
        public void ArmUtilsShouldCreateArmEntity()
        {
            string[] urls = { _defaultArmRequest, _defaultArmRequest.Replace("/bla", "/bla/") };

            foreach (var url in urls)
            {
                var httpRequest = GetMockRequest(url: url);
                var result = ArmUtils.AddEnvelopeOnArmRequest<TestModel>(new TestModel { Id = _payloadId, Name = _payloadName }, httpRequest) as ArmEntry<TestModel>;
                Assert.NotNull(result);
                Assert.Equal(_defaultArmId, result.Id);
                Assert.Equal(_defaultArmName, result.Name);
                Assert.Equal(_defaultArmType, result.Type);
                Assert.Equal(_defaultGeoLocation, result.Location);
                Assert.Equal(_payloadId, result.Properties.Id);
                Assert.Equal(_payloadName, result.Properties.Name);
            }
        }

        [Fact]
        public void ArmUtilsShouldNotCreateArmEntity()
        {
            var httpRequest = GetMockRequest(isArmRequest: false);
            var result = ArmUtils.AddEnvelopeOnArmRequest<TestModel>(new TestModel { Id = _payloadId, Name = _payloadName }, httpRequest);
            Assert.Null(result as ArmEntry<TestModel>);
            var model = result as TestModel;
            Assert.NotNull(model);
            Assert.Equal(_payloadId, model.Id);
            Assert.Equal(_payloadName, model.Name);
        }

        [Fact]
        public void ArmUtilsShouldCreateArmEntityList()
        {
            string[] urls = { _defaultArmListRequest, _defaultArmListRequest.Replace("/settings", "/settings/") };

            foreach (var url in urls)
            {
                var httpRequest = GetMockRequest(url: url);
                TestModel[] modelList = {
                                        new TestModel { Id = _payloadId, Name = _payloadName },
                                        new TestModel { Id = _payloadId, Name = _payloadName },
                                    };

                var results = ArmUtils.AddEnvelopeOnArmRequest<TestModel>(modelList, httpRequest) as ArmListEntry<TestModel>;
                Assert.NotNull(results);
                foreach (var result in results.Value)
                {
                    Assert.Equal(_defaultArmId.Replace("/bla", "/" + _payloadName), result.Id);
                    Assert.Equal(_defaultArmName.Replace("/bla", "/" + _payloadName), result.Name);
                    Assert.Equal(_defaultArmType, result.Type);
                    Assert.Equal(_defaultGeoLocation, result.Location);
                    Assert.Equal(_payloadId, result.Properties.Id);
                    Assert.Equal(_payloadName, result.Properties.Name);
                }
            }
        }

        [Fact]
        public void ArmUtilsShouldNotCreateArmEntityList()
        {
            var httpRequest = GetMockRequest(url: _defaultArmListRequest, isArmRequest: false);
            TestModel[] modelList = {
                                        new TestModel { Id = _payloadId, Name = _payloadName },
                                        new TestModel { Id = _payloadId, Name = _payloadName },
                                    };

            var results = ArmUtils.AddEnvelopeOnArmRequest<TestModel>(modelList, httpRequest);
            Assert.Null(results as ArmListEntry<TestModel>);
            var models = results as TestModel[];
            foreach (var model in models)
            {
                Assert.Equal(_payloadId, model.Id);
                Assert.Equal(_payloadName, model.Name);
            }
        }

        private HttpRequestMessage GetMockRequest(string url = null, bool isArmRequest = true)
        {
            HttpRequestMessage request = new HttpRequestMessage();
            request.Headers.Referrer = !string.IsNullOrWhiteSpace(url) ?
                    new Uri(url) : new Uri(_defaultArmRequest);

            if (isArmRequest)
            {
                request.Headers.Add(ArmUtils.GeoLocationHeaderKey, _defaultGeoLocation);
            }

            return request;
        }

        private class TestModel : INamedObject
        {
            public string Id { get; set; }

            public string Name { get; set; }

        }
    }
}
