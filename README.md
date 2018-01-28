# Nuke.WebDocu

[![Build Status](https://jenkins.dangl.me/buildStatus/icon?job=Nuke.WebDocu.Tests)](https://jenkins.dangl.me/job/Nuke.WebDocu.Tests/)
[![MyGet](https://img.shields.io/myget/dangl/v/Nuke.WebDocu.svg)]()

This plugin provides a task to upload documentation packages to [WebDocu sites](https://github.com/GeorgDangl/WebDocu).
It's written for the [NUKE Build](https://github.com/nuke-build/nuke) system.

[Link to documentation](https://docs.dangl-it.com/Projects/Nuke.WebDocu).

## CI Builds

All builds are available on MyGet:

    https://www.myget.org/F/dangl/api/v2
    https://www.myget.org/F/dangl/api/v3/index.json

## Example

```
Target UploadDocumentation => _ => _
    .DependsOn(BuildDocumentation)
    .Requires(() => DocuApiKey)
    .Requires(() => DocuApiEndpoint)
    .Executes(() =>
    {
        WebDocuTasks.WebDocu(s =>
        {
            var packageVersion = GlobFiles(OutputDirectory, "*.nupkg").NotEmpty()
                .Where(x => !x.EndsWith("symbols.nupkg"))
                .Select(Path.GetFileName)
                .Select(x => WebDocuTasks.GetVersionFromNuGetPackageFilename(x, "Nuke.WebDeploy"))
                .First();

            return s.SetDocuApiEndpoint(DocuApiEndpoint)
                    .SetDocuApiKey(DocuApiKey)
                    .SetSourceDirectory(OutputDirectory / "docs")
                    .SetVersion(packageVersion);
        });
    });
```

The `DocuApiEndpoint` should look like this:

    https://docs.dangl-it.com/API/Projects/Upload

## License

[This project is available under the MIT license.](LICENSE.md)
