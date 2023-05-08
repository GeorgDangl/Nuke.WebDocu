> **Archival Information**: This repository is archived, it was moved as a sub project into https://github.com/GeorgDangl/Nuke.GitHub

# Nuke.WebDocu

[![Build Status](https://jenkins.dangl.me/buildStatus/icon?job=GeorgDangl%2FNuke.WebDocu%2Fdevelop)](https://jenkins.dangl.me/job/GeorgDangl/job/Nuke.WebDocu/job/develop/)

![NuGet](https://img.shields.io/nuget/v/Nuke.WebDocu.svg)
[![MyGet](https://img.shields.io/myget/dangl/v/Nuke.WebDocu.svg)]()

This plugin provides a task to upload documentation packages to [WebDocu sites](https://github.com/GeorgDangl/WebDocu).
It's written for the [NUKE Build](https://github.com/nuke-build/nuke) system.

[Link to documentation](https://docs.dangl-it.com/Projects/Nuke.WebDocu).

[Changelog](./Changelog.md)

## CI Builds

All builds are available on MyGet:

    https://www.myget.org/F/dangl/api/v2
    https://www.myget.org/F/dangl/api/v3/index.json

## Example

When publishing to WebDocu, you have to include the version of the docs.

### Getting the Version from Generated NuGet Packages

```
Target UploadDocumentation => _ => _
    .DependsOn(BuildDocumentation)
    .Requires(() => DocuApiKey)
    .Requires(() => DocuBaseUrl)
    .Executes(() =>
    {
        WebDocuTasks.WebDocu(s =>
        {
            var packageVersion = GlobFiles(OutputDirectory, "*.nupkg").NotEmpty()
                .Where(x => !x.EndsWith("symbols.nupkg"))
                .Select(Path.GetFileName)
                .Select(x => WebDocuTasks.GetVersionFromNuGetPackageFilename(x, "Nuke.WebDeploy"))
                .First();

            return s.SetDocuBaseUrl(DocuBaseUrl)
                    .SetDocuApiKey(DocuApiKey)
                    .SetSourceDirectory(OutputDirectory / "docs")
                    .SetVersion(packageVersion);
        });
    });
```

### Getting the Version from GitVersion

```
Target UploadDocumentation => _ => _
    .DependsOn(Push) // To have a relation between pushed package version and published docs version
    .DependsOn(BuildDocumentation)
    .Requires(() => DocuApiKey)
    .Requires(() => DocuApiEndpoint)
    .Executes(() =>
    {
        WebDocuTasks.WebDocu(s => s.SetDocuApiEndpoint(DocuApiEndpoint)
            .SetDocuApiKey(DocuApiKey)
            .SetSourceDirectory(OutputDirectory / "docs")
            .SetVersion(GitVersion.NuGetVersion));
    });
```

The `DocuApiEndpoint` should look like this:

    https://docs.dangl-it.com/API/Projects/Upload

## License

[This project is available under the MIT license.](LICENSE.md)
