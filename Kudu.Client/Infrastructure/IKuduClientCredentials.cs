// -----------------------------------------------------------------------
// <copyright file="IKuduCredentials.cs" company="Microsoft">
// TODO: Update copyright text.
// </copyright>
// -----------------------------------------------------------------------

namespace Kudu.Client.Infrastructure
{
    using System.Net;

    public interface IKuduClientCredentials
    {
        ICredentials Credentials { get; set; }
    }
}
