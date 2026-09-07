using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Jimakufin
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public string ApiKey { get; set; } = string.Empty;
    }

    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public Plugin(IApplicationPaths paths, IXmlSerializer serializer, ILogger<Plugin> logger) : base(paths, serializer)
        {
            Instance = this;
            logger.LogInformation("Jimakufin Jellyfin plugin {Version} loaded (single DLL); API key configured: {HasApiKey}",
                typeof(Plugin).Assembly.GetName().Version, !string.IsNullOrWhiteSpace(Configuration.ApiKey));
        }
        public static Plugin Instance { get; private set; }
        public override string Name => "Jimakufin";
        public override string Description => "Japanese episode subtitles from Jimaku.cc";
        public override Guid Id => new Guid("7FAFDAEF-3F55-4FD7-B14D-BBFCDAA801EE");
        public IEnumerable<PluginPageInfo> GetPages() => new[]
        {
            new PluginPageInfo { Name = "Jimakufin", EmbeddedResourcePath = "Jellyfin.Jimakufin.Configuration.config.html" }
        };
    }
}

