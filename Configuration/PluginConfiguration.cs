using MediaBrowser.Model.Plugins;

namespace Emby.Subtitle.OneOneFiveMaster.Configuration
{
    public class PluginConfiguration : BasePluginConfiguration
    {
        public bool EnableSubtitleCat { get; set; } = true;
        public bool EnableThunder { get; set; } = true;

        public PluginConfiguration()
        {
            EnableSubtitleCat = true;
            EnableThunder = true;
        }
    }
}
