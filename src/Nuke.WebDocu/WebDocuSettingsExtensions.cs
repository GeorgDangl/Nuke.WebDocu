using JetBrains.Annotations;
using Nuke.Core.Tooling;
using System;
using System.Diagnostics.CodeAnalysis;

namespace Nuke.WebDocu
{
    [PublicAPI]
    [ExcludeFromCodeCoverage]
    public static class WebDocuSettingsExtensions
    {
        [Pure]
        public static WebDocuSettings SetSourceDirectory(this WebDocuSettings toolSettings, string sourceDirectory)
            => toolSettings.CloneAndModify(s => s.SourceDirectory = sourceDirectory);

        [Pure]
        public static WebDocuSettings SetDocuApiKey(this WebDocuSettings toolSettings, string docuApiKey)
            => toolSettings.CloneAndModify(s => s.DocuApiKey = docuApiKey);

        [Pure]
        public static WebDocuSettings SetDocuApiEndpoint(this WebDocuSettings toolSettings, string docuApiEndpoint)
            => toolSettings.CloneAndModify(s => s.DocuApiEndpoint = docuApiEndpoint);

        [Pure]
        public static WebDocuSettings SetVersion(this WebDocuSettings toolSettings, string version)
            => toolSettings.CloneAndModify(s => s.Version = version);

        private static WebDocuSettings CloneAndModify(this WebDocuSettings toolSettings, Action<WebDocuSettings> modifyAction)
        {
            toolSettings = toolSettings.NewInstance();
            modifyAction(toolSettings);
            return toolSettings;
        }
    }
}
