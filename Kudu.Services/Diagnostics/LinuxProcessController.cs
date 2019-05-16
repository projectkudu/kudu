using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace Kudu.Services.Performance
{
    // This is a placeholder for future process API functionality on Linux,
    // the implementation of which will differ from Windows enough that it warrants
    // a separate controller class. For now this returns 400s for all routes.

    public class LinuxProcessController : ApiController
    {
        private const string ERRORMSG = "Not supported on Linux";

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage GetThread(int processId, int threadId)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage GetAllThreads(int id)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage GetModule(int id, string baseAddress)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage GetAllModules(int id)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage GetAllProcesses(bool allUsers = false)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage GetProcess(int id)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpDelete]
        public HttpResponseMessage KillProcess(int id)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage MiniDump(int id, int dumpType = 0, string format = null)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpPost]
        public HttpResponseMessage StartProfileAsync(int id, bool iisProfiling = false)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }

        [SuppressMessage("Microsoft.Usage", "CA1801:ReviewUnusedParameters", Justification = "Parameters preserved for equivalent route binding")]
        [HttpGet]
        public HttpResponseMessage StopProfileAsync(int id)
        {
            return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ERRORMSG);
        }
    }
}