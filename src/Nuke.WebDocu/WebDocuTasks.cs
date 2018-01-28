using Nuke.Core.Tooling;
using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;

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
                var request = new HttpRequestMessage(HttpMethod.Post, settings.DocuApiEndpoint);
                var requestContent = new MultipartFormDataContent();
                requestContent.Add(new StringContent(settings.DocuApiKey), "ApiKey");
                requestContent.Add(new StringContent(settings.Version), "Version");
                requestContent.Add(new StreamContent(docsStream), "ProjectPackage", "docs.zip");
                request.Content = requestContent;
                var response = await new HttpClient().SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Upload failed");
                }
            }
        }
    }
}
