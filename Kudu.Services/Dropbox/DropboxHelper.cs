using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kudu.Contracts.Dropbox;
using Kudu.Contracts.Settings;
using Kudu.Contracts.Tracing;
using Kudu.Core;
using Kudu.Core.SourceControl;

namespace Kudu.Services.Dropbox
{
    public class DropboxHelper
    {
        public const string Dropbox = "dropbox";
        public const string CursorKey = "dropbox_cursor";
        public const string UserNameKey = "dropbox_username";
        public const string EmailKey = "dropbox_email";

        private const string DropboxApiContentUri = "https://api-content.dropbox.com/";
        private const string SandboxFilePath = "1/files/sandbox";
        private const int MaxConcurrentRequests = 5;

        private readonly ITracer _tracer;
        private readonly IServerRepository _repository;
        private readonly IDeploymentSettingsManager _settings;
        private readonly IEnvironment _environment;

        public DropboxHelper(ITracer tracer, 
                             IServerRepository repository, 
                             IDeploymentSettingsManager settings, 
                             IEnvironment environment)
        {
            _tracer = tracer;
            _repository = repository;
            _settings = settings;
            _environment = environment;
        }

        public void Sync(DropboxDeployInfo info, string branch)
        {
            if (_settings.GetValue(CursorKey) != info.OldCursor)
            {
                throw new InvalidOperationException(Resources.Error_MismatchDropboxCursor);
            }

            if (!IsEmptyRepo())
            {
                // git checkout --force <branch>
                _repository.Update(branch);
            }

            string prefix = "Partially"; 
            try
            {
                using (_tracer.Step("Synch with Dropbox"))
                {
                    // Sync dropbox => repository directory
                    ApplyChanges(info);
                }

                prefix = "Successfully";
            }
            finally
            {
                // Commit anyway even partial change
                _repository.Commit(prefix + " sync with dropbox at " + DateTime.UtcNow.ToString("g"), GetAuthor());
            }

            // Save new dropboc cursor
            _tracer.Trace("Update dropbox cursor");
            _settings.SetValue(CursorKey, info.NewCursor);
        }

        private bool IsEmptyRepo()
        {
            try
            {
                return string.IsNullOrEmpty(_repository.CurrentId);
            }
            catch (Exception)
            {
                return true;
            }
        }

        private string GetAuthor()
        {
            string userName = _settings.GetValue(UserNameKey);
            string email = _settings.GetValue(EmailKey);
            if (!String.IsNullOrEmpty(userName) && !String.IsNullOrEmpty(email))
            {
                return String.Format("{0} <{1}>", userName, email);
            }

            return null;
        }

        private void ApplyChanges(DropboxDeployInfo info)
        {
            Semaphore sem = new Semaphore(MaxConcurrentRequests, MaxConcurrentRequests);
            List<Task> tasks = new List<Task>();
            string parent = info.Path.TrimEnd('/') + '/'; 

            foreach (DropboxDeltaInfo delta in info.Deltas)
            {
                if (!delta.Path.StartsWith(parent, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var path = delta.Path;
                if (delta.IsDeleted)
                {
                    SafeDelete(parent, path);
                }
                else if (delta.IsDirectory)
                {
                    SafeCreateDir(parent, path);
                }
                else
                {
                    DateTime modified = DateTime.Parse(delta.Modified).ToUniversalTime();
                    if (!IsFileChanged(parent, path, modified))
                    {
                        _tracer.Trace("file unchanged {0}", path);
                        continue;
                    }

                    // throttle concurrent get file dropbox
                    sem.WaitOne();
                    Task task;
                    try
                    {
                        task = GetFileAsync(info, delta).Then(stream =>
                        {
                            using (stream)
                            {
                                SafeWriteFile(parent, path, stream, modified);
                            }
                        }).Catch(catchInfo =>
                        {
                            _tracer.TraceError(catchInfo.Exception);
                            return catchInfo.Throw();
                        })
                        .Finally(() =>
                        {
                            sem.Release();
                        });
                    }
                    catch (Exception ex)
                    {
                        sem.Release();
                        _tracer.TraceError(ex);
                        Task.WaitAll(tasks.ToArray());
                        throw;
                    }

                    tasks.Add(task);
                }
            }

            Task.WaitAll(tasks.ToArray());
        }

        private bool IsFileChanged(string parent, string path, DateTime modified)
        {
            var file = new System.IO.FileInfo(GetRepositoryPath(parent, path));
            return !file.Exists || file.LastWriteTimeUtc != modified;
        }

        private void SafeDelete(string parent, string path)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _tracer.Trace("del " + path);
            }
            else if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, recursive: true);
                _tracer.Trace("rmdir /s " + path);
            }
        }

        private void SafeCreateDir(string parent, string path)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
                _tracer.Trace("del " + path);
            }

            if (!Directory.Exists(fullPath))
            {
                Directory.CreateDirectory(fullPath);
                _tracer.Trace("mkdir " + path);
            }
        }

        private void SafeWriteFile(string parent, string path, Stream stream, DateTime modified)
        {
            var fullPath = GetRepositoryPath(parent, path);
            if (Directory.Exists(fullPath))
            {
                Directory.Delete(fullPath, true);
                _tracer.Trace("rmdir /s " + path);
            }

            using (FileStream fs = new FileStream(fullPath, FileMode.Create, FileAccess.Write))
            {
                stream.CopyTo(fs);
                _tracer.Trace("write {0,6} bytes to {1}", fs.Length, path);
            }

            File.SetLastWriteTimeUtc(fullPath, modified);
        }

        private string GetRepositoryPath(string parent, string path)
        {
            string relativePath = path.Substring(parent.Length).Replace('/', '\\');
            return Path.Combine(_environment.RepositoryPath, relativePath);
        }

        private Task<Stream> GetFileAsync(DropboxDeployInfo info, DropboxDeltaInfo delta)
        {
            var parameters = new Dictionary<string, string>
            {
                { "oauth_consumer_key", info.ConsumerKey },
                { "oauth_signature_method", info.SignatureMethod },
                { "oauth_timestamp", info.TimeStamp },
                { "oauth_nonce", delta.Nonce },
                { "oauth_version", info.OAuthVersion },
                { "oauth_token", info.Token },
                { "oauth_signature", delta.Signature }
            };

            var sb = new StringBuilder();
            foreach (var key in parameters.Keys)
            {
                if (sb.Length != 0)
                {
                    sb.Append(',');
                }
                sb.AppendFormat("{0}=\"{1}\"", key, parameters[key]);
            }

            var client = new HttpClient();
            client.BaseAddress = new Uri(DropboxApiContentUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("OAuth", sb.ToString());
            return client.GetAsync(SandboxFilePath + delta.Path).Then(response =>
            {
                return response.EnsureSuccessStatusCode().Content.ReadAsStreamAsync();
            }).Finally(() => client.Dispose());
        }
    }
}
