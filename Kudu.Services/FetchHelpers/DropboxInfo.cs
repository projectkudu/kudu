using System;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;
using Kudu.Core.Deployment;
using Kudu.Contracts.SourceControl;

namespace Kudu.Services
{
    public class DropboxInfo : DeploymentInfo
    {
        private DropboxInfo(IRepositoryFactory repositoryFactory)
            : base(repositoryFactory)
        {
        }

        public int OAuthVersion { get; private set; }

        public DropboxDeployInfo DeployInfo { get; private set; }

        public static DropboxInfo CreateV1Info(JObject payload, RepositoryType repositoryType, IRepositoryFactory repositoryFactory)
        {
            return new DropboxInfo(repositoryFactory)
            {
                Deployer = DropboxHelper.Dropbox,
                DeployInfo = payload.ToObject<DropboxDeployInfo>(),
                RepositoryType = repositoryType,
                IsReusable = false,
                OAuthVersion = 1
            };
        }

        public static DropboxInfo CreateV2Info(string dropboxPath, string oauthToken, RepositoryType repositoryType, IRepositoryFactory repositoryFactory)
        {
            if (String.IsNullOrEmpty(dropboxPath))
            {
                throw new ArgumentNullException("dropboxPath");
            }

            if (String.IsNullOrEmpty(oauthToken))
            {
                throw new ArgumentNullException("oauthToken");
            }

            return new DropboxInfo(repositoryFactory)
            {
                Deployer = DropboxHelper.Dropbox,
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
