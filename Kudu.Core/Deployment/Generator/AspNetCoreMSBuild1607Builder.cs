﻿using System;
using Kudu.Contracts.Settings;

namespace Kudu.Core.Deployment.Generator
{
    class AspNetCoreMSBuild1607Builder : MicrosoftSiteBuilder
    {
        private readonly string _version;

        public AspNetCoreMSBuild1607Builder(IEnvironment environment, IDeploymentSettingsManager settings, IBuildPropertyProvider propertyProvider, string sourcePath, string projectFilePath, string solutionPath)
            : base(environment, settings, propertyProvider, sourcePath, projectFilePath, solutionPath, "--aspNetCoreMSBuild1607")
        {
            if (projectFilePath.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase))
            {
                _version = "CSPROJ";
            }
            else if (projectFilePath.EndsWith(".xproj", StringComparison.OrdinalIgnoreCase))
            {
                // if it's xproj, throw invalidOperationException
                throw new InvalidOperationException(@"Building Asp.Net Core .xproj is no longer supported in Azure, please move to .csproj
For more information, please visit https://go.microsoft.com/fwlink/?linkid=850964");
            }
            else
            {
                _version = "PROJECT.JSON";
            }
        }

        public override string ProjectType
        {
            get { return $"ASP.NET CORE {_version} MSBUILD 16.7.0"; }
        }
    }
}
