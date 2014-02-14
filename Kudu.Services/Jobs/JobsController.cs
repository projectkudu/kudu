using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Tracing;
using Kudu.Core.Hooks;
using Kudu.Core.Jobs;

namespace Kudu.Services.Jobs
{
    public class JobsController : ApiController
    {
        private readonly ITracer _tracer;
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly IContinuousJobsManager _continuousJobsManager;

        public JobsController(ITriggeredJobsManager triggeredJobsManager, IContinuousJobsManager continuousJobsManager, ITracer tracer)
        {
            _triggeredJobsManager = triggeredJobsManager;
            _continuousJobsManager = continuousJobsManager;
            _tracer = tracer;
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

        [HttpGet]
        public HttpResponseMessage GetContinuousJobSettings(string jobName)
        {
            return GetJobSettings(jobName, _continuousJobsManager);
        }

        [HttpPut]
        public HttpResponseMessage SetContinuousJobSettings(string jobName, JobSettings jobSettings)
        {
            return SetJobSettings(jobName, jobSettings, _continuousJobsManager);
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

        [HttpPut]
        public Task<HttpResponseMessage> CreateContinuousJob(string jobName)
        {
            return CreateJob(jobName, _continuousJobsManager);
        }

        [HttpDelete]
        public HttpResponseMessage RemoveContinuousJob(string jobName)
        {
            return RemoveJob(jobName, _continuousJobsManager);
        }

        [HttpPut]
        public Task<HttpResponseMessage> CreateTriggeredJob(string jobName)
        {
            return CreateJob(jobName, _triggeredJobsManager);
        }

        [HttpDelete]
        public HttpResponseMessage RemoveTriggeredJob(string jobName)
        {
            return RemoveJob(jobName, _triggeredJobsManager);
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobSettings(string jobName)
        {
            return GetJobSettings(jobName, _triggeredJobsManager);
        }

        [HttpPut]
        public HttpResponseMessage SetTriggeredJobSettings(string jobName, JobSettings jobSettings)
        {
            return SetJobSettings(jobName, jobSettings, _triggeredJobsManager);
        }

        private HttpResponseMessage RemoveJob<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            jobsManager.DeleteJobAsync(jobName);
            return Request.CreateResponse(HttpStatusCode.OK);
        }

        private async Task<HttpResponseMessage> CreateJob<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            TJob job = null;
            string errorMessage = null;
            HttpStatusCode errorStatusCode;

            // Get the script file name from the content disposition header
            string scriptFileName = null;
            HttpContent content = Request.Content;
            if (content.Headers != null && content.Headers.ContentDisposition != null)
            {
                scriptFileName = content.Headers.ContentDisposition.FileName;
            }

            if (String.IsNullOrEmpty(scriptFileName))
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, Resources.Error_MissingContentDispositionHeader);
            }

            // Clean the file name from quotes and directories
            scriptFileName = scriptFileName.Trim('"');
            scriptFileName = Path.GetFileName(scriptFileName);

            Stream fileStream = await content.ReadAsStreamAsync();

            try
            {
                // Upload as a zip if content type is of a zipped file
                if (content.Headers.ContentType != null &&
                    String.Equals(content.Headers.ContentType.MediaType, "application/zip", StringComparison.OrdinalIgnoreCase))
                {
                    job = jobsManager.CreateOrReplaceJobFromZipStream(fileStream, jobName);
                }
                else
                {
                    job = jobsManager.CreateOrReplaceJobFromFileStream(fileStream, jobName, scriptFileName);
                }

                errorMessage = job.Error;
                errorStatusCode = HttpStatusCode.BadRequest;
            }
            catch (ConflictException)
            {
                return CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_WebJobAlreadyExists);
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                errorStatusCode = HttpStatusCode.InternalServerError;
                _tracer.TraceError(ex);
            }

            // On error, delete job (if exists)
            if (errorMessage != null)
            {
                await jobsManager.DeleteJobAsync(jobName);
                return CreateErrorResponse(errorStatusCode, errorMessage);
            }

            return Request.CreateResponse(job);
        }

        private HttpResponseMessage GetJobSettings<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            try
            {
                JobSettings jobSettings = jobsManager.GetJobSettings(jobName);
                return Request.CreateResponse(HttpStatusCode.OK, jobSettings);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage SetJobSettings<TJob>(string jobName, JobSettings jobSettings, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            try
            {
                jobsManager.SetJobSettings(jobName, jobSettings);
                return Request.CreateResponse(HttpStatusCode.OK);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
        }

        private HttpResponseMessage CreateErrorResponse(HttpStatusCode errorStatusCode, string errorMessage)
        {
            HttpResponseMessage response = Request.CreateResponse(errorStatusCode);
            response.Content = new StringContent(errorMessage);
            return response;
        }
    }
}