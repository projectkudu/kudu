using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.ServiceModel;
using System.ServiceModel.Web;

namespace Kudu.Services.Performance
{
    [ServiceContract]
    public class ProfilingService
    {
        private readonly string _profilePath;
        public ProfilingService(string profilePath)
        {
            _profilePath = profilePath;
        }

        [WebGet(UriTemplate = "")]
        public HttpResponseMessage GetProfileData()
        {
            var response = new HttpResponseMessage();
            if (!File.Exists(_profilePath))
            {
                // Not profiling information available yet
                response.StatusCode = HttpStatusCode.NoContent;
                return response;
            }

            var fs = new FileStream(_profilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            response.Content = new StreamContent(fs);
            response.Content.Headers.ContentType = new MediaTypeHeaderValue("text/xml");
            return response;
        }

        [WebGet(UriTemplate = "delete")]
        public HttpResponseMessage Delete()
        {
            var response = new HttpResponseMessage();
            if (!File.Exists(_profilePath))
            {
                // Not profiling information available yet
                response.StatusCode = HttpStatusCode.NoContent;
                return response;
            }

            File.Delete(_profilePath);
            response.StatusCode = HttpStatusCode.OK;
            return response;
        }
    }
}
