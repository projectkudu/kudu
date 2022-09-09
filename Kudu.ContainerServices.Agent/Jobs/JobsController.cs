using Kudu.Contracts.Jobs;
using Kudu.Core.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using Kudu.Contracts;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Routing;
using Kudu.ContainerServices.Agent.Util;
using Microsoft.AspNetCore.Mvc.WebApiCompatShim;

namespace Kudu.ContainerServices.Agent.Jobs
{
    [ApiController]
    [Route("/webjobs")]
    public class JobsController : ControllerBase
    {
        //private readonly ITracer _tracer;
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly IContinuousJobsManager _continuousJobsManager;

        public static string TmpFolder = System.Environment.ExpandEnvironmentVariables(@"%WEBROOT_PATH%\data\Temp");

        public JobsController()
        {
            _triggeredJobsManager = Host._triggeredJobsManager;
            _continuousJobsManager = Host._continuousJobsManager;
        }

        [HttpGet]
        [HttpGet("listalljobs")]
        public IActionResult ListAllJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs(forceRefreshCache: false);
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs(forceRefreshCache: false);

            var allJobs = triggeredJobs.OfType<JobBase>().Union(continuousJobs);

            return ListJobsResponseBasedOnETag(allJobs);
        }

        [HttpGet("continuouswebjobs")]
        public IActionResult ListContinuousJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs(forceRefreshCache: false);

