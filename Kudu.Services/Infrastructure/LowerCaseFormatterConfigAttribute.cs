using System;
using System.Net.Http.Formatting;
using System.Web.Http.Controllers;
using Newtonsoft.Json;

namespace Kudu.Services
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class LowerCaseFormatterConfigAttribute : Attribute, IControllerConfiguration
    {
        public void Initialize(HttpControllerSettings controllerSettings, HttpControllerDescriptor controllerDescriptor)
        {
            controllerSettings.Formatters.Clear();

            var settings = new JsonSerializerSettings
            {
                ContractResolver = new LowerCasePropertyNamesContractResolver()
            };
            controllerSettings.Formatters.Add(new JsonMediaTypeFormatter { SerializerSettings = settings });
        }
    }
}
