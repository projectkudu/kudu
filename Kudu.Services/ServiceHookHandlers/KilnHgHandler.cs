using System;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Web;
using Kudu.Contracts.Settings;
using Kudu.Core.SourceControl;
using Newtonsoft.Json.Linq;

namespace Kudu.Services.ServiceHookHandlers
{
    /// <summary>
    /// This hook handler provides support for Fog Creek's Kiln in both a hosted and self-hosted setup.
    /// 
    /// Both public and private repositories are supported. Public repositories will require the
    /// setting value kiln.accesstoken to be left blank while private repositories will require it
    /// to be set to an Access Token (write permissions are not required).
    /// 
    /// To use a self-hosted setup you will need to add in a setting value called kiln.domain. This
    /// is a regex that will be used to parse your non kilnhg.com URLs from the payload.
    /// </summary>
    public class KilnHgHandler : ServiceHookHandlerBase
    {
        private static readonly Regex AuthorPattern = new Regex("(?<name>.*)<.*>", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);
        private static readonly Regex EmailPattern = new Regex(".*<(?<email>.*@.*)>.*|(?<email>.*@.*)", RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

        private readonly IDeploymentSettingsManager _settings;

        public KilnHgHandler(IDeploymentSettingsManager settings)
        {
            _settings = settings;
        }

        public override DeployAction TryParseDeploymentInfo(HttpRequestBase request, JObject payload, string targetBranch, out DeploymentInfo deploymentInfo)
        {
            deploymentInfo = null;

            if (IsKilnRequest(payload))
            {
                deploymentInfo = GetDeploymentInfo(payload, targetBranch);
                return deploymentInfo == null ? DeployAction.NoOp : DeployAction.ProcessDeployment;
            }

            return DeployAction.UnknownPayload;
        }

        /// <summary>
        /// Verifies the payload to see if it's coming from Kiln.
        /// </summary>
        /// <returns>
        /// true if it's assumed to be from kiln; otherwise false.
        /// </returns>
        public bool IsKilnRequest(JObject payload)
        {
            var repository = payload.Value<JObject>("repository");
            if (repository == null)
            {
                return false;
            }

            var url = repository.Value<string>("url");
            if (url == null)
            {
                return false;
            }

            var pattern = _settings.GetValue("kiln.domain") ?? @"\.kilnhg\.com";
            if (!Regex.IsMatch(url, pattern, RegexOptions.IgnoreCase))
            {
                return false;
            }

            return true;
        }

        private DeploymentInfo GetDeploymentInfo(JObject payload, string targetBranch)
        {
            var repository = payload.Value<JObject>("repository");
            var commits = payload.Value<JArray>("commits");

            // Identify the last commit for the target branch.
            JObject targetCommit = (from commit in commits
                                    where targetBranch.Equals(commit.Value<string>("branch"), StringComparison.OrdinalIgnoreCase)
                                    orderby commit.Value<int>("revision") descending
                                    select (JObject)commit).FirstOrDefault();

            if (targetCommit == null)
            {
                return null;
            }

            string accessToken = _settings.GetValue("kiln.accesstoken");

            var author = targetCommit.Value<string>("author");

            // if the commit was via an access token we don't want to show that for security reasons
            Guid authorGuid;
            if (Guid.TryParse(author, out authorGuid))
            {
                author = "System Account";
            }

            var info = new DeploymentInfo
            {
                Deployer = "Kiln",
                RepositoryUrl = repository.Value<string>("url"),
                RepositoryType = RepositoryType.Mercurial,
                IsContinuous = true,
                TargetChangeset = new ChangeSet(
                    id: targetCommit.Value<string>("id"),
                    authorName: ParseNameFromAuthor(author),
                    authorEmail: ParseEmailFromAuthor(author),
                    message: (targetCommit.Value<string>("message") ?? String.Empty).Trim(),
                    timestamp: new DateTimeOffset(DateTime.Parse(targetCommit.Value<string>("timestamp"), CultureInfo.InvariantCulture), TimeSpan.Zero)
                    )
            };

            bool isPrivate = !String.IsNullOrWhiteSpace(accessToken); // assume a private repo if an access token is provided
            if (isPrivate)
            {
                var uri = new UriBuilder(info.RepositoryUrl)
                {
                    Scheme = "https",
                    Port = -1,
                    UserName = accessToken,
                    Password = "kudu" // kiln doesn't use the password when using an access token
                };

                info.RepositoryUrl = uri.ToString();
            }

            return info;
        }

        /// <summary>
        /// Tries to parse the user's name from a string formatted like Author Name &lt;email@address.com&gt;
        /// </summary>
        /// <returns>
        /// The author's name if it's found; otherwise the original value.
        /// </returns>
        public static string ParseNameFromAuthor(string author)
        {
            if (String.IsNullOrWhiteSpace(author))
            {
                return null;
            }

            Match match = AuthorPattern.Match(author);

            string name = match.Groups["name"].Value.Trim();
            if (String.IsNullOrWhiteSpace(name))
            {
                return author.Trim();
            }

            return name;
        }

        /// <summary>
        /// Tries to parse the user's email address from a string formatted like Author Name &lt;email@address.com&gt;
        /// </summary>
        /// <returns>
        /// The author's email address if it's found; otherwise null.
        /// </returns>
        public static string ParseEmailFromAuthor(string author)
        {
            if (String.IsNullOrWhiteSpace(author))
            {
                return null;
            }

            Match match = EmailPattern.Match(author);

            string email = match.Groups["email"].Value.Trim();
            if (String.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            return email;
        }
    }
}
