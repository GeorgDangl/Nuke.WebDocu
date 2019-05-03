using System;
using System.Linq;
using Nuke.Common;
using static Nuke.Common.Tools.DotNet.DotNetTasks;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.EnvironmentInfo;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using Nuke.WebDocu;
using System.IO;
using Nuke.Common.Git;
using static Nuke.Common.Tools.Xunit.XunitTasks;
using Nuke.Common.Tools.Xunit;
using static Nuke.DocFX.DocFXTasks;
using Nuke.DocFX;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities;
using Nuke.Common.Tooling;
using Nuke.GitHub;
using static Nuke.GitHub.ChangeLogExtensions;
using static Nuke.GitHub.GitHubTasks;
using static Nuke.Common.ChangeLog.ChangelogTasks;
using Nuke.Azure.KeyVault;

class Build : NukeBuild
{
    // Console application entry. Also defines the default target.
    public static int Main() => Execute<Build>(x => x.Compile);

    [KeyVaultSettings(
        BaseUrlParameterName = nameof(KeyVaultBaseUrl),
        ClientIdParameterName = nameof(KeyVaultClientId),
        ClientSecretParameterName = nameof(KeyVaultClientSecret))]
    readonly KeyVaultSettings KeyVaultSettings;

    [Parameter] readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    [Parameter] string KeyVaultBaseUrl;
    [Parameter] string KeyVaultClientId;
    [Parameter] string KeyVaultClientSecret;
    [GitVersion] readonly GitVersion GitVersion;
    [GitRepository] readonly GitRepository GitRepository;

    [KeyVaultSecret] string DocuBaseUrl;
    [KeyVaultSecret] string GitHubAuthenticationToken;
    [KeyVaultSecret] string PublicMyGetSource;
    [KeyVaultSecret] string PublicMyGetApiKey;
    [KeyVaultSecret("NukeWebDocu-DocuApiKey")] string DocuApiKey;
    [KeyVaultSecret] string NuGetApiKey;

    string DocFxFile => RootDirectory / "docfx.json";

    string ChangeLogFile => RootDirectory / "CHANGELOG.md";
    AbsolutePath OutputDirectory => RootDirectory / "output";
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath TestsDirectory => RootDirectory / "test";

    Target Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(OutputDirectory);
        });

    Target Restore => _ => _
        .DependsOn(Clean)
        .Executes(() =>
        {
            DotNetRestore();
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DotNetBuild(x => x
                .SetConfiguration(Configuration)
                .EnableNoRestore()
                .SetFileVersion(GitVersion.GetNormalizedFileVersion())
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetInformationalVersion(GitVersion.InformationalVersion));
        });

    Target Pack => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var changeLog = GetCompleteChangeLog(ChangeLogFile)
                .EscapeStringPropertyForMsBuild();

            DotNetPack(x => x
                .SetConfiguration(Configuration)
                .SetPackageReleaseNotes(changeLog)
                .SetTitle("WebDocu for NUKE Build - www.dangl-it.com")
                .EnableNoBuild()
                .SetOutputDirectory(OutputDirectory)
                .SetVersion(GitVersion.NuGetVersion));
        });

    Target Push => _ => _
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrWhiteSpace(PublicMyGetSource))
        .Requires(() => !string.IsNullOrWhiteSpace(PublicMyGetApiKey))
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            GlobFiles(OutputDirectory, "*.nupkg").NotEmpty()
                .Where(x => !x.EndsWith("symbols.nupkg"))
                .ForEach(x => DotNetNuGetPush(s => s
                    .SetTargetPath(x)
                    .SetSource(PublicMyGetSource)
                    .SetApiKey(PublicMyGetApiKey)));

            if (GitVersion.BranchName.Equals("master") || GitVersion.BranchName.Equals("origin/master"))
            {
                // Stable releases are published to NuGet
                GlobFiles(OutputDirectory, "*.nupkg").NotEmpty()
                    .Where(x => !x.EndsWith("symbols.nupkg"))
                    .ForEach(x => DotNetNuGetPush(s => s
                        .SetTargetPath(x)
                        .SetSource("https://api.nuget.org/v3/index.json")
                        .SetApiKey(NuGetApiKey)));
            }
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(x => x
                .SetNoBuild(true)
                .SetProjectFile(RootDirectory / "test" / "Nuke.WebDocu.Tests")
                .SetTestAdapterPath(".")
                .SetLogger($"xunit;LogFilePath={OutputDirectory / "tests.xml"}"));
        });

    Target BuildDocFxMetadata => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            DocFXMetadata(x => x.AddProjects(DocFxFile));
        });

    Target BuildDocumentation => _ => _
        .DependsOn(Clean)
        .DependsOn(BuildDocFxMetadata)
        .Executes(() =>
        {
            // Using README.md as index.md
            if (File.Exists(RootDirectory / "index.md"))
            {
                File.Delete(RootDirectory / "index.md");
            }

            File.Copy(RootDirectory / "README.md", RootDirectory / "index.md");

            DocFXBuild(x => x.SetConfigFile(DocFxFile));

            File.Delete(RootDirectory / "index.md");
        });

    Target UploadDocumentation => _ => _
        .DependsOn(Push) // To have a relation between pushed package version and published docs version
        .DependsOn(BuildDocumentation)
        .Requires(() => !string.IsNullOrWhiteSpace(DocuApiKey))
        .Requires(() => !string.IsNullOrWhiteSpace(DocuBaseUrl))
        .Executes(() =>
        {
            WebDocuTasks.WebDocu(s => s.SetDocuBaseUrl(DocuBaseUrl)
                .SetDocuApiKey(DocuApiKey)
                .SetSourceDirectory(OutputDirectory / "docs")
                .SetVersion(GitVersion.NuGetVersion));
        });

    Target PublishGitHubRelease => _ => _
        .DependsOn(Pack)
        .Requires(() => !string.IsNullOrWhiteSpace(GitHubAuthenticationToken))
        .OnlyWhenDynamic(() => GitVersion.BranchName.Equals("master") || GitVersion.BranchName.Equals("origin/master"))
        .Executes(async () =>
        {
            var releaseTag = $"v{GitVersion.MajorMinorPatch}";

            var changeLogSectionEntries = ExtractChangelogSectionNotes(ChangeLogFile);
            var latestChangeLog = changeLogSectionEntries
                .Aggregate((c, n) => c + Environment.NewLine + n);
            var completeChangeLog = $"## {releaseTag}" + Environment.NewLine + latestChangeLog;

            var repositoryInfo = GetGitHubRepositoryInfo(GitRepository);

            await PublishRelease(x => x
                    .SetArtifactPaths(GlobFiles(RootDirectory, "*.nupkg").NotEmpty().ToArray())
                    .SetCommitSha(GitVersion.Sha)
                    .SetReleaseNotes(completeChangeLog)
                    .SetRepositoryName(repositoryInfo.repositoryName)
                    .SetRepositoryOwner(repositoryInfo.gitHubOwner)
                    .SetTag(releaseTag)
                    .SetToken(GitHubAuthenticationToken)
                );
        });
}
