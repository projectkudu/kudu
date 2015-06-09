using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Http.ModelBinding;
using Kudu.Contracts;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Tracing;
using Kudu.Core.Hooks;
using Kudu.Core.Infrastructure;
using Kudu.Core.Jobs;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;

namespace Kudu.Services.Jobs
{
    [ArmControllerConfiguration]
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

            return ListJobsResponseBasedOnETag(allJobs);
        }

        [HttpGet]
        public HttpResponseMessage ListContinuousJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs();

            return ListJobsResponseBasedOnETag(continuousJobs);
        }

        [HttpGet]
        public HttpResponseMessage GetContinuousJob(string jobName)
        {
            ContinuousJob continuousJob = _continuousJobsManager.GetJob(jobName);
            if (continuousJob != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(continuousJob, Request));
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

            return ListJobsResponseBasedOnETag(triggeredJobs);
        }

        [HttpGet]
        public HttpResponseMessage ListTriggeredJobsInSwaggerFormat()
        {
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs();

            SwaggerApiDef responseSwagger = new SwaggerApiDef(triggeredJobs);
            return Request.CreateResponse(responseSwagger);
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJob(string jobName)
        {
            TriggeredJob triggeredJob = _triggeredJobsManager.GetJob(jobName);
            if (triggeredJob != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(triggeredJob, Request));
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobHistory(string jobName)
        {
            string etag = GetRequestETag();

            string currentETag;
            TriggeredJobHistory history = _triggeredJobsManager.GetJobHistory(jobName, etag, out currentETag);

            if (history == null && currentETag == null)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }

            HttpResponseMessage response;
            if (etag == currentETag)
            {
                response = Request.CreateResponse(HttpStatusCode.NotModified);
            }
            else
            {
                object triggeredJobHistoryResponse =
                    history != null && ArmUtils.IsArmRequest(Request) ? ArmUtils.AddEnvelopeOnArmRequest(history.TriggeredJobRuns, Request) : history;

                response = Request.CreateResponse(HttpStatusCode.OK, triggeredJobHistoryResponse);
            }
            response.Headers.ETag = new EntityTagHeaderValue(currentETag);
            return response;
        }

        [HttpGet]
        public HttpResponseMessage GetTriggeredJobRun(string jobName, string runId)
        {
            TriggeredJobRun triggeredJobRun = _triggeredJobsManager.GetJobRun(jobName, runId);
            if (triggeredJobRun != null)
            {
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(triggeredJobRun, Request));
            }

            return Request.CreateResponse(HttpStatusCode.NotFound);
        }

        [HttpPost]
        public HttpResponseMessage InvokeTriggeredJob(string jobName, string arguments = null)
        {
            try
            {
                _triggeredJobsManager.InvokeTriggeredJob(jobName, arguments, "External - " + Request.Headers.UserAgent);
                return Request.CreateResponse(HttpStatusCode.Accepted);
            }
            catch (JobNotFoundException)
            {
                return Request.CreateResponse(HttpStatusCode.NotFound);
            }
            catch (ConflictException)
            {
                return CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_WebJobAlreadyRunning);
            }
            catch (WebJobsStoppedException)
            {
                return CreateErrorResponse(HttpStatusCode.Conflict, Resources.Error_WebJobsStopped);
            }
        }

        [HttpPut]
        public Task<HttpResponseMessage> CreateContinuousJob(string jobName)
        {
            return CreateJob(jobName, _continuousJobsManager);
        }

        [HttpPut]
        public HttpResponseMessage CreateContinuousJobArm(string jobName, ArmEntry<ContinuousJob> armContinuousJob)
        {
            return SetJobSettings(jobName, armContinuousJob.Properties.Settings, _continuousJobsManager);
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

        [HttpPut]
        public HttpResponseMessage CreateTriggeredJobArm(string jobName, ArmEntry<TriggeredJob> armTriggeredJob)
        {
            return SetJobSettings(jobName, armTriggeredJob.Properties.Settings, _triggeredJobsManager);
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

        private HttpResponseMessage ListJobsResponseBasedOnETag(IEnumerable<JobBase> jobs)
        {
            string etag = GetRequestETag();

            string currentETag = "\"" + HashHelpers.CalculateCompositeHash(jobs.ToArray()).ToString("x") + "\"";

            HttpResponseMessage response;
            if (etag == currentETag)
            {
                response = Request.CreateResponse(HttpStatusCode.NotModified);
            }
            else
            {
                response = Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(jobs, Request));
            }

            response.Headers.ETag = new EntityTagHeaderValue(currentETag);

            return response;
        }

        private string GetRequestETag()
        {
            return Request.Headers.IfNoneMatch.Select(header => header.Tag).FirstOrDefault();
        }

        private HttpResponseMessage RemoveJob<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            jobsManager.DeleteJob(jobName);
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
                jobsManager.DeleteJob(jobName);
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