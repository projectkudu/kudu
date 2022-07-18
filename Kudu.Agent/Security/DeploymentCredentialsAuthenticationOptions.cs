//------------------------------------------------------------------------------
// <copyright file="DeploymentCredentialsAuthenticationOptions.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
//------------------------------------------------------------------------------

using Microsoft.AspNetCore.Authentication;

namespace Kudu.Agent.Security
{
    public class DeploymentCredentialsAuthenticationOptions : AuthenticationSchemeOptions
    {
        public DeploymentCredentialsAuthenticationOptions()
        {
        }
    }
}