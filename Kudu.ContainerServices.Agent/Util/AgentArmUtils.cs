using Kudu.Contracts.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace Kudu.ContainerServices.Agent.Util
{
    // Copy of ArmUtils needed for the agent
    public class AgentArmUtils
    {

        public const string GeoLocationHeaderKey = "x-ms-geo-location";

        public static bool IsArmRequest(HttpRequest request)
        {
            return request != null &&
                   request.Headers != null &&
                   request.Headers.ContainsKey(GeoLocationHeaderKey);
        }

        private static ArmListEntry<T> Create<T>(IEnumerable<T> objects, HttpRequest request) where T : INamedObject
        {
            return new ArmListEntry<T>
            {
                Value = objects.Select(entry => Create(entry, request, isChild: true))
            };
        }

        private static ArmEntry<T> Create<T>(T o, HttpRequest request, bool isChild = false) where T : INamedObject
        {
            var armEntry = new ArmEntry<T>()
            {
                Properties = o
            };

            // In Azure ARM requests, the referrer is the current id
            Uri referrer = new Uri(request.Headers.Referer);

            armEntry.Id = referrer != null ? referrer.AbsolutePath : request.GetDisplayUrl();

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

            //IEnumerable<string> values;
            StringValues values;
            if (request.Headers.TryGetValue(GeoLocationHeaderKey, out values))
            {
                armEntry.Location = values.FirstOrDefault();
            }

            return armEntry;
        }

        public static object AddEnvelopeOnArmRequest<T>(T namedObject, HttpRequest request) where T : INamedObject
        {
            if (IsArmRequest(request))
            {
                return Create(namedObject, request);
            }

            return namedObject;
        }

        public static object AddEnvelopeOnArmRequest<T>(List<T> namedObjects, HttpRequest request) where T : INamedObject
        {
            return AddEnvelopeOnArmRequest((IEnumerable<T>)namedObjects, request);
        }

        public static object AddEnvelopeOnArmRequest<T>(IEnumerable<T> namedObjects, HttpRequest request) where T : INamedObject
        {
            if (IsArmRequest(request))
            {
                return Create(namedObjects, request);
            }

            return namedObjects;
        }
    }

    public class ArmListEntry<T> where T : INamedObject
    {
        [JsonPropertyName("value")]
        public IEnumerable<ArmEntry<T>> Value { get; set; }
    }

    public class ArmEntry<T> where T : INamedObject
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("properties")]
        public T Properties { get; set; }
    }
}
