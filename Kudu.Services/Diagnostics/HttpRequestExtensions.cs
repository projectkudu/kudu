using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using NuGet;
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Web;

namespace Kudu.Services.Diagnostics
{
    public static class HttpRequestExtensions
    {
        public static bool IsFunctionsPortalRequest(this HttpRequest request)
        {
            return request.Headers[Constants.FunctionsPortal] != null;
        }

        public static HttpResponseMessage ForwardToContainer(string route, HttpRequestMessage request)
        {
            // Forward request to windows container
            // Get the container address and the port kudu agent port
            IDictionary environmentVariables = System.Environment.GetEnvironmentVariables();
            if (environmentVariables.Contains("KUDU_AGENT_HOST") && environmentVariables.Contains("KUDU_AGENT_PORT")
                && environmentVariables.Contains("KUDU_AGENT_USR") && environmentVariables.Contains("KUDU_AGENT_PWD"))
            {
                var containerAddress = environmentVariables["KUDU_AGENT_HOST"];
                var kuduContainerAgentPort = environmentVariables["KUDU_AGENT_PORT"];
                var kudu_agent_un = environmentVariables["KUDU_AGENT_USR"];
                var kudu_agent_pwd = environmentVariables["KUDU_AGENT_PWD"];

                string containerUrl = $"http://{containerAddress}:{kuduContainerAgentPort}" + route;

                // Create the client to forward the request to the container
                System.Net.Http.HttpClient client = new System.Net.Http.HttpClient();
                
                foreach (var header in request.Headers.ToList())
                {
                    // Ignore the host since we want it to be updated to be the container address
                    if (header.Key != "Host")
                    {
                        client.DefaultRequestHeaders.Add(header.Key, header.Value);
                    }
                }

                // Add authorization to the client request
                string authHeader = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{kudu_agent_un}:{kudu_agent_pwd}"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("BASIC", authHeader);

                HttpResponseMessage response;
                // Determine the request method to use
                if (request.Method == HttpMethod.Get)
                {
                    response = client.GetAsync(containerUrl).Result;
                }
                else if (request.Method == HttpMethod.Post)
                {
                    response = client.PostAsync(containerUrl, request.Content).Result;
                }
                else if (request.Method == HttpMethod.Put)
                {
                    response = client.PutAsync(containerUrl, request.Content).Result;
                }
                else if (request.Method == HttpMethod.Delete)
                {
                    response = client.DeleteAsync(containerUrl).Result;
                }
                else
                {
                    return new HttpResponseMessage(HttpStatusCode.NotFound);
                }

                return response;
            }
            else
            {
                // If these environment variables don't exist, then the container has not started yet
                HttpResponseMessage response = new HttpResponseMessage(HttpStatusCode.NotFound);
                response.Content = new StringContent("The container cannot be reached. Please ensure it is running.");
                return response;
            }
        }
    }
}
