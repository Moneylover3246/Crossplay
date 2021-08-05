using System.IO;
using Newtonsoft.Json;
using TShockAPI.Configuration;

namespace Crossplay
{
    public class CrossplaySettings
    {
        public bool EnableJourneySupport = false;

        public bool EnablePacketDebugging = false;
    }

    public class Config : ConfigFile<CrossplaySettings>
    {
    }
}
