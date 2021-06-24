using System.IO;
using Newtonsoft.Json;

namespace Crossplay
{
    public class ConfigFile
    {
        public static ConfigFile Read(string path) 
        {
            ConfigFile config = new ConfigFile();
            if (!File.Exists(path))
            {
                File.WriteAllText(path, JsonConvert.SerializeObject(config, Formatting.Indented));
                return config;
            }
            return JsonConvert.DeserializeObject<ConfigFile>(File.ReadAllText(path));
        }

        public bool EnableJourneySupport = false;
    }
}
