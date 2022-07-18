using Kudu.Agent;
using Kudu.Contracts;
using Kudu.Contracts.Jobs;
using Kudu.Contracts.Tracing;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Kudu.Services.Jobs
{
    //[ArmControllerConfiguration]
    [ApiController]
    [Route("/webjobs")]
    public class JobsController : ControllerBase
    {
        private readonly ITracer _tracer;
        private readonly ITriggeredJobsManager _triggeredJobsManager;
        private readonly IContinuousJobsManager _continuousJobsManager;

        public const string GeoLocationHeaderKey = "x-ms-geo-location";
        public static string TmpFolder = System.Environment.ExpandEnvironmentVariables(@"%WEBROOT_PATH%\data\Temp");

        public JobsController(ITriggeredJobsManager triggeredJobsManager, IContinuousJobsManager continuousJobsManager, ITracer tracer)
        {
            _triggeredJobsManager = triggeredJobsManager;
            _continuousJobsManager = continuousJobsManager;
            _tracer = tracer;
        }

        [HttpGet("listalljobs/")]
        public IActionResult ListAllJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs(forceRefreshCache: false);
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs(forceRefreshCache: false);

            var allJobs = triggeredJobs.OfType<JobBase>().Union(continuousJobs);

            return ListJobsResponseBasedOnETag(allJobs);
        }

        [HttpGet("/listcontinuousjobs")]
        public IActionResult ListContinuousJobs()
        {
            IEnumerable<ContinuousJob> continuousJobs = _continuousJobsManager.ListJobs(forceRefreshCache: false);

            return ListJobsResponseBasedOnETag(continuousJobs);
        }

        [HttpGet("/getcontinuousjob/{jobName}")]
        public IActionResult GetContinuousJob(string jobName)
        {
            ContinuousJob continuousJob = _continuousJobsManager.GetJob(jobName);
            if (continuousJob != null)
            {
                // return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(continuousJob, Request));
                //return Ok(ArmUtils.AddEnvelopeOnArmRequest(continuousJob, Request));
                return Ok();
            }

            return NotFound();
        }

        [HttpPost("{jobName}/enablecontinuousjob")]
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

        [HttpPost("{jobName}/disablecontinuousjob")]
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

        [HttpGet("{jobName}/getcontinuousjobsettings")]
        public IActionResult GetContinuousJobSettings(string jobName)
        {
            return GetJobSettings(jobName, _continuousJobsManager);
        }

        [HttpPut("{jobName}/setcontinuousjobsettings/{jobSettings}")]
        public IActionResult SetContinuousJobSettings(string jobName, JobSettings jobSettings)
        {
            return SetJobSettings(jobName, jobSettings, _continuousJobsManager);
        }

        [HttpGet("/listtriggeredjobs")]
        public IActionResult ListTriggeredJobs()
        {
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs(forceRefreshCache: false);

            return ListJobsResponseBasedOnETag(triggeredJobs);
        }

        [HttpGet("/listtriggeredjobsinswaggerformat")]
        public IActionResult ListTriggeredJobsInSwaggerFormat()
        {
            IEnumerable<TriggeredJob> triggeredJobs = _triggeredJobsManager.ListJobs(forceRefreshCache: false);

            SwaggerApiDef responseSwagger = new SwaggerApiDef(triggeredJobs);
            return Ok(responseSwagger);
        }

        [HttpGet("/gettriggeredjob/{jobName}")]
        public IActionResult GetTriggeredJob(string jobName)
        {
            TriggeredJob triggeredJob = _triggeredJobsManager.GetJob(jobName);
            if (triggeredJob != null)
            {
                //return Ok(ArmUtils.AddEnvelopeOnArmRequest(triggeredJob, Request));
                return Ok(triggeredJob);
            }

            return NotFound();
        }

        [HttpGet("{jobName}/getHistory")]
        public IActionResult GetTriggeredJobHistory(string jobName)
        {
            string etag = GetRequestETag();

            string currentETag;
            TriggeredJobHistory history = _triggeredJobsManager.GetJobHistory(jobName, etag, out currentETag);

            if (history == null && currentETag == null)
            {
                return NotFound();
            }

            HttpResponseMessage response;
            if (etag == currentETag)
            {
            }
            else
            {
                //object triggeredJobHistoryResponse =
                //history != null && IsArmRequest(Request) ? ArmUtils.AddEnvelopeOnArmRequest(history.TriggeredJobRuns, Request) : history;
                object triggeredJobHistoryResponse =
                    history != null && IsArmRequest(Request) ? history.TriggeredJobRuns : history;
                return Ok(triggeredJobHistoryResponse);
                //response = Request.CreateResponse(HttpStatusCode.OK, triggeredJobHistoryResponse);

            }
            /*response.Headers.ETag = new EntityTagHeaderValue(currentETag);
            return response;*/

            return Ok();
        }

        [HttpGet("{jobName}/getrun/{runId}")]
        public IActionResult GetTriggeredJobRun(string jobName, string runId)
        {
            TriggeredJobRun triggeredJobRun = _triggeredJobsManager.GetJobRun(jobName, runId);
            if (triggeredJobRun != null)
            {
                //return Ok(ArmUtils.AddEnvelopeOnArmRequest(triggeredJobRun, Request));
                return Ok(triggeredJobRun);
            }

            return NotFound();
        }

        [HttpPost("{jobName}/invoke")]
        public IActionResult InvokeTriggeredJob(string jobName, string arguments = null)
        {
            try
            {
                Uri runUri = _triggeredJobsManager.InvokeTriggeredJob(jobName, arguments, "External - " + Request.Headers.UserAgent);

                // Return a 200 in the ARM case, otherwise a 202 can cause it to poll on /run, which we don't support
                // For non-ARM, stay with the 202 to reduce potential impact of change
                if (IsArmRequest(Request))
                {
                    return Ok();
                }
                else
                {
                    return Accepted();
                }
                // Add the run uri in the location so caller can get status on the running job
                
                // TODO: What does this do? Is it important?
                //response.Headers.Add("Location", runUri.AbsoluteUri);

                //return response;
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
                // TODO: This seems very important but like a lot of code that will need to be ported in. Let's discuss
                /*if (FileSystemHelpers.IsFileSystemReadOnly())
                {
                    // return 503 to ask caller to retry, since ReadOnly file system should be temporary
                    return StatusCode((int) HttpStatusCode.ServiceUnavailable);
                }*/
                
                // Check if file system is read only

                //System.IO.DriveInfo di = new System.IO.DriveInfo(@"C:\");
                
                
                // TODO: DISCUSS WHAT DIRECTORY TO CHECK. Need this to be linux compatibile.
                DirectoryInfo di = new DirectoryInfo(@"~");
                if ((di.Attributes & FileAttributes.ReadOnly) == FileAttributes.ReadOnly)
                {
                    return StatusCode((int)HttpStatusCode.ServiceUnavailable);
                }

                throw;
            }
        }

        [HttpPut("{jobName}/createcontinuous")]
        public Task<IActionResult> CreateContinuousJob(string jobName)
        {
            return CreateJob(jobName, _continuousJobsManager);
        }

        /*[HttpPut]
        public IActionResult CreateContinuousJobArm(string jobName, ArmEntry<ContinuousJob> armContinuousJob)
        {
            return SetJobSettings(jobName, armContinuousJob.Properties.Settings, _continuousJobsManager);
        }*/

        [HttpDelete("{jobName}/removecontinuous")]
        public IActionResult RemoveContinuousJob(string jobName)
        {
            return RemoveJob(jobName, _continuousJobsManager);
        }

        [HttpPut("{jobName}/createtriggered")]
        public Task<IActionResult> CreateTriggeredJob(string jobName)
        {
            return CreateJob(jobName, _triggeredJobsManager);
        }

        /*[HttpPut]
        public IActionResult CreateTriggeredJobArm(string jobName, ArmEntry<TriggeredJob> armTriggeredJob)
        {
            return SetJobSettings(jobName, armTriggeredJob.Properties.Settings, _triggeredJobsManager);
        }*/

        [HttpDelete("{jobName}/remotetriggered")]
        public IActionResult RemoveTriggeredJob(string jobName)
        {
            return RemoveJob(jobName, _triggeredJobsManager);
        }

        [HttpGet("{jobName}/gettriggeredsettings")]
        public IActionResult GetTriggeredJobSettings(string jobName)
        {
            return GetJobSettings(jobName, _triggeredJobsManager);
        }

        [HttpPut("{jobName}/settriggeredsettings/{jobSettings}")]
        public IActionResult SetTriggeredJobSettings(string jobName, JobSettings jobSettings)
        {
            return SetJobSettings(jobName, jobSettings, _triggeredJobsManager);
        }

        [AcceptVerbs("GET", "HEAD", "PUT", "POST", "DELETE", "PATCH")]
        public async Task<IActionResult> RequestPassthrough(string jobName, string path)
        {
            try
            {
                // TODO: COME BACK AND IMPLEMENT
                //return await _continuousJobsManager.HandleRequest(jobName, path, Request);
                return NotFound();
            }
            catch(Exception e)
            {
                //_tracer.TraceError(e);
                return NotFound(e);
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
            // TODO: REALLY NOT CONVINCED THAT THIS WORKS :)
            HttpRequestMessage tempRequest = new HttpRequestMessage(HttpMethod.Get, "");
            //Add the valid headers to the new request
            foreach (var header in Request.Headers)
            {
                tempRequest.Headers.Add(header.Key, header.Value.ToString());
            }

            return tempRequest.Headers.IfNoneMatch.Select(header => header.Tag).FirstOrDefault();
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
            if (Request.Headers != null && Request.Headers.ContentDisposition.Contains("filename"))
            {
                //scriptFileName = Request.Headers.ContentDisposition.FileName;
                
                
                // TODO: Revisit the reliability of this
                // What is it grabbing here? Is it grabbing 'filename=test.txt' or just 'filename=' or just 'test.txt'
                string[] filenameObject = Request.Headers.ContentDisposition.ToArray();
                scriptFileName = Array.Find(filenameObject, e => e.Contains("filename"));
            }

            if (String.IsNullOrEmpty(scriptFileName))
            {
                return CreateErrorResponse(HttpStatusCode.BadRequest, Resources.Error_MissingContentDispositionHeader);
            }

            // Clean the file name from quotes and directories
            scriptFileName = scriptFileName.Trim('"');
            scriptFileName = Path.GetFileName(scriptFileName);

            //Stream fileStream = await content.ReadAsStreamAsync();
            StreamReader reader = new StreamReader(Request.Body);
            Stream fileStream = reader.BaseStream;

            // TODO: PRETTY SURE THE ABOVE STREAM IS NOT HOW THIS WORKS

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

        public static bool IsArmRequest(HttpRequest request)
        {
            return request != null &&
                   request.Headers != null &&
                   request.Headers.ContainsKey(GeoLocationHeaderKey);
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