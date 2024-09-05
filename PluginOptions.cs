namespace Emby.Jimaku
{
    using Emby.Web.GenericEdit;
    using Emby.Web.GenericEdit.Elements;
    using System.ComponentModel;

    public class PluginOptions : EditableOptionsBase
    {
        public override string EditorTitle => "Jimaku Subtitles";

        public override string EditorDescription => "";

        SpacerItem SpacerItem { get; set; }

        [DisplayName("Jimaku API Key:")]
        public string ApiKey { get; set; }
    }
}
