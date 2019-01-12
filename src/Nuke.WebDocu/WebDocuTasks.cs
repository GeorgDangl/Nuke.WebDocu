using Nuke.Common.Tooling;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Nuke.Common;
using Nuke.Common.BuildServers;
using static Nuke.Common.IO.PathConstruction;
using System.Linq;

namespace Nuke.WebDocu
{
    public static class WebDocuTasks
    {
        public static void WebDocu(Configure<WebDocuSettings> configurator)
        {
            var settings = configurator.InvokeSafe(new WebDocuSettings());

            // Create zip package
            var tempPath = Path.GetTempFileName();
            File.Delete(tempPath);
            FixGitUrlsIfInJenkinsJob(settings.SourceDirectory);
            ZipFile.CreateFromDirectory(settings.SourceDirectory, tempPath);

            // Upload package to docs
            UploadToDanglDocu(tempPath, settings)
                .ConfigureAwait(false)
                .GetAwaiter()
                .GetResult();

            File.Delete(tempPath);
        }

        public static string GetVersionFromNuGetPackageFilename(string nuGetPackagePath, string projectName)
        {
            return Path.GetFileName(nuGetPackagePath)
                .Replace(".nupkg", string.Empty)
                .Replace(projectName + ".", string.Empty);
        }

        static async Task UploadToDanglDocu(string zipPackage, WebDocuSettings settings)
        {
            using (var docsStream = File.OpenRead(zipPackage))
            {
                var request = new HttpRequestMessage(HttpMethod.Post, settings.DocuBaseUrl.TrimEnd('/') + "/API/Projects/Upload");
                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new StringContent(settings.DocuApiKey), "ApiKey");
                requestContent.Add(new StringContent(settings.Version), "Version");
                requestContent.Add(new StreamContent(docsStream), "ProjectPackage", "docs.zip");
                request.Content = requestContent;
                var response = await new HttpClient().SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Upload failed with status code: " + response.StatusCode + Environment.NewLine + await response.Content.ReadAsStringAsync());
                }
            }

            if (settings.AssetFilePaths?.Any() == true)
            {
                foreach (var assetFilePath in settings.AssetFilePaths)
                {
                    using (var assetStream = File.OpenRead(assetFilePath))
                    {
                        var fileName = NormalizeFilename(assetFilePath);
                        var request = new HttpRequestMessage(HttpMethod.Post, settings.DocuBaseUrl.TrimEnd('/') + "/API/ProjectAssets/Upload");
                        var requestContent = new MultipartFormDataContent();
                        requestContent.Add(new StringContent(settings.DocuApiKey), "ApiKey");
                        requestContent.Add(new StringContent(settings.Version), "Version");
                        requestContent.Add(new StreamContent(assetStream), "AssetFile", fileName);
                        request.Content = requestContent;
                        var response = await new HttpClient().SendAsync(request);
                        if (!response.IsSuccessStatusCode)
                        {
                            throw new Exception("Upload failed with status code: " + response.StatusCode + Environment.NewLine + await response.Content.ReadAsStringAsync());
                        }
                    }
                }
            }
        }

        static string NormalizeFilename(string originalFilename)
        {
            if (string.IsNullOrWhiteSpace(originalFilename))
            {
                return string.Empty;
            }

            var filename = Path.GetFileName(originalFilename).Trim();
            return filename;
        }

        static void FixGitUrlsIfInJenkinsJob(string sourceDirectory)
        {
            var jenkinsInstance = Jenkins.Instance;
            if (jenkinsInstance == null)
            {
                Logger.Log("Not inside a Jenkins job, \"View Source\" links will not be changed");
                return;
            }
            Logger.Log("Inside a Jenkins job, \"View Source\" links will be changed to point to the commit hash");

            // In Jenkins, the Git branch is something like "origin/dev", which should
            // only be "dev" to generate correct urls.
            // It's just replaced by the actual commit hash as to preserve the version context

            foreach (var htmlFile in GlobFiles(sourceDirectory, "**/*.html"))
            {
                var originalContent = File.ReadAllText(htmlFile);
                var correctedText = originalContent
                    .Replace($"blob/{jenkinsInstance.GitBranch}", $"blob/{jenkinsInstance.GitCommit}")
                    .Replace($"blob/heads/{jenkinsInstance.GitBranch}", $"blob/{jenkinsInstance.GitCommit}");
                File.WriteAllText(htmlFile, correctedText);
            }
        }
    }
}
