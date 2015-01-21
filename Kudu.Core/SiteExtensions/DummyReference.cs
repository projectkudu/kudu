#pragma warning disable
extern alias nugetcore;
using System.Diagnostics.CodeAnalysis;

namespace Kudu.Core.SiteExtensions
{
    /// <summary>
    /// 
    /// This class is ment for maintaining a direct reference to those NuGet client dll that loaded via MEF by v3 client,
    /// since MEF loads dll via reflection, Kudu.Core do not have any direct usage to those dll, dll will be drop during build time,
    /// causing project that reference Kudu.Core won`t function properly due to missing dll.
    /// 
    /// Another work-around could add required nuget packages to projects that reference Kudu.Core,
    /// I prefer to do the hack within Kudu.Core, other projects will just work as expect when they reference Kudu.Core.
    /// Since it is hard to find out and remember we need to include extra NuGet packages in order to correctly reference Kudu.Core.
    /// </summary>
    internal class DummyReference
    {
        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Client.dll
        private NuGet.Client.V3SimpleSearchResourceProvider v3SimpleSearchResourceProvider = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Client.BaseTypes.dll
        private NuGet.Client.INuGetResource iNuGetResource = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Client.V2.dll
        private NuGet.Client.V2.V2SimpleSearchResourceProvider v2SimpleSearchResourceProvider = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Client.V2.VisualStudio.dll
        private NuGet.Client.V2.VisualStudio.V2UISearchResourceProvider v2UISearchResourceProvider = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Client.V3.VisualStudio.dll
        private NuGet.Client.V3.VisualStudio.V3UISearchResourceProvider v3UISearchResourceProvider = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Client.VisualStudio.dll
        private NuGet.Client.VisualStudio.UISearchResource uiSearchResource = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGetConfiguration.dll
        private NuGet.Configuration.SettingValue settingValue = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Core.dll
        private nugetcore::NuGet.AggregateConstraintProvider aggregateConstraintProvider = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Data.dll
        private NuGet.Data.INuGetRequestModifier iNuGetRequestModifier = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Frameworks.dll
        private NuGet.Frameworks.IFrameworkCompatibilityProvider iFrameworkCompatibilityProvider = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Packaging.dll
        private NuGet.Packaging.INuspecReader iNuspecReader = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.PackagingCore.dll
        private NuGet.PackagingCore.IPackageReaderCore iPackageReaderCore = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Resolver.dll
        private NuGet.Resolver.PackageResolver packageResolver = null;

        [SuppressMessage("Microsoft.Performance", "CA1823:AvoidUnusedPrivateFields")]
        // NuGet.Versioning.dll
        private NuGet.Versioning.IVersionComparer iVersionComparer = null;
    }
}
