using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Web.Http.Routing;
using Kudu.Contracts.Editor;
using Kudu.Core.Infrastructure;

namespace Kudu.Services.Infrastructure
{
    public static class VfsSpecialFolders
    {
        private const string SystemDriveFolder = "SystemDrive";
        private const string LocalSiteRootFolder = "LocalSiteRoot";

        private static string _systemDrivePath;
        private static string _localSiteRootPath;

        public static string SystemDrivePath
        {
            get
            {
                if (_systemDrivePath == null)
                {
                    _systemDrivePath = Environment.GetEnvironmentVariable(SystemDriveFolder) ?? String.Empty;
                }

                return _systemDrivePath;
            }
            // internal for testing purpose
            internal set
            {
                _systemDrivePath = value;
            }
        }

        public static string LocalSiteRootPath
        {
            get
            {
                if (_localSiteRootPath == null)
                {
                    // only light up in Azure env
                    string tmpPath = Environment.GetEnvironmentVariable("TMP");
                    if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_SITE_NAME")) &&
                        !String.IsNullOrEmpty(tmpPath))
                    {
                        _localSiteRootPath = Path.GetDirectoryName(tmpPath);
                    }
                }

                return _localSiteRootPath;
            }
            // internal for testing purpose
            internal set
            {
                _localSiteRootPath = value;
            }
        }

        public static IEnumerable<VfsStatEntry> GetEntries(string baseAddress, string query)
        {
            if (!String.IsNullOrEmpty(SystemDrivePath))
            {
                var dir = FileSystemHelpers.DirectoryInfoFromDirectoryName(SystemDrivePath + Path.DirectorySeparatorChar);
                yield return new VfsStatEntry
                {
                    Name = SystemDriveFolder,
                    MTime = dir.LastWriteTimeUtc,
                    CRTime = dir.CreationTimeUtc,
                    Mime = "inode/shortcut",
                    Href = baseAddress + Uri.EscapeUriString(SystemDriveFolder + VfsControllerBase.UriSegmentSeparator) + query,
                    Path = dir.FullName
                };
            }

            if (!String.IsNullOrEmpty(LocalSiteRootPath))
            {
                var dir = FileSystemHelpers.DirectoryInfoFromDirectoryName(LocalSiteRootPath);
                yield return new VfsStatEntry
                {
                    Name = LocalSiteRootFolder,
                    MTime = dir.LastWriteTimeUtc,
                    CRTime = dir.CreationTimeUtc,
                    Mime = "inode/shortcut",
                    Href = baseAddress + Uri.EscapeUriString(LocalSiteRootFolder + VfsControllerBase.UriSegmentSeparator) + query,
                    Path = dir.FullName
                };
            }
        }

        public static bool TryHandleRequest(HttpRequestMessage request, string path, out HttpResponseMessage response)
        {
            response = null;
            if (String.Equals(path, SystemDrivePath, StringComparison.OrdinalIgnoreCase))
            {
                response = request.CreateResponse(HttpStatusCode.TemporaryRedirect);
                UriBuilder location = new UriBuilder(UriHelper.GetRequestUri(request));
                location.Path += "/";
                response.Headers.Location = location.Uri;
            }

            return response != null;
        }

        // this resolves the special folders such as SystemDrive or LocalSiteRoot
        public static bool TryParse(IHttpRouteData routeData, out string result)
        {
            result = null;

            string path = routeData != null ? routeData.Values["path"] as string : null;
            if (!String.IsNullOrEmpty(path))
            {
                if (String.Equals(path, SystemDriveFolder, StringComparison.OrdinalIgnoreCase) ||
                    path.IndexOf(SystemDriveFolder + VfsControllerBase.UriSegmentSeparator, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (!String.IsNullOrEmpty(SystemDrivePath))
                    {
                        string relativePath = path.Substring(SystemDriveFolder.Length);
                        if (String.IsNullOrEmpty(relativePath))
                        {
                            result = SystemDrivePath;
                        }
                        else
                        {
                            result = Path.GetFullPath(SystemDrivePath + relativePath);
                        }
                    }
                }
                else if (String.Equals(path, LocalSiteRootFolder, StringComparison.OrdinalIgnoreCase) ||
                    path.IndexOf(LocalSiteRootFolder + VfsControllerBase.UriSegmentSeparator, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    if (!String.IsNullOrEmpty(LocalSiteRootPath))
                    {
                        string relativePath = path.Substring(LocalSiteRootFolder.Length);
                        if (String.IsNullOrEmpty(relativePath))
                        {
                            result = LocalSiteRootPath;
                        }
                        else
                        {
                            result = Path.GetFullPath(LocalSiteRootPath + relativePath);
                        }
                    }
                }
            }
            
            return result != null;
        }
    }
}
