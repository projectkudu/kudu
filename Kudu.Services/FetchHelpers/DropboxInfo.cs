using System;
using Kudu.Core.SourceControl;
using Kudu.Services.ServiceHookHandlers;
using Newtonsoft.Json.Linq;

namespace Kudu.Services
{
    public class DropboxInfo : DeploymentInfo
    {
        private DropboxInfo()
        {
        }

        public int OAuthVersion { get; private set; }

        public DropboxDeployInfo DeployInfo { get; private set; }

        public static DropboxInfo CreateV1Info(JObject payload, RepositoryType repositoryType)
        {
            return new DropboxInfo
            {
                Deployer = DropboxHelper.Dropbox,
                DeployInfo = payload.ToObject<DropboxDeployInfo>(),
                RepositoryType = repositoryType,
                IsReusable = false,
                OAuthVersion = 1
            };
        }

        public static DropboxInfo CreateV2Info(string dropboxPath, string oauthToken, RepositoryType repositoryType)
        {
            if (String.IsNullOrEmpty(dropboxPath))
            {
                throw new ArgumentNullException("dropboxPath");
            }

            if (String.IsNullOrEmpty(oauthToken))
            {
                throw new ArgumentNullException("oauthToken");
            }

            return new DropboxInfo
            {
                DeployInfo = new DropboxDeployInfo
                             {
                                 Path = dropboxPath,
                                 Token = oauthToken
                             },
                OAuthVersion = 2,
                RepositoryType = repositoryType
            };
        }
    }
}
