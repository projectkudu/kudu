using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;

namespace Kudu.Core.Infrastructure
{
    /// <summary>
    /// The SettingsProcessor understands how to turn environment settings set by WAWS to app settings and connection strings
    /// </summary>
    public class SettingsProcessor
    {
        private const string AppSettingPrefix = "APPSETTING_";
        private const string SqlServerPrefix = "SQLCONNSTR_";
        private const string MySqlServerPrefix = "MYSQLCONNSTR_";
        private const string SqlAzureServerPrefix = "SQLAZURECONNSTR_";
        private const string CustomPrefix = "CUSTOMCONNSTR_";

        public Dictionary<string, string> AppSettings { get; private set; }
        public List<ConnectionStringSettings> ConnectionStrings { get; private set; }

        private static readonly Lazy<SettingsProcessor> _instance = new Lazy<SettingsProcessor>(() => new SettingsProcessor());

        public static SettingsProcessor Instance
        {
            get { return _instance.Value; }
        }

        private SettingsProcessor()
        {
            AppSettings = new Dictionary<string, string>();
            ConnectionStrings = new List<ConnectionStringSettings>();
            Initialize();
        }

        private void Initialize()
        {
            // Go through all the environment variables and process those that start
            // with one of WAWS prefixes
            foreach (DictionaryEntry entry in System.Environment.GetEnvironmentVariables())
            {
                var name = (string)entry.Key;
                var val = (string)entry.Value;

                if (name.StartsWith(MySqlServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(MySqlServerPrefix.Length);
                    SetConnectionString(name, val, "MySql.Data.MySqlClient");
                }
                else if (name.StartsWith(SqlAzureServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(SqlAzureServerPrefix.Length);
                    SetConnectionString(name, val, "System.Data.SqlClient");
                }
                else if (name.StartsWith(SqlServerPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(SqlServerPrefix.Length);
                    SetConnectionString(name, val, "System.Data.SqlClient");
                }
                else if (name.StartsWith(CustomPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(CustomPrefix.Length);
                    SetConnectionString(name, val);
                }
                else if (name.StartsWith(AppSettingPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    name = name.Substring(AppSettingPrefix.Length);
                    AppSettings[name] = val;
                }
            }
        }

        private void SetConnectionString(string name, string connString, string providerName = null)
        {
            ConnectionStrings.Add(new ConnectionStringSettings(name, connString, providerName));
        }
    }
}