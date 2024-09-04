namespace Emby.Jimaku
{
    using System.ComponentModel;

    using Emby.Web.GenericEdit;
    using Emby.Web.GenericEdit.Elements;
    using Emby.Web.GenericEdit.Validation;

    using MediaBrowser.Model.Attributes;
    using MediaBrowser.Model.GenericEdit;
    using MediaBrowser.Model.Logging;
    using MediaBrowser.Model.MediaInfo;

    public class PluginOptions : EditableOptionsBase
    {
        public override string EditorTitle => "Jimaku Subtitles";

        public override string EditorDescription => "";

        SpacerItem SpacerItem { get; set; }

        [DisplayName("Jimaku API Key:")]
        public string ApiKey { get; set; }
    }
}
