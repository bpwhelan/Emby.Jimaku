/*namespace EmbyPluginUiDemo.UI
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    using EmbyPluginUiDemo.Storage;
    using EmbyPluginUiDemo.UI.Basics;
    using EmbyPluginUiDemo.UIBaseClasses;

    using MediaBrowser.Controller;
    using MediaBrowser.Model.Plugins;
    using MediaBrowser.Model.Plugins.UI;
    using MediaBrowser.Model.Plugins.UI.Views;

    internal class MainPageController : ControllerBase
    {
        private readonly PluginInfo pluginInfo;
        private readonly BasicsOptionsStore basicsOptionsStore;
        private readonly List<IPluginUIPageController> tabPages = new List<IPluginUIPageController>();

        /// <summary>Initializes a new instance of the <see cref="ControllerBase" /> class.</summary>
        /// <param name="pluginInfo">The plugin information.</param>
        /// <param name="applicationHost"></param>
        /// <param name="basicsOptionsStore"></param>
        public MainPageController(PluginInfo pluginInfo, IServerApplicationHost applicationHost, BasicsOptionsStore basicsOptionsStore)
            : base(pluginInfo.Id)
        {
            this.pluginInfo = pluginInfo;
            this.basicsOptionsStore = basicsOptionsStore;
            this.PageInfo = new PluginPageInfo
                            {
                                Name = "JimakuSubtitleProvider",
                                EnableInMainMenu = true,
                                DisplayName = "Jimaku",
                                MenuIcon = "vpn_key",
                                IsMainConfigPage = true
                            };
        }

        public override PluginPageInfo PageInfo { get; }

        public override Task<IPluginUIView> CreateDefaultPageView()
        {
            IPluginUIView view = new BasicsPageView(this.pluginInfo, this.basicsOptionsStore);
            return Task.FromResult(view);
        }

        public IReadOnlyList<IPluginUIPageController> TabPageControllers => this.tabPages.AsReadOnly();
    }
}
*/