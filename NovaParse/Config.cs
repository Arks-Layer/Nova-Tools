using System.IO;

using Newtonsoft.Json;

namespace NovaParse
{
    public class Config
    {
        public string DownloadUrl { get; set; } = "https://github.com/Arks-Layer/PSNovaTranslations/archive/master.zip";
        public string DownloadFileName { get; set; } = "Translation.zip";
        public string InputPath { get; set; } = "PSNovaTranslations-master";
        public string TranslationJsonFile { get; set; } = "translations.json";
        public string TranslationJsonFileOutput { get; set; } = "translations.output.json";
        public string LogFile { get; set; } = "NovaParse.log";

        public static Config Load()
        {
            if (!File.Exists(("Config.json")))
                Create();

            return JsonConvert.DeserializeObject<Config>(File.ReadAllText("Config.json"));
        }

        private static void Create()
        {
            File.WriteAllText("Config.json", JsonConvert.SerializeObject(new Config(), Formatting.Indented));
        }
    }
}