            return ListJobsResponseBasedOnETag(continuousJobs);
        }

        [HttpGet("continuouswebjobs/{jobName}")]
        public IActionResult GetContinuousJob(string jobName)
        {
            ContinuousJob continuousJob = _continuousJobsManager.GetJob(jobName);
            if (continuousJob != null)
            {
                return Ok(AgentArmUtils.AddEnvelopeOnArmRequest(continuousJob, Request));
               
            }

            return NotFound($"Could not find the continuous job with the name \'{jobName}\'.");
        }

        [HttpPost("continuouswebjobs/{jobName}/start")]
        public IActionResult EnableContinuousJob(string jobName)
        {
            try
            {
                _continuousJobsManager.EnableJob(jobName);
                return Ok();
            }
            catch (JobNotFoundException)
            {
                return NotFound();
            }
            catch (IOException ex)
            {
                return Conflict(ex);
            }
        }

        [HttpPost("continuouswebjobs/{jobName}/stop")]
        public IActionResult DisableContinuousJob(string jobName)
        {
            try
            {
                _continuousJobsManager.DisableJob(jobName);
                return Ok();
            }
            catch (JobNotFoundException)
            {
                return NotFound();
            }
            catch (IOException ex)
            {
                return Conflict(ex);
            }
        }

        [HttpGet("continuouswebjobs/{jobName}/settings")]
        public IActionResult GetContinuousJobSettings(string jobName)
        {
            return GetJobSettings(jobName, _continuousJobsManager);
        }

        [HttpPut("continuouswebjobs/{jobName}/settings")]
        public IActionResult SetContinuousJobSettings(string jobName, JobSettings jobSettings)
        {
            return SetJobSettings(jobName, jobSettings, _continuousJobsManager);
        }

        [HttpGet("triggeredwebjobs")]
        public IActionResult ListTriggeredJobs()
        {
            if (_triggeredJobsManager == null)
            {
                return NotFound();
            }
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs(forceRefreshCache: false);

            return ListJobsResponseBasedOnETag(triggeredJobs);
        }

        [HttpGet("triggeredwebjobsswagger")]
        public IActionResult ListTriggeredJobsInSwaggerFormat()
        {
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs(forceRefreshCache: false);

            SwaggerApiDef responseSwagger = new SwaggerApiDef(triggeredJobs);
            return Ok(responseSwagger);
        }

        [HttpGet("triggeredwebjobs/{jobName}")]
        public IActionResult GetTriggeredJob(string jobName)
        {
            TriggeredJob triggeredJob = _triggeredJobsManager.GetJob(jobName);
            if (triggeredJob != null)
            {
                return Ok(AgentArmUtils.AddEnvelopeOnArmRequest(triggeredJob, Request));
            }

            return NotFound($"Could not find the triggered job with the name \'{jobName}\'.");
        }

        [HttpGet("triggeredwebjobs/{jobName}/history")]
        public IActionResult GetTriggeredJobHistory(string jobName)
        {
            string etag = GetRequestETag();

            string currentETag;
            TriggeredJobHistory history = _triggeredJobsManager.GetJobHistory(jobName, etag, out currentETag);

            if (history == null && currentETag == null)
            {
                return NotFound($"No history to show for the job \'{jobName}\'.");
            }

            if (etag == currentETag)
            {
                Response.Headers.ETag = new EntityTagHeaderValue(currentETag).ToString();
                return StatusCode(304, "Not Modified");
            }
            else
            {
                object triggeredJobHistoryResponse =
                    history != null && AgentArmUtils.IsArmRequest(Request) ? AgentArmUtils.AddEnvelopeOnArmRequest(history.TriggeredJobRuns, Request) : history;
                
                Response.Headers.ETag = new EntityTagHeaderValue(currentETag).ToString();
                return Ok(triggeredJobHistoryResponse);
            }
        }

        [HttpGet("triggeredwebjobs/{jobName}/history/{runId}")]
        public IActionResult GetTriggeredJobRun(string jobName, string runId)
        {
            TriggeredJobRun triggeredJobRun = _triggeredJobsManager.GetJobRun(jobName, runId);
            if (triggeredJobRun != null)
            {
                return Ok(AgentArmUtils.AddEnvelopeOnArmRequest(triggeredJobRun, Request));
            }

            return NotFound();
        }

        [HttpPost("triggeredwebjobs/{jobName}/run")]
        public IActionResult InvokeTriggeredJob(string jobName, string arguments = null)
        {
            try
            {
                Uri runUri = _triggeredJobsManager.InvokeTriggeredJob(jobName, arguments, "External - " + Request.Headers.UserAgent);

                // Add the run uri in the location so caller can get status on the running job
                Response.Headers.Add("Location", runUri.AbsoluteUri);
                
                // Return a 200 in the ARM case, otherwise a 202 can cause it to poll on /run, which we don't support
                // For non-ARM, stay with the 202 to reduce potential impact of change
                return AgentArmUtils.IsArmRequest(Request) ? Ok() : Accepted();
            }
            catch (JobNotFoundException)
            {
                return NotFound();
            }
            catch (IOException ex)
            {
                return Conflict(ex);
            }
            catch (ConflictException)
            {
                return Conflict(Resources.Error_WebJobAlreadyRunning);
            }
            catch (WebJobsStoppedException)
            {
                return Conflict(Resources.Error_WebJobsStopped);
            }
            catch
            {
                // There should be another way to check if the container is read only. We can do that right here, right?
                if (FileSystemHelpers.IsFileSystemReadOnly())
                {
                    // return 503 to ask caller to retry, since ReadOnly file system should be temporary
                    return StatusCode((int) HttpStatusCode.ServiceUnavailable);
                }

                throw;
            }
        }

        [HttpPut("continuouswebjobs/{jobName}")]
        public Task<IActionResult> CreateContinuousJob(string jobName)
        {
            return CreateJob(jobName, _continuousJobsManager);
        }

        /*[HttpPut("continuouswebjobs/{jobName}/settings/")]
        public IActionResult CreateContinuousJobArm(string jobName, ArmEntry<ContinuousJob> armContinuousJob)
        {
            return SetJobSettings(jobName, armContinuousJob.Properties.Settings, _continuousJobsManager);
        }*/

        [HttpDelete("continuouswebjobs/{jobName}")]
        public IActionResult RemoveContinuousJob(string jobName)
        {
            return RemoveJob(jobName, _continuousJobsManager);
        }

        [HttpPut("triggeredwebjobs/{jobName}")]
        public Task<IActionResult> CreateTriggeredJob(string jobName)
        {
            return CreateJob(jobName, _triggeredJobsManager);
        }

        /*[HttpPut]
        public IActionResult CreateTriggeredJobArm(string jobName, ArmEntry<TriggeredJob> armTriggeredJob)
        {
            return SetJobSettings(jobName, armTriggeredJob.Properties.Settings, _triggeredJobsManager);
        }*/

        [HttpDelete("triggeredwebjobs/{jobName}")]
        public IActionResult RemoveTriggeredJob(string jobName)
        {
            return RemoveJob(jobName, _triggeredJobsManager);
        }

        [HttpGet("triggeredwebjobs/{jobName}/settings")]
        public IActionResult GetTriggeredJobSettings(string jobName)
        {
            return GetJobSettings(jobName, _triggeredJobsManager);
        }

        [HttpPut("triggeredwebjobs/{jobName}/settings")]
        public IActionResult SetTriggeredJobSettings(string jobName, JobSettings jobSettings)
        {
            return SetJobSettings(jobName, jobSettings, _triggeredJobsManager);
        }

        [AcceptVerbs("GET", "HEAD", "PUT", "POST", "DELETE", "PATCH")]
        [Route("continuouswebjobs/{jobName}/passthrough/{path}")]
        public async Task<Object> RequestPassthrough(string jobName, string path)
        {
           try
            {
                // Convert HttpRequest to HttpRequestMessage
                HttpRequestMessageFeature hreqmf = new HttpRequestMessageFeature(Request.HttpContext);
                return await _continuousJobsManager.HandleRequest(jobName, path, hreqmf.HttpRequestMessage);
            }
            catch(Exception e)
            {
                //_tracer.TraceError(e);
                return BadRequest(e.Message);
            }
        }

        private IActionResult ListJobsResponseBasedOnETag(IEnumerable<JobBase> jobs)
        {
            string etag = GetRequestETag();

            string currentETag = "\"" + HashHelpers.CalculateCompositeHash(jobs.ToArray()).ToString("x") + "\"";

            if (etag == currentETag)
            {
                return StatusCode((int)HttpStatusCode.NotModified);
            }
            else
            {
                return Ok(jobs);
            }
        }

        private string GetRequestETag()
        {
            foreach (var header in Request.Headers)
            {
                if (header.Key == "ETag")
                {
                    return header.Value;
                }
            }
            return null;
            //return tempRequest.Headers.IfNoneMatch.Select(header => header.Tag).FirstOrDefault();
        }

        private IActionResult RemoveJob<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            jobsManager.DeleteJob(jobName);
            return Ok();
        }

        private async Task<IActionResult> CreateJob<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            TJob job = null;
            string errorMessage = null;
            HttpStatusCode errorStatusCode;

            // Get the script file name from the content disposition header
            string scriptFileName = null;

            if (Request.Headers != null && !Request.Headers.ContentDisposition.IsNullOrEmpty())
            {
                string[] filenameObject = Request.Headers.ContentDisposition.ToString().Split(";");
                
                // Find the part that contains 'filename=*'
                scriptFileName = Array.Find(filenameObject, e => e.Contains("filename="));

                if (scriptFileName == null)
                {
                    return BadRequest("Content-Disposition header must contain the filename");
                }

                // Drop the 'filename=' and any spaces that may come before it
                scriptFileName = scriptFileName.Substring(scriptFileName.IndexOf("filename=") + 9);
            }

            if (String.IsNullOrEmpty(scriptFileName))
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, Resources.Error_MissingContentDispositionHeader);
            }

            // Clean the file name from quotes and directories
            scriptFileName = scriptFileName.Trim('"');
            scriptFileName = Path.GetFileName(scriptFileName);

            StreamReader reader = new StreamReader(Request.Body);
            Stream fileStream = reader.BaseStream;
            
            try
            {
                // Upload as a zip if content type is of a zipped file
                if (!StringValues.IsNullOrEmpty(Request.Headers.ContentType) && Request.Headers.ContentType.Contains("media type=application/zip"))
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
                errorMessage += "]\n" + ex.InnerException;
                errorStatusCode = HttpStatusCode.InternalServerError;
                //_tracer.TraceError(ex);
            }

            // On error, delete job (if exists)
            if (errorMessage != null)
            {
                jobsManager.DeleteJob(jobName);
                return CreateErrorResponse(errorStatusCode, errorMessage);
            }

            return Ok(job);
        }

        private IActionResult GetJobSettings<TJob>(string jobName, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            try
            {
                JobSettings jobSettings = jobsManager.GetJobSettings(jobName);
                return Ok(jobSettings);
            }
            catch (JobNotFoundException)
            {
                return NotFound();
            }
        }

        private IActionResult SetJobSettings<TJob>(string jobName, JobSettings jobSettings, IJobsManager<TJob> jobsManager) where TJob : JobBase, new()
        {
            try
            {
                jobsManager.SetJobSettings(jobName, jobSettings);
                return Ok();
            }
            catch (JobNotFoundException)
            {
                return NotFound();
            }
        }

        private IActionResult CreateErrorResponse(HttpStatusCode errorStatusCode, string errorMessage)
        {
            return StatusCode((int) errorStatusCode, errorMessage);
        }

    }

    public class JobNotFoundException : InvalidOperationException
    {
        public JobNotFoundException(string message)
            : base(message)
        {
        }
    }

    public class ConflictException : InvalidOperationException
    {
        public ConflictException()
            : base()
        {
        }
    }

    public class WebJobsStoppedException : InvalidOperationException
    {
    }
}