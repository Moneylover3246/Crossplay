using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class CrossplayPlugin : TerrariaPlugin
    {
        private readonly Dictionary<int, string> _supportedVersions = new()
        {
            { 269, "v1.4.4" },
            { 270, "v1.4.4.1" },
            { 271, "v1.4.4.2" },
            { 272, "v1.4.4.3" },
            { 273, "v1.4.4.4" },
            { 274, "v1.4.4.5" },
            { 275, "v1.4.4.6" },
            { 276, "v1.4.4.7" },
            { 277, "v1.4.4.8" },
            { 278, "v1.4.4.8.1" },
        };

        public override string Name => "Crossplay";

        public override string Author => "Moneylover3246";

        public override string Description => "Enables crossplay for terraria";

        public override Version Version => new("2.1");

        public CrossplayConfig Config { get; } = new();

        public int[] ClientVersions { get; } = new int[Main.maxPlayers];

        public static CrossplayPlugin Instance { get; private set; }

        public static string SavePath => Path.Combine(TShock.SavePath, "Crossplay.json");

        public readonly Dictionary<int, int> MaxItems = new()
        {
            { 269, 5453 },
            { 270, 5453 },
            { 271, 5453 },
            { 272, 5453 },
            { 273, 5453 },
            { 274, 5456 },
            { 275, 5456 },
            { 276, 5456 },
            { 277, 5456 },
            { 278, 5456 },
        };

        public CrossplayPlugin(Main game) : base(game)
        {
            Instance = this;
            Order = -1;
        }

        public override void Initialize()
        {
            if (!_supportedVersions.TryGetValue(Main.curRelease, out string version) || version != Main.versionNumber)
            {
                throw new NotSupportedException("The provided version of this plugin is outdated and will not function properly. Check for any updates here: https://github.com/Moneylover3246/Crossplay");
            }

            On.Terraria.Net.NetManager.Broadcast_NetPacket_int += NetModuleHandler.OnBroadcast;
            On.Terraria.Net.NetManager.SendToClient += NetModuleHandler.OnSendToClient;

            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);

            GeneralHooks.ReloadEvent += OnReload;
        }

        private void OnInitialize(EventArgs args)
        {
            if (!File.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }
            bool writeConfig = true;
            if (File.Exists(SavePath))
            {
                Config.Read(SavePath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(SavePath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                On.Terraria.Net.NetManager.Broadcast_NetPacket_int -= NetModuleHandler.OnBroadcast;
                On.Terraria.Net.NetManager.SendToClient -= NetModuleHandler.OnSendToClient;

                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);

                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
        {
            bool writeConfig = true;
            if (File.Exists(SavePath))
            {
                Config.Read(SavePath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(SavePath);
            }
        }

        private void OnPostInitialize(EventArgs e)
        {
            StringBuilder sb = new StringBuilder()
                .Append("Crossplay has been enabled & has whitelisted the following versions:\n")
                .Append(string.Join(", ", _supportedVersions.Values))
                .Append("\n\nIf there are any issues please report them here: https://github.com/Moneylover3246/Crossplay");

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("-------------------------------------");

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine(sb.ToString());

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine("-------------------------------------");
            Console.ResetColor();
        }

        private void OnGetData(GetDataEventArgs args)
        {
            int index = args.Msg.whoAmI;
            using (var reader = new BinaryReader(new MemoryStream(args.Msg.readBuffer, args.Index, args.Length)))
            {
                if (ClientVersions[index] == 0 && args.MsgID != PacketTypes.ConnectRequest)
                {
                    return;
                }
                switch (args.MsgID)
                {
                    case PacketTypes.ConnectRequest:
                        {
                            string clientVersion = reader.ReadString();
                            if (clientVersion.Length != 11)
                            {
                                args.Handled = true;
                                return;
                            }
                            if (!int.TryParse(clientVersion.AsSpan(clientVersion.Length - 3), out int versionNumber))
                            {
                                return;
                            }
                            if (versionNumber == Main.curRelease)
                            {
                                ClientVersions[index] = -1;
                                return;
                            }
                            if (!_supportedVersions.ContainsKey(versionNumber))
                            {
                                return;
                            }
                            ClientVersions[index] = versionNumber;
                            NetMessage.SendData(9, args.Msg.whoAmI, -1, NetworkText.FromLiteral("Fixing Version..."), 1);
                            byte[] connectRequest = new PacketFactory()
                                .SetType(1)
                                .PackString($"Terraria276")
                                .GetByteData();
                            Log($"Changing version of index {args.Msg.whoAmI} from {_supportedVersions[versionNumber]} => {_supportedVersions[276]}", color: ConsoleColor.Green);

                            Buffer.BlockCopy(connectRequest, 0, args.Msg.readBuffer, args.Index - 3, connectRequest.Length);
                        }
                        break;
                    case PacketTypes.PlayerInfo:
                        {
                            if (!Config.Settings.SupportJourneyClients)
                            {
                                return;
                            }
                            ref byte gameModeFlags = ref args.Msg.readBuffer[args.Length - 1];
                            if (Main.GameModeInfo.IsJourneyMode)
                            {
                                if ((gameModeFlags & 8) != 8)
                                {
                                    Log($"Enabled journey mode for index {args.Msg.whoAmI}", color: ConsoleColor.Green);
                                    gameModeFlags |= 8;
                                    if (Main.ServerSideCharacter)
                                    {
                                        NetMessage.SendData(4, args.Msg.whoAmI, -1, null, args.Msg.whoAmI);
                                    }
                                }
                                return;
                            }
                            if (TShock.Config.Settings.SoftcoreOnly && (gameModeFlags & 3) != 0)
                            {
                                return;
                            }
                            if ((gameModeFlags & 8) == 8)
                            {
                                Log($"Disabled journey mode for index {args.Msg.whoAmI}", color: ConsoleColor.Green);
                                gameModeFlags &= 247;
                            }
                        }
                        break;
                }
            }
        }

        private void OnLeave(LeaveEventArgs args)
        {
            ClientVersions[args.Who] = 0;
        }

        public void Log(string message, bool debug = false, ConsoleColor color = ConsoleColor.White)
        {
            if (debug)
            {
                if (Config.Settings.DebugMode)
                {
                    Console.ForegroundColor = color;
                    Console.WriteLine($"[Crossplay Debug] {message}");
                    Console.ResetColor();
                }
                return;
            }
            Console.ForegroundColor = color;
            Console.WriteLine($"[Crossplay] {message}");
            Console.ResetColor();
        }
    }
}
