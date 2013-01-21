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

        private readonly IDeploymentSettingsManager _settingsManager;
        private readonly SiteBuilderFactory _originalSiteBuilderFactory;
        private readonly Generator.SiteBuilderFactory _generatorSiteBuilderFactory;

        public SiteBuilderFactoryDispatcher(IDeploymentSettingsManager settingsManager, IBuildPropertyProvider propertyProvider, IEnvironment environment)
        {
            _settingsManager = settingsManager;

            _originalSiteBuilderFactory = new SiteBuilderFactory(settingsManager, propertyProvider, environment);
            _generatorSiteBuilderFactory = new Generator.SiteBuilderFactory(settingsManager, propertyProvider, environment);
        }

        public ISiteBuilder CreateBuilder(ITracer tracer, ILogger logger)
        {
            return CurrentSiteBuilderFactory.CreateBuilder(tracer, logger);
        }

        private ISiteBuilderFactory CurrentSiteBuilderFactory
        {
            get
            {
                string setting = _settingsManager.GetValue(SettingsKeys.SiteBuilderFactory);
                if (String.Equals(setting, Original, StringComparison.InvariantCultureIgnoreCase))
                {
                    return _originalSiteBuilderFactory;
                }

                // Default
                return _generatorSiteBuilderFactory;
            }
        }
    }
}
