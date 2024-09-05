// <copyright file="BasicsUI.cs" company="Emby LLC">
// Copyright © 2022 - Emby LLC. All rights reserved.
// </copyright>

namespace EmbyPluginUiDemo.UI.Basics
{
    using Emby.Web.GenericEdit;
    using Emby.Web.GenericEdit.Elements;
    using System.ComponentModel;

    public class BasicsUI : EditableOptionsBase
    {
        public override string EditorTitle => "Jimaku Subtitles";

        public override string EditorDescription => "";

        SpacerItem SpacerItem { get; set; }

        [DisplayName("Jimaku API Key:")]
        public string ApiKey { get; set; }
    }
}
