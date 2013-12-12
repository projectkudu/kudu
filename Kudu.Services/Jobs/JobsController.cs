using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using Kudu.Contracts.Jobs;
using Kudu.Core.Hooks;
using Kudu.Core.Jobs;

namespace Kudu.Services.Jobs
{
    public class JobsController : ApiController
    {
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly IContinuousJobsManager _continuousJobsManager;

        public JobsController(ITriggeredJobsManager triggeredJobsManager, IContinuousJobsManager continuousJobsManager)
        {
            _triggeredJobsManager = triggeredJobsManager;
            _continuousJobsManager = continuousJobsManager;
        }

        [HttpGet]
        public HttpResponseMessage ListAllJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs();
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs();

            var allJobs = triggeredJobs.OfType<JobBase>().Union(continuousJobs);

            return Request.CreateResponse(HttpStatusCode.OK, allJobs);
        }

        [HttpGet]
        public HttpResponseMessage ListContinuousJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs();

            return Request.CreateResponse(HttpStatusCode.OK, continuousJobs);
        }

        [HttpGet]
        public HttpResponseMessage GetContinuousJob(string jobName)
        {
            ContinuousJob continuousJob = _continuousJobsManager.GetJob(jobName);
            if (continuousJob != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, continuousJob);
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpPost]
        public HttpResponseMessage EnableContinuousJob(string jobName)
        {
            try
            {
                _continuousJobsManager.EnableJob(jobName);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        [HttpPost]
        public HttpResponseMessage DisableContinuousJob(string jobName)
        {
            try
            {
                _continuousJobsManager.DisableJob(jobName);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        [HttpPost]
        public HttpResponseMessage SetContinuousJobSingleton(string jobName, bool isSingleton)
        {
            try
            {
                _continuousJobsManager.SetSingleton(jobName, isSingleton);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        [HttpGet]
        public HttpResponseMessage ListTriggeredJobs()
        {
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs();

            return Request.CreateResponse(HttpStatusCode.OK, triggeredJobs);
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJob(string jobName)
        {
            TriggeredJob triggeredJob = _triggeredJobsManager.GetJob(jobName);
            if (triggeredJob != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, triggeredJob);
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobHistory(string jobName)
        {
            TriggeredJobHistory triggeredJobHistory = _triggeredJobsManager.GetJobHistory(jobName);
            if (triggeredJobHistory != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, triggeredJobHistory);
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobRun(string jobName, string runId)
        {
            TriggeredJobRun triggeredJobRun = _triggeredJobsManager.GetJobRun(jobName, runId);
            if (triggeredJobRun != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, triggeredJobRun);
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpPost]
        public HttpResponseMessage InvokeTriggeredJob(string jobName)
        {
            try
            {
                _triggeredJobsManager.InvokeTriggeredJob(jobName);
                return Request.CreateResponse(HttpStatusCode.Accepted);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            catch (ConflictException)
            {
                return Request.CreateResponse(HttpStatusCode.Conflict);
            }
        }
    }
}