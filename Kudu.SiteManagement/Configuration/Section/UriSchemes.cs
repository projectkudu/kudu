using System;

namespace Kudu.SiteManagement.Configuration.Section
{
    [Flags]
    public enum UriSchemes
    {
        None = 0,
        Http = 1,
        Https = 2,
        Both = Http | Https
    }
}