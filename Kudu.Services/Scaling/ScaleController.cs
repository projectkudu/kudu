using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using Kudu.Core.Scaling;
using Kudu.Core.Tracing;
using Kudu.Services.Arm;
using Kudu.Services.Filters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.Scaling
{
    [ArmControllerConfiguration]
    [FunctionExceptionFilter]
    public class ScaleController : ApiController
    {
        private readonly IScaleManager _manager;
        private readonly ITraceFactory _traceFactory;

        public ScaleController(IScaleManager manager, ITraceFactory traceFactory)
        {
            this._manager = manager;
            this._traceFactory = traceFactory;
        }

        [HttpGet]
        public async Task<HttpResponseMessage> List()
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step("ScaleController.List()"))
            {
                var workers = await _manager.ListWorkers();
                return Request.CreateResponse(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest(workers, Request));
            }
        }

        [HttpGet]
        public async Task<HttpResponseMessage> Get(string id)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"ScaleController.Get({id})"))
            {
                var worker = await _manager.GetWorker(id);
                return Request.CreateResponse<object>(HttpStatusCode.OK, ArmUtils.AddEnvelopeOnArmRequest<WorkerInfo>(worker, Request));
            }
        }

        [HttpPut]
        public async Task<HttpResponseMessage> Update(string id)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"ScaleController.Update({id})"))
            {
                var info = JsonConvert.DeserializeObject<WorkerInfo>(await Request.Content.ReadAsStringAsync());
                await _manager.UpdateWorker(id, info);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }
        }

        [HttpDelete]
        public async Task<HttpResponseMessage> Delete(string id)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"ScaleController.Delete({id})"))
            {
                return await _manager.RemoveWorker(id);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> AddWorker(string id)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"ScaleController.AddWorker({id})"))
            {
                return await _manager.AddWorker(id);
            }
        }

        [HttpPost]
        public async Task<HttpResponseMessage> PingWorker(string id)
        {
            var tracer = _traceFactory.GetTracer();
            using (tracer.Step($"ScaleController.PingWorker({id})"))
            {
                return await _manager.PingWorker(id);
            }
        }
        ////private JObject GetJsonContent()
        ////{
        ////    try
        ////    {
        ////        var payload = Request.Content.ReadAsAsync<JObject>().Result;
        ////        if (ArmUtils.IsArmRequest(Request))
        ////        {
        ////            payload = payload.Value<JObject>("properties");
        ////        }

        ////        return payload;
        ////    }
        ////    catch
        ////    {
        ////        // We're going to return null here since we don't want to force a breaking change
        ////        // on the client side. If the incoming request isn't application/json, we want this
        ////        // to return null.
        ////        return null;
        ////    }
        ////}
    }
}
