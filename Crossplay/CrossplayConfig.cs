using System.IO;
using Newtonsoft.Json;
using TShockAPI.Configuration;

namespace Crossplay
{
    public class CrossplaySettings
    {
        public bool EnableJourneySupport = false;

        public bool EnablePacketDebugging = false;

        public bool FakeVersionEnabled = false;

        public int FakeVersion = 248;
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
    }
}
