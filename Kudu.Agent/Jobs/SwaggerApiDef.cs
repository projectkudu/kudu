using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Kudu.Contracts.Jobs;

namespace Kudu.Services.Jobs
{
    public class SwaggerApiDef
    {
        [JsonPropertyName("swagger")]
        public string Swagger { get; set; }

        [JsonPropertyName("info")]
        public SwaggerApiDefInfo Info { get; private set; }

        [JsonPropertyName("host")]
        public string Host { get; private set; }

        [JsonPropertyName("schemes")]
        public List<String> Schemes { get; private set; }

        [JsonPropertyName("paths")]
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
        [JsonPropertyName("version")]
        public string Version { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        public SwaggerApiDefInfo()
        {
            Version = "v1";
            Title = "WebJobs";
        }
    }

    public class PathItem
    {
        [JsonPropertyName("post")]
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
        [JsonPropertyName("deprecated")]
        public bool Deprecated { set; get; }

        [JsonPropertyName("operationId")]
        public string OperationId { set; get; }

        [JsonPropertyName("consumes")]
        public IEnumerable<String> Consumes { set; get; }

        [JsonPropertyName("produces")]
        public IEnumerable<String> Produces { set; get; }

        [JsonPropertyName("responses")]
        public IDictionary<string, Response> Responses { set; get; }

        [JsonPropertyName("parameters")]
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
        [JsonPropertyName("name")]
        public string Name { set; get; }

        [JsonPropertyName("in")]
        public string Input { set; get; }

        [JsonPropertyName("description")]
        public string Description { set; get; }

        [JsonPropertyName("required")]
        public bool Required { set; get; }

        [JsonPropertyName("type")]
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
        [JsonPropertyName("description")]
        public string Description { get; set; }
    }
}
