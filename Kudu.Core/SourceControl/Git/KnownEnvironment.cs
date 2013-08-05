
namespace Kudu.Core.SourceControl.Git
{
    /// <summary>
    /// Environment variables used for the post receive hook
    /// </summary>
    internal static class KnownEnvironment
    {
        public const string EXEPATH = "KUDU_EXE";
        public const string APPPATH = "KUDU_APPPATH";
        public const string MSBUILD = "KUDU_MSBUILD";
        public const string DEPLOYER = "KUDU_DEPLOYER";
        /// <summary>
        /// The HEAD commit id prior to the receive
        /// </summary>
        public const string HEAD_COMMIT_ID = "KUDU_HEAD_COMMIT_ID";

        // Command to launch the post receive hook
        public static readonly string KUDUCOMMAND = "\"$" + EXEPATH + "\" " +
                                                    "\"$" + APPPATH + "\" " +
                                                    "\"$" + MSBUILD + "\" " +
                                                    "\"$" + DEPLOYER + "\"";
    }
}
