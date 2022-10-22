using Newtonsoft.Json;
using TShockAPI.Configuration;

namespace Crossplay
{
    public class CrossplaySettings
    {
        [JsonProperty("support_journey_clients")]
        public bool SupportJourneyClients = false;

        [JsonProperty("debug_mode")]
        public bool DebugMode = false;
    }

    public class CrossplayConfig : ConfigFile<CrossplaySettings>
    {
    }
}
