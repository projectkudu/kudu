using System;
using System.IO;
using System.Net;
using System.Web;
using Kudu.Contracts.SourceControl;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.SourceControl;
using Kudu.Core.SourceControl.Git;
using Kudu.Core.Tracing;

namespace Kudu.Services.GitServer
{
    public class CustomGitRepositoryHandler : IHttpHandler
    {
        public enum GitServerRequestType
        {
            Unknown,
            AdvertiseUploadPack,
            AdvertiseReceivePack,
            ReceivePack,
            UploadPack,
            LegacyInfoRef,
        };

        private readonly Func<Type, object> _getInstance;
        private readonly ITracer _tracer;
        // We need to take an instance constructor from so we can ensure we create the IGitServer after
        // repository path in IEnvironment is set.
        public CustomGitRepositoryHandler(Func<Type, object> getInstance)
        {
            _getInstance = getInstance;
            _tracer = GetInstance<ITracer>();
        }

        public void ProcessRequest(HttpContext context)
        {
            ProcessRequestBase(new HttpContextWrapper(context));
        }

        // Take HttpContextBase for mock/testability.
        public void ProcessRequestBase(HttpContextBase context)
        {
            using (_tracer.Step("CustomGitServerController.ProcessRequest"))
            {
                GitServerRequestType requestType;
                string repoRelFilePath;

                _tracer.Trace("Parsing request uri {0}", context.Request.Url.AbsoluteUri);
                if (!TryParseUri(context.Request.Url, out repoRelFilePath, out requestType))
                {
                    context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
                    context.Response.End();
                    return;
                }

                IEnvironment env = GetInstance<IEnvironment>();
                var repoFullPath = Path.GetFullPath(Path.Combine(env.RootPath, repoRelFilePath));
                env.RepositoryPath = repoFullPath;
                _tracer.Trace("Using repository path: {0}", repoFullPath);

                switch (requestType)
                {
                    case GitServerRequestType.AdvertiseUploadPack:
                        using (_tracer.Step("CustomGitServerController.AdvertiseUploadPack"))
                        {
                            if (RepositoryExists(context))
                            {
                                var gitServer = GetInstance<IGitServer>();
                                GitServerHttpHandler.UpdateNoCacheForResponse(context.Response);
                                context.Response.ContentType = "application/x-git-upload-pack-advertisement";
                                context.Response.StatusCode = (int)HttpStatusCode.OK;
                                context.Response.OutputStream.PktWrite("# service=git-upload-pack\n");
                                context.Response.OutputStream.PktFlush();
                                gitServer.AdvertiseUploadPack(context.Response.OutputStream);
                                context.Response.End();
                            }
                        }
                        break;
                    case GitServerRequestType.UploadPack:
                        if (RepositoryExists(context))
                        {
                            UploadPackHandler uploadPackHandler = GetInstance<UploadPackHandler>();
                            context.Response.StatusCode = (int)HttpStatusCode.OK;
                            uploadPackHandler.ProcessRequestBase(context);
                            context.Response.End();
                        }
                        break;
                    default:
                        context.Response.StatusCode = (int)HttpStatusCode.NotImplemented;
                        context.Response.End();
                        break;
                }

                return;
            }
        }

        //  parse one of the four
        //  GET Git/{repositorypath}/info/refs?service=git-upload-pack
        //  GET Git/{repositorypath}/info/refs?service=git-receive-pack
        //  GET Git/{repositorypath}/info/refs
        // POST Git/{repositorypath}/git-receive-pack
        // POST Git/{repositorypath}/git-upload-pack
        public static bool TryParseUri(Uri url, out string repoRelLocalPath, out GitServerRequestType requestType)
        {
            repoRelLocalPath = null;
            requestType = GitServerRequestType.Unknown;
            // AbsolutePath returns encoded path, decoded so we can interpret as local file paths.
            var pathElts = HttpUtility.UrlDecode(url.AbsolutePath)
                                      .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathElts.Length < 2)
            {
                return false;
            }

            var repoPathEltStart = 1;
            var repoPathEltEnd = 0;
            var firstPathElt = pathElts[0];
            var lastPathElt = pathElts[pathElts.Length - 1];
            var nextToLastPathElt = pathElts[pathElts.Length - 2];

            if (!firstPathElt.Equals("git", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (nextToLastPathElt.Equals("info", StringComparison.OrdinalIgnoreCase) &&
               lastPathElt.Equals("refs", StringComparison.OrdinalIgnoreCase))
            {
                repoPathEltEnd = pathElts.Length - 2;
                var queryParams = HttpUtility.ParseQueryString(url.Query);
                var serviceValue = queryParams["service"];
                if (String.IsNullOrEmpty(url.Query))
                {
                    requestType = GitServerRequestType.LegacyInfoRef;
                }
                else if (serviceValue != null && serviceValue.Equals("git-upload-pack", StringComparison.OrdinalIgnoreCase))
                {
                    requestType = GitServerRequestType.AdvertiseUploadPack;
                }
                else if (serviceValue != null && serviceValue.Equals("git-receive-pack", StringComparison.OrdinalIgnoreCase))
                {
                    requestType = GitServerRequestType.AdvertiseReceivePack;
                }
                else
                {
                    return false;
                }
            }
            else if (lastPathElt.Equals("git-receive-pack", StringComparison.OrdinalIgnoreCase))
            {
                repoPathEltEnd = pathElts.Length - 1;
                requestType = GitServerRequestType.ReceivePack;
            }
            else if (lastPathElt.Equals("git-upload-pack", StringComparison.OrdinalIgnoreCase))
            {
                repoPathEltEnd = pathElts.Length - 1;
                requestType = GitServerRequestType.UploadPack;
            }
            else
            {
                return false;
            }

            var repoPathEltsCount = (repoPathEltEnd - repoPathEltStart);
            string[] repoPathElts = new string[repoPathEltsCount];
            Array.Copy(pathElts, repoPathEltStart, repoPathElts, 0, repoPathEltsCount);
            repoRelLocalPath = string.Join(@"\", repoPathElts);
            return true;
        }

        public bool RepositoryExists(HttpContextBase context)
        {
            // Ensure that the target directory has a git repository.
            IRepositoryFactory repositoryFactory = GetInstance<IRepositoryFactory>();
            IRepository repository = repositoryFactory.GetCustomRepository();
            if (repository != null && repository.RepositoryType == RepositoryType.Git)
            {
                return true;
            }
            context.Response.StatusCode = (int)HttpStatusCode.NotFound;
            context.Response.End();
            return false;
        }

        public bool IsReusable
        {
            get { return false; }
        }

        private T GetInstance<T>()
        {
            return (T)_getInstance(typeof(T));
        }
    }
}
