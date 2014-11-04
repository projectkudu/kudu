using System;

namespace Kudu.SiteManagement.Configuration
{
    [Flags]
    public enum UriScheme
    {
        None = 0,
        Http = 1,
        Https = 2,
        Both = Http | Https
    }
}