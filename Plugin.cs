using System;
using System.Collections.Generic;
using Emby.Subtitle.OneOneFiveMaster.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Serialization;
using MediaBrowser.Model.Plugins;

namespace Emby.Subtitle.OneOneFiveMaster
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        public override string Name => "115Master Subtitles";
        public override Guid Id => new Guid("9b7dca24-edf8-4155-bbd6-8c2b0f07448d"); // Using conversation GUID for uniqueness or a random one? Let's generate a random one to avoid confusion.
        // Actually, let's use a fixed GUID: 
        // 7c945417-578d-4299-b13c-74312270914d

        public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
        }

        public static Plugin Instance { get; private set; } = null!;

        public IEnumerable<PluginPageInfo> GetPages()
        {
            return new[]
            {
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = GetType().Namespace + ".Configuration.configPage.html"
                }
            };
        }
    }
}
