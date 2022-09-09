using System.Threading.Tasks;

namespace Kudu.Contracts
{
    public interface ILinuxConsumptionEnvironment
    {
        /// <summary>
        /// Gets a value indicating whether requests should be delayed.
        /// </summary>
        bool DelayRequestsEnabled { get; }

        Task DelayCompletionTask { get; }

        /// <summary>
        /// Gets a value indicating whether the current environment is in standby mode.
        /// </summary>
        bool InStandbyMode { get; }

        /// <summary>
        /// Flags that requests under this environment should be delayed.
        /// </summary>
        void DelayRequests();

        /// <summary>
        /// Flags that requests under this environment should be resumed.
        /// </summary>
        void ResumeRequests();

        /// <summary>
        /// Flags the current environment as ready and specialized.
        /// This sets <see cref="EnvironmentSettingNames.AzureWebsitePlaceholderMode"/> to "0"
        /// and <see cref="EnvironmentSettingNames.AzureWebsiteContainerReady"/> to "1" against
        /// the current environment.
        /// </summary>
        void FlagAsSpecializedAndReady();
    }
}
