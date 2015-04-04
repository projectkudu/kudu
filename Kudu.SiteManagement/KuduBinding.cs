using System;
using System.Globalization;
using Kudu.SiteManagement.Configuration.Section;

namespace Kudu.SiteManagement
{
    public struct KuduBinding : IFormattable
    {
        public SiteType SiteType { get; set; }

        public int Port { get; set; }
        public UriScheme Scheme { get; set; }
        public string Ip { get; set; }
        public string Host { get; set; }

        public string DnsName { get; set; }

        public string Certificate { get; set; }
        public bool Sni { get; set; }

        public override string ToString()
        {
            return ToString("L");
        }

        public string ToString(string format)
        {
            return ToString(format, CultureInfo.InvariantCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var builder = new UriBuilder();
            builder.Scheme = Scheme.ToString().ToLowerInvariant();
            switch (format.ToUpperInvariant())
            {
                //NOTE: Local format, uses localhost for catch all host configuration.
                //      this format is default.
                case "L":
                    builder.Host = string.IsNullOrEmpty(Host) ? "localhost" : Host;
                    break;

                //NOTE: Targetable format, uses DnsName for catch all host configuration.
                case "T":
                    builder.Host = string.IsNullOrEmpty(Host) ? DnsName : Host;
                    break;

                case "B":
                    return string.Format("{0}:{1}:{2}:{3}", Scheme, Ip, Port, Host);

                default:
                    throw new FormatException("The format '" + format + "' is not supported.");
            }

            builder.Port = Port;
            if (
                (Port == 80 && Scheme == UriScheme.Http) ||
                (Port == 443 && Scheme == UriScheme.Https))
            {
                //Reset port when it's the protocol default.
                builder.Port = -1;
            }
            return builder.ToString();
        }

        public static KuduBinding Parse(string binding)
        {
            string[] parts = binding.Split(':');

            int port;
            if (!int.TryParse(parts[2], out port))
                port = -1;

            return new KuduBinding
            {
                Scheme = parts[0].Equals("https",StringComparison.OrdinalIgnoreCase) ? UriScheme.Https : UriScheme.Http,
                Ip = parts[1],
                Port = port,
                Host = parts[3]
            };
        }
    }
}