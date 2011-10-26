using System;
using System.IO;
using System.Net;
using System.Net.Http.Formatting;
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace Kudu.Core.Infrastructure
{
    public class SimpleJsonMediaTypeFormatter : JsonMediaTypeFormatter
    {

        public SimpleJsonMediaTypeFormatter()
            : base()
        {
        }

        protected override object OnReadFromStream(Type type, Stream stream, HttpContentHeaders contentHeaders)
        {
            var reader = new StreamReader(stream);
            return JsonConvert.DeserializeObject(reader.ReadToEnd(), type);
        }

        protected override void OnWriteToStream(Type type, object value, Stream stream, HttpContentHeaders contentHeaders, TransportContext context)
        {
            var writer = new StreamWriter(stream);
            writer.Write(JsonConvert.SerializeObject(value, Formatting.None));
            writer.Flush();
        }
    }
}