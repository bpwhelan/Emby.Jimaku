// <copyright file="PluginUIDemoPlugin.cs" company="Emby LLC">
// Copyright © 2022 - Emby LLC. All rights reserved.
// </copyright>

namespace Emby.Jimaku
{
    using MediaBrowser.Common.Plugins;
    using MediaBrowser.Controller;
    using MediaBrowser.Controller.Plugins;
    using MediaBrowser.Model.Drawing;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.Plugins;
    using System;
    using System.IO;

    /// <summary>
    /// The plugin.
    /// </summary>
    public class Plugin : BasePluginSimpleUI<PluginOptions>, IHasThumbImage
    {
        private readonly IServerApplicationHost applicationHost;

        /// <summary>The Plugin ID.</summary>
        private readonly Guid id = new Guid("7FAFDAEF-3F55-4FD7-B14D-BBFCDAA801EE");

        private readonly ILogger logger;

        public static PluginOptions Options = new PluginOptions();

        /// <summary>Initializes a new instance of the <see cref="Plugin" /> class.</summary>
        /// <param name="applicationHost">The application host.</param>
        /// <param name="logManager">The log manager.</param>
        public Plugin(IServerApplicationHost applicationHost, ILogManager logManager) : base(applicationHost)
        {
            this.applicationHost = applicationHost;
            this.logger = logManager.GetLogger(this.Name);
            Plugin.Options = GetOptions();
        }

        /// <summary>Gets the description.</summary>
        /// <value>The description.</value>
        public override string Description => "This plugin acts as a subtitle provider from Jimaku";

        /// <summary>Gets the unique id.</summary>
        /// <value>The unique id.</value>
        public override Guid Id => this.id;

        /// <summary>Gets the name of the plugin</summary>
        /// <value>The name.</value>
        public override sealed string Name => PluginName;

        public static string PluginName = "Jimaku";

        /// <summary>Gets the thumb image format.</summary>
        /// <value>The thumb image format.</value>
        public ImageFormat ThumbImageFormat => ImageFormat.Jpg;

        /// <summary>Gets the thumb image.</summary>
        /// <returns>An image stream.</returns>
        public Stream GetThumbImage()
        {
            var type = this.GetType();
            return type.Assembly.GetManifestResourceStream(type.Namespace + ".jimaku.png");
        }

        /// <summary>
        /// Completely overwrites the current configuration with a new copy
        /// Returns true or false indicating success or failure
        /// </summary>
        /// <param name="configuration">The configuration.</param>
        /// <exception cref="System.ArgumentNullException">configuration</exception>
        public void UpdateConfiguration(BasePluginConfiguration configuration)
        {
        }


        /// <summary>
        /// Gets the plugin's configuration
        /// </summary>
        /// <value>The configuration.</value>
        public BasePluginConfiguration Configuration { get; } = new BasePluginConfiguration();

        public void SetStartupInfo(Action<string> directoryCreateFn)
        {
        }

        protected override void OnOptionsSaved(PluginOptions options)
        {
            Plugin.Options = options;
            this.logger.Info("My plugin ({0}) options have been updated.", this.Name);
        }

    }

}