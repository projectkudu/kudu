using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Kudu.Contracts.Infrastructure;
using Newtonsoft.Json;

namespace Kudu.Services.Arm
{
    public static class ArmUtils
    {
        public const string GeoLocationHeaderKey = "x-ms-geo-location";

        public static object AddEnvelopeOnArmRequest<T>(T namedObject, HttpRequestMessage request) where T : INamedObject
        {
            if (IsArmRequest(request))
            {
                return Create(namedObject, request);
            }

            return namedObject;
        }

        public static object AddEnvelopeOnArmRequest<T>(List<T> namedObjects, HttpRequestMessage request) where T : INamedObject
        {
            return AddEnvelopeOnArmRequest((IEnumerable<T>)namedObjects, request);
        }

        public static object AddEnvelopeOnArmRequest<T>(IEnumerable<T> namedObjects, HttpRequestMessage request) where T : INamedObject
        {
            if (IsArmRequest(request))
            {
                return Create(namedObjects, request);
            }

            return namedObjects;
        }

        public static bool IsArmRequest(HttpRequestMessage request)
        {
            return request != null &&
                   request.Headers != null &&
                   request.Headers.Contains(GeoLocationHeaderKey);
        }

        public static bool IsRbacContributorRequest(HttpRequestMessage request)
        {
            IEnumerable<string> headerValues;
            if (request.Headers.TryGetValues(Constants.RoleBasedContributorHeader, out headerValues))
            {
                return headerValues.FirstOrDefault() == "1";
            }

            return false;
        }

        public static bool IsLegacyAuthorizationSource(HttpRequestMessage request)
        {
            IEnumerable<string> headerValues;
            if (request.Headers.TryGetValues(Constants.ClientAuthorizationSourceHeader, out headerValues))
            {
                return string.Equals(headerValues.FirstOrDefault(), "legacy", StringComparison.OrdinalIgnoreCase);
            }

            return false;
        }

        private static ArmListEntry<T> Create<T>(IEnumerable<T> objects, HttpRequestMessage request) where T : INamedObject
        {
            return new ArmListEntry<T>
            {
                Value = objects.Select(entry => Create(entry, request, isChild: true))
            };
        }

        private static ArmEntry<T> Create<T>(T o, HttpRequestMessage request, bool isChild = false) where T : INamedObject
        {
            var armEntry = new ArmEntry<T>()
            {
                Properties = o
            };

            // In Azure ARM requests, the referrer is the current id
            Uri referrer = request.Headers.Referrer;
            armEntry.Id = referrer != null ? referrer.AbsolutePath : request.RequestUri.AbsolutePath;

            // If we're generating a child object, append the child name
            if (isChild)
            {
                if (!armEntry.Id.EndsWith("/", StringComparison.OrdinalIgnoreCase))
                {
                    armEntry.Id += '/';
                }

                armEntry.Id += ((INamedObject)o).Name;
            }

            armEntry.Id = armEntry.Id.TrimEnd('/');

            // The Type and Name properties use alternating token starting with 'Microsoft.Web/sites'
            // e.g. /subscriptions/b0019e1d-2829-4226-9356-4a57a4a5cc90/resourcegroups/MyRG/providers/Microsoft.Web/sites/MySite/extensions/SettingsAPISample/settings/foo1
            // Type: Microsoft.Web/sites/extensions/settings
            // Name: MySite/SettingsAPISample/foo1

            string[] idTokens = armEntry.Id.Split('/');
            if (idTokens.Length > 8 && idTokens[6] == "Microsoft.Web")
            {
                armEntry.Type = idTokens[6];

                for (int i = 7; i < idTokens.Length; i += 2)
                {
                    armEntry.Type += "/" + idTokens[i];
                }

                armEntry.Name = idTokens[8];
                for (int i = 10; i < idTokens.Length; i += 2)
                {
                    armEntry.Name += "/" + idTokens[i];
                }
            }

            IEnumerable<string> values;
            if (request.Headers.TryGetValues(GeoLocationHeaderKey, out values))
            {
                armEntry.Location = values.FirstOrDefault();
            }

            return armEntry;
        }

        public static HttpResponseMessage CreateErrorResponse(HttpRequestMessage request, HttpStatusCode statusCode, Exception exception)
        {
            if (IsArmRequest(request))
            {
                return request.CreateResponse(statusCode, new ArmErrorInfo(statusCode, exception));
            }

            return request.CreateErrorResponse(statusCode, exception);
        }

        // this error will be deserialized conforming with ARM spec
        public class ArmErrorInfo
        {
            public ArmErrorInfo(HttpStatusCode code, Exception exception)
            {
                Error = new ArmErrorDetails
                {
                    Code = code.ToString(),
                    Message = exception.ToString()
                };
            }

            [JsonProperty(PropertyName = "error")]
            public ArmErrorDetails Error { get; private set; }

            public class ArmErrorDetails
            {
                [JsonProperty(PropertyName = "code")]
                public string Code { get; set; }

                [JsonProperty(PropertyName = "message")]
                public string Message { get; set; }
            }
        }
    }
}
