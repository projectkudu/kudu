using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Kudu.Contracts.Jobs;
using Newtonsoft.Json;

namespace Kudu.Services.Jobs
{
    public class SwaggerApiDef
    {
        [JsonProperty(PropertyName = "swagger")]
        public string Swagger { get; set; }

        [JsonProperty(PropertyName = "info")]
        public SwaggerApiDefInfo Info { get; private set; }

        [JsonProperty(PropertyName = "host")]
        public string Host { get; private set; }

        [JsonProperty(PropertyName = "schemes")]
        public List<String> Schemes { get; private set; }

        [JsonProperty(PropertyName = "paths")]
        public Dictionary<String, PathItem> Paths { get; set; }

        public SwaggerApiDef(IEnumerable<JobBase> triggeredJobs)
        {
            Swagger = "2.0";
            Info = new SwaggerApiDefInfo();
            Host = "placeHolder";
            Schemes = new List<String> { "https" };
            Paths = new Dictionary<string, PathItem>();
            foreach (var triggeredJob in triggeredJobs)
            {
                Paths.Add(String.Format("/api/triggeredjobs/{0}/run", triggeredJob.Name), PathItem.GetDefaultPathItem(triggeredJob.Name));
            }
        }
    }

    public class SwaggerApiDefInfo
    {
        [JsonProperty(PropertyName = "version")]
        public string Version { get; set; }

        [JsonProperty(PropertyName = "title")]
        public string Title { get; set; }

        public SwaggerApiDefInfo()
        {
            Version = "v1";
            Title = "WebJobs";
        }
    }

    public class PathItem
    {
        [JsonProperty(PropertyName = "post")]
        public Operation Post { get; set; }

        public static PathItem GetDefaultPathItem(string id)
        {
            PathItem item = new PathItem();
            item.Post = Operation.GetDefaultOperation(id);
            return item;
        }
    }

    public class Operation
    {
        [JsonProperty(PropertyName = "deprecated")]
        public bool Deprecated { set; get; }

        [JsonProperty(PropertyName = "operationId")]
        public string OperationId { set; get; }

        [JsonProperty(PropertyName = "consumes")]
        public IEnumerable<String> Consumes { set; get; }

        [JsonProperty(PropertyName = "produces")]
        public IEnumerable<String> Produces { set; get; }

        [JsonProperty(PropertyName = "responses")]
        public IDictionary<string, Response> Responses { set; get; }

        [JsonProperty(PropertyName = "parameters")]
        public List<Parameter> Parameters { set; get; }

        public static Operation GetDefaultOperation(String id)
        {
            return new Operation
            {
                Deprecated = false,
                OperationId = id,
                Responses = new Dictionary<String, Response> { { "200", new Response { Description = "Success" } },
                                                               { "default", new Response { Description = "Success" } } },
                Consumes = new List<String>(),
                Produces = new List<String>(),
                Parameters = new List<Parameter> { Parameter.GetDefaultParameter() }
            };
        }
    }

    public class Parameter
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { set; get; }

        [JsonProperty(PropertyName = "in")]
        public string Input { set; get; }

        [JsonProperty(PropertyName = "description")]
        public string Description { set; get; }

        [JsonProperty(PropertyName = "required")]
        public bool Required { set; get; }

        [JsonProperty(PropertyName = "type")]
        public string Type { set; get; }

        public static Parameter GetDefaultParameter()
        {
            return new Parameter
            {
                Name = "arguments",
                Input = "query",
                Description = "Web Job Arguments",
                Required = false,
                Type = "string"
            };
        }

    }
    public class Response
    {
        [JsonProperty(PropertyName = "description")]
        public string Description { get; set; }
    }
}
