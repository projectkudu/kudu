using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using System;
using System.Diagnostics;
using System.Linq;

namespace Kudu.Core.Deployment
{
    public class SiteBuilderFactoryDispatcher : ISiteBuilderFactory
    {
        private const string Original = "Original";

        private readonly SiteBuilderFactory _originalSiteBuilderFactory;
        private readonly Generator.SiteBuilderFactory _generatorSiteBuilderFactory;

        public SiteBuilderFactoryDispatcher(IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _originalSiteBuilderFactory = new SiteBuilderFactory(propertyProvider, environment);
            _generatorSiteBuilderFactory = new Generator.SiteBuilderFactory(propertyProvider, environment);
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger, IDeploymentSettingsManager settingsManager)
        {
            return GetCurrentSiteBuilderFactory(settingsManager).CreateBuilder(tracer, logger, settingsManager);
        }

        private ISiteBuilderFactory GetCurrentSiteBuilderFactory(IDeploymentSettingsManager settingsManager)
        {
            string setting = settingsManager.GetValue(SettingsKeys.SiteBuilderFactory);
            if (String.Equals(setting, Original, StringComparison.OrdinalIgnoreCase))
            {
                return _originalSiteBuilderFactory;
            }

            // Default
            return _generatorSiteBuilderFactory;
        }
    }
}
