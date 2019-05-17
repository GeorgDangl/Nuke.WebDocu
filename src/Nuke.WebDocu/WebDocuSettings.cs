using JetBrains.Annotations;
using Nuke.Common.Tooling;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Nuke.WebDocu
{
    [PublicAPI]
    [ExcludeFromCodeCoverage]
    [Serializable]
    public class WebDocuSettings : ISettingsEntity
    {
        public virtual string SourceDirectory { get; internal set; }
        public virtual string DocuApiKey { get; internal set; }
        public virtual string DocuBaseUrl { get; internal set; }
        public virtual string Version { get; internal set; }
        public virtual string MarkdownChangelog { get; internal set; }
        public virtual string[] AssetFilePaths { get; set; }
    }
}
