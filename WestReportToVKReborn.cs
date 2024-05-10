using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Cvars;
using Newtonsoft.Json;
using VkNet;
using VkNet.Model;
using WestReportSystemApiReborn;

namespace WestReportToVKReborn
{
    public class WestReportToVKReborn : BasePlugin
    {
        public override string ModuleName => "WestReportToVK";
        public override string ModuleVersion => "1.1";
        public override string ModuleAuthor => "E!N";

        private IWestReportSystemApi? WRS_API;
        private VKConfig? _config;
        private VkApi? vk;
        private long chatId;
        private string[]? admins;

        public override void OnAllPluginsLoaded(bool hotReload)
        {
            string configDirectory = GetConfigDirectory();
            EnsureConfigDirectory(configDirectory);
            string configPath = Path.Combine(configDirectory, "VKConfig.json");
            _config = VKConfig.Load(configPath);

            WRS_API = IWestReportSystemApi.Capability.Get();

            if (WRS_API == null)
            {
                Console.WriteLine($"{ModuleName} | Error: Essential services (WestReportSystem API) are not available.");
                return;
            }

            InitializeVK();
        }

        private static string GetConfigDirectory()
        {
            return Path.Combine(Server.GameDirectory, "csgo/addons/counterstrikesharp/configs/plugins/WestReportSystem/Modules");
        }

        private void EnsureConfigDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                Directory.CreateDirectory(directoryPath);
                Console.WriteLine($"{ModuleName} | Created configuration directory at: {directoryPath}");
            }
        }

        private void InitializeVK()
        {
            if (_config == null)
            {
                Console.WriteLine($"{ModuleName} | Error: Configuration is not loaded.");
                return;
            }
            string? token = _config.VkToken;
            chatId = _config.VkPeerId;
            admins = _config.VkAdmins;

            if (string.IsNullOrEmpty(token) || chatId == 0)
            {
                Console.WriteLine($"{ModuleName} | Error: VK configuration is missing (Token or Chat ID).");
                return;
            }

            vk = new VkApi();
            vk.Authorize(new ApiAuthParams { AccessToken = token });
            WRS_API?.RegisterReportingModule(WRS_SendReport_To_VK);
            Console.WriteLine($"{ModuleName} | Initialized successfully.");
        }

        public void WRS_SendReport_To_VK(CCSPlayerController sender, CCSPlayerController violator, string reason)
        {
            string serverName = ConVar.Find("hostname")?.StringValue ?? "Unknown Server";
            string mapName = NativeAPI.GetMapName();
            string serverIp = ConVar.Find("ip")?.StringValue ?? "Unknown IP";
            string serverPort = ConVar.Find("hostport")?.GetPrimitiveValue<int>().ToString() ?? "Unknown Port";
            string siteLink = WRS_API?.GetConfigValue<string>("SiteLink") ?? "No link provided";
            int reportCount = WRS_API?.WRS_GetReportCounterPerRound(violator)?.GetValueOrDefault(violator, 1) ?? 1;

            if (vk == null || admins == null || WRS_API == null)
            {
                Console.WriteLine($"{ModuleName} | VK is not initialized.");
                return;
            }

            bool sender_prime = WRS_API.HasPrimeStatus(sender.SteamID);
            bool violator_prime = WRS_API.HasPrimeStatus(sender.SteamID);

            string messageText = $"{WRS_API.GetTranslatedText("wrtv.Title", serverName)}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Server", serverName)}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Sender", sender.PlayerName, sender.SteamID, sender_prime ? WRS_API.GetTranslatedText("wrs.PrimeTrue") : WRS_API.GetTranslatedText("wrs.PrimeFalse") ?? "Unknown")}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Violator", violator.PlayerName, violator.SteamID, violator_prime ? WRS_API.GetTranslatedText("wrs.PrimeTrue") : WRS_API.GetTranslatedText("wrs.PrimeFalse") ?? "Unknown")}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Reason", reason)}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Map", mapName, reportCount)}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Administrators", admins)}" +
                                 $"{WRS_API.GetTranslatedText("wrtv.Connect", serverIp, serverPort, siteLink)}";

            try
            {
                vk.Messages.Send(new MessagesSendParams
                {
                    RandomId = new Random().Next(int.MaxValue),
                    Message = messageText,
                    PeerId = chatId
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{ModuleName} | Error sending report to VK: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"{ModuleName} | Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        public class VKConfig
        {
            public string? VkToken { get; set; }
            public long VkPeerId { get; set; }
            public string[]? VkAdmins { get; set; }

            public static VKConfig Load(string configPath)
            {
                if (!File.Exists(configPath))
                {
                    VKConfig defaultConfig = new();
                    File.WriteAllText(configPath, JsonConvert.SerializeObject(defaultConfig, Newtonsoft.Json.Formatting.Indented));
                    return defaultConfig;
                }

                string json = File.ReadAllText(configPath);
                return JsonConvert.DeserializeObject<VKConfig>(json) ?? new VKConfig();
            }
        }
    }
}