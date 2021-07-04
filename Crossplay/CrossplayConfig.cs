using System.IO;
using Newtonsoft.Json;
using TShockAPI.Configuration;

namespace Crossplay
{
    public class CrossplaySettings
    {
        public bool EnableJourneySupport = false;
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
    }
}
