using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using MediaBrowser.Controller.Subtitles;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Jimakufin
{
    // Jellyfin 10.11 injects IEnumerable<ISubtitleProvider> into its subtitle manager.
    // This hook is discovered separately from the plugin's configuration entry point.
    public class PluginServiceRegistrator : IPluginServiceRegistrator
    {
        public void RegisterServices(IServiceCollection services, IServerApplicationHost applicationHost)
        {
            services.AddHttpClient();
            services.AddSingleton<ISubtitleProvider, JimakuSubtitleProvider>();
        }
    }
}
