using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using OTAPI.Tile;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.Hooks;
using TShockAPI.Net;

namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class Crossplay : TerrariaPlugin
    {
        public static int Header = 3;
        public override string Name => "Crossplay";
        public override string Author => "Moneylover3246";
        public override string Description => "Enables crossplay for terraria";
        public override Version Version => new Version("1.4.2.1");

        private static List<int> AllowedVersions = new List<int>() { 230, 233, 234, 235, 236, 237 };
        public static string ConfigPath => Path.Combine("tshock", "Crossplay.json");

        public static CrossplayConfig Config = new CrossplayConfig();

        private static Dictionary<int, int> Non238Clients = new Dictionary<int, int>();
        private static Dictionary<int, int> MaxNPCID = new Dictionary<int, int>()
        {
            { 230, 662 },
            { 233, 664 },
            { 234, 664 },
            { 235, 664 },
            { 236, 664 },
            { 237, 666 },
        };
        public static Dictionary<int, int> MaxTileType = new Dictionary<int, int>()
        {
            { 230, 622 },
            { 233, 623 },
            { 234, 623 },
            { 235, 623 },
            { 236, 623 },
            { 237, 623 },
        };

        public Crossplay(Main game) : base(game)
        {
            Order = -1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, GetData, int.MaxValue);
            ServerApi.Hooks.NetSendBytes.Register(this, SendBytes, -int.MaxValue);
            ServerApi.Hooks.NetSendNetData.Register(this, HandleNetModules);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
            GeneralHooks.ReloadEvent += OnReload;
        }

        private static void OnInitialize(EventArgs args)
        {
            if (!File.Exists(TShock.SavePath))
            {
                Directory.CreateDirectory(TShock.SavePath);
            }
            bool writeConfig = true;
            if (File.Exists(ConfigPath))
            {
                Config.Read(ConfigPath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(ConfigPath);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.GameInitialize.Deregister(this, OnInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, GetData);
                ServerApi.Hooks.NetSendBytes.Deregister(this, SendBytes);
                ServerApi.Hooks.NetSendNetData.Deregister(this, HandleNetModules);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
                GeneralHooks.ReloadEvent -= OnReload;
            }
            base.Dispose(disposing);
        }

        private void OnReload(ReloadEventArgs args)
        {
            bool writeConfig = true;
            if (File.Exists(ConfigPath))
            {
                Config.Read(ConfigPath, out writeConfig);
            }
            if (writeConfig)
            {
                Config.Write(ConfigPath);
            }
        }

        private void GetData(GetDataEventArgs args)
        {
            MemoryStream stream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);
            int index = args.Msg.whoAmI;
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if ((int)args.MsgID == 1)
                {
                    string versionstring = reader.ReadString();
                    if (int.TryParse(versionstring.Substring(versionstring.Length - 3), out int version))
                    {
                        if (AllowedVersions.Contains(version))
                        {
                            Non238Clients.Add(index, version);
                            NetMessage.SendData(9, args.Msg.whoAmI, -1, NetworkText.FromLiteral("Fixing Version..."), 1);
                            byte[] buffer = new PacketFactory()
                                .SetType(1)
                                .PackString("Terraria" + Main.curRelease)
                                .GetByteData();
                            Log($"Changing version of index {args.Msg.whoAmI} from {Convert(version)} => v1.4.2.3", color: ConsoleColor.Green);
                            args.Msg.readBuffer.SwapBytes(args.Index - Header, args.Length + (Header - 1), buffer);
                        }
                    }
                    return;
                }
                if (Non238Clients.ContainsKey(index))
                {
                    Non238Clients.TryGetValue(index, out int playerVersion);
                    switch (args.MsgID)
                    {
                        case PacketTypes.PlayerInfo:
                            if (Config.Settings.EnableJourneySupport)
                            {
                                byte value = args.Msg.readBuffer[args.Length];
                                if (Main.GameMode == 3)
                                {
                                    args.Msg.readBuffer[args.Length] |= 8;
                                }
                                else
                                {
                                    args.Msg.readBuffer[args.Length] &= 247;
                                }
                                if (args.Msg.readBuffer[args.Length] != value)
                                {
                                    Log($"[Crossplay] {(Main.GameMode == 3 ? "Enabled" : "Disabled")} journey mode for index {args.Msg.whoAmI}", color: ConsoleColor.Green);
                                }
                            }
                            break;
                        case PacketTypes.TileSendSquare:
                            {
                                if (playerVersion < 234)
                                {
                                    ushort header = reader.ReadUInt16();
                                    var size = header & 32767;
                                    byte changeType = 0;
                                    if ((header & 0x8000) > 0)
                                    {
                                        changeType = reader.ReadByte();
                                    }
                                    short tileX = reader.ReadInt16();
                                    short tileY = reader.ReadInt16();

                                    SendTileRectHandler handler = new SendTileRectHandler();
                                    GetDataHandlers.SendTileRectEventArgs STREventArgs = new GetDataHandlers.SendTileRectEventArgs()
                                    {
                                        Player = TShock.Players[args.Msg.whoAmI],
                                        TileX = tileX,
                                        TileY = tileY,
                                        ChangeType = (TileChangeType)changeType,
                                        Data = stream,
                                        Width = (byte)size,
                                        Length = (byte)size,
                                    };
                                    args.Handled = true;
                                    if (SendTileRectHandler.ShouldSkipProcessing(STREventArgs))
                                    {
                                        return;
                                    }
                                    bool[,] processed = new bool[size, size];
                                    NetTile[,] tiles = new NetTile[size, size];
                                    MemoryStream stream2 = new MemoryStream(args.Msg.readBuffer, (int)(args.Index + reader.BaseStream.Position), args.Length); ;
                                    for (int x = 0; x < size; x++)
                                    {
                                        for (int y = 0; y < size; y++)
                                        {
                                            tiles[x, y] = new NetTile(stream2);
                                        }
                                    }
                                    handler.IterateTileRect(tiles, processed, STREventArgs);
                                }
                            }
                            break;
                    }
                }
            }
        }

        private void SendBytes(SendBytesEventArgs args)
        {
            int playerIndex = args.Socket.Id;
            RemoteClient client = Netplay.Clients[playerIndex];
            if (Non238Clients.ContainsKey(playerIndex))
            {
                Non238Clients.TryGetValue(playerIndex, out int playerVersion);
                var stream = new MemoryStream(args.Buffer, 0, args.Buffer.Length);
                BinaryWriter writer = new BinaryWriter(stream);
                using (BinaryReader reader = new BinaryReader(stream))
                {
                    try
                    {
                        int packetlength = reader.ReadUInt16();
                        int msgType = reader.ReadByte();
                        switch ((PacketTypes)msgType)
                        {
                            case PacketTypes.WorldInfo:
                                {
                                    byte[] buffer = reader.ReadBytes(22);
                                    string worldName = reader.ReadString();
                                    byte[] buffer2 = reader.ReadBytes(103);
                                    reader.ReadByte(); // Main.tenthAnniversaryWorld
                                    byte[] buffer3 = reader.ReadBytes(27);
                                    byte[] newdata = new PacketFactory()
                                        .SetType(7)
                                        .PackBuffer(buffer)
                                        .PackString(worldName)
                                        .PackBuffer(buffer2)
                                        .PackBuffer(buffer3)
                                        .GetByteData();
                                    client.Socket.AsyncSend(newdata, 0, newdata.Length, client.ServerWriteCallBack);
                                    args.Handled = true;
                                }
                                break;
                            case PacketTypes.TileSendSection:
                                {
                                    bool compressed = reader.ReadBoolean();
                                    MemoryStream streamRead = new MemoryStream();
                                    streamRead.Write(args.Buffer, 4, args.Buffer.Length - 4);
                                    streamRead.Position = 0L;
                                    if (compressed)
                                    {
                                        using (DeflateStream deflateStream = new DeflateStream(streamRead, CompressionMode.Decompress, true))
                                        {
                                            streamRead = new MemoryStream();
                                            deflateStream.CopyTo(streamRead);
                                            deflateStream.Close();
                                        }
                                        streamRead.Position = 0L;
                                    }
                                    byte[] newdata = SectionHelper.WriteDecompressedSection(streamRead, playerVersion);
                                    if (newdata.Length != 0)
                                    {
                                        client.Socket.AsyncSend(newdata, 0, newdata.Length, client.ServerWriteCallBack);
                                        args.Handled = true;
                                    }
                                }
                                break;
                            case PacketTypes.TileSendSquare:
                                {
                                    if (playerVersion < 234)
                                    {
                                        short tileX = reader.ReadInt16();
                                        short tileY = reader.ReadInt16();
                                        ushort width = reader.ReadByte();
                                        ushort length = reader.ReadByte();
                                        byte changeType = reader.ReadByte();
                                        ushort header = Math.Max(width, length);
                                        args.Handled = true;
                                        if (width != length)
                                        {
                                            Log($"/ SendTileRect - Relooping tileRect of index {playerIndex} because of irregular dimensions", true, ConsoleColor.Cyan);
                                            TShock.Players[playerIndex].SendTileRect(tileX, tileY, (byte)header, (byte)header, (TileChangeType)changeType);
                                            return;
                                        }
                                        if (changeType != 0)
                                        {
                                            header |= 32768;
                                        }
                                        var size = header & 32767;
                                        PacketFactory data = new PacketFactory()
                                            .SetType(20)
                                            .PackUInt16(header);
                                        if (changeType != 0)
                                        {
                                            data.PackByte(changeType);
                                        }
                                        data.PackInt16(tileX);
                                        data.PackInt16(tileY);
                                        var position = reader.BaseStream.Position;

                                        BitsByte tileflags = 0;
                                        BitsByte tileflags2 = 0;
                                        for (int x = 0; x < size; x++)
                                        {
                                            for (int y = 0; y < size; y++)
                                            {
                                                tileflags = reader.ReadByte();
                                                tileflags2 = reader.ReadByte();
                                                if (tileflags2[2])
                                                {
                                                    reader.ReadByte(); // color
                                                }
                                                if (tileflags2[3])
                                                {
                                                    reader.ReadByte(); // wall color
                                                }
                                                if (tileflags[0])
                                                {
                                                    var tileType = reader.ReadUInt16();
                                                    MaxTileType.TryGetValue(playerVersion, out int maxTileType);
                                                    if (tileType > maxTileType)
                                                    {
                                                        stream.Position -= 2;
                                                        writer.Write((ushort)(Main.tileFrameImportant[tileType] ? 72 : 1));
                                                    }
                                                    if (Main.tileFrameImportant[tileType])
                                                    {
                                                        reader.ReadBytes(4);
                                                    }
                                                }
                                                if (tileflags[2])
                                                {
                                                    reader.ReadUInt16(); // Wall type
                                                }
                                                if (tileflags[3])
                                                {
                                                    reader.ReadBytes(2); // Liquid type + amount
                                                }
                                            }
                                        }
                                        stream.Position = position;
                                        data.PackBuffer(reader.ReadToEnd());

                                        byte[] newdataBuffer = data.GetByteData();
                                        client.Socket.AsyncSend(newdataBuffer, 0, newdataBuffer.Length, client.ServerWriteCallBack);
                                    }
                                }
                                break;
                            case PacketTypes.ProjectileNew:
                                {
                                    if (playerVersion < 237)
                                    {
                                        byte[] buffer = reader.ReadBytes(19);
                                        short projID = reader.ReadInt16();
                                        if (projID > 949)
                                        {
                                            var old = projID;
                                            switch (projID)
                                            {
                                                case 953:
                                                    projID = 612;
                                                    break;
                                                case 954:
                                                    projID = 504;
                                                    break;
                                                case 955:
                                                    projID = 12;
                                                    break;
                                                default:
                                                    args.Handled = true;
                                                    Log($"/ ProjectileUpdate - handled index {playerIndex} from exceeded maxType ({projID})", true, ConsoleColor.Red);
                                                    return;
                                            }
                                            Log($"/ ProjectileUpdate - swapped type from {old} -> {projID} from previously exceeded maxType", true, ConsoleColor.DarkGreen);
                                        }
                                        BitsByte projFlags = reader.ReadByte();
                                        float AI0 = projFlags[0] ? reader.ReadSingle() : 0f;
                                        float AI1 = projFlags[1] ? reader.ReadSingle() : 0f;
                                        int bannerIdToRespondTo = projFlags[3] ? reader.ReadUInt16() : 0;

                                        var newdata = new PacketFactory()
                                            .SetType(27)
                                            .PackBuffer(buffer)
                                            .PackInt16(projID)
                                            .PackByte(projFlags);
                                        var pack = AI0 > 0f ? newdata.PackSingle(AI0) : null;
                                        var pack2 = AI1 > 0f ? newdata.PackSingle(AI1) : null;
                                        newdata.PackBuffer(reader.ReadToEnd());

                                        byte[] newdataBuffer = newdata.GetByteData();
                                        client.Socket.AsyncSend(newdataBuffer, 0, newdataBuffer.Length, client.ServerWriteCallBack);
                                        args.Handled = true;
                                    }
                                }
                                break;
                            case PacketTypes.NpcUpdate:
                                {
                                    reader.ReadBytes(20);
                                    BitsByte npcFlags = reader.ReadByte();
                                    reader.ReadByte();
                                    for (int i = 2; i < 6; i++)
                                    {
                                        if (npcFlags[i])
                                        {
                                            reader.ReadSingle();
                                        }
                                    }
                                    int type = reader.ReadInt16();
                                    MaxNPCID.TryGetValue(playerVersion, out int maxNpcID);
                                    if (type > maxNpcID)
                                    {
                                        Log($"/ NpcUpdate - Preventing NPC packet from sending to index {playerIndex} because it exceeds npcType limit", true, ConsoleColor.Cyan);
                                        args.Handled = true;
                                    }
                                }
                                break;
                        }
                    }
                    catch (IOException ex)
                    {
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }
            }
        }

        private void HandleNetModules(SendNetDataEventArgs args)
        {
            int GetIndexFromSocket(ISocket socket)
            {
                for (int i = 0; i < 255; i++)
                {
                    if (Netplay.Clients[i].Socket == socket)
                    {
                        return i;
                    }
                }
                return -1;
            }
            byte[] moduleData = args.packet.Buffer.Data;
            
            using (BinaryReader reader = new BinaryReader(new MemoryStream(moduleData)))
            {
                reader.ReadInt16();
                reader.ReadByte();
                ushort netModuleID = reader.ReadUInt16();
                switch (netModuleID)
                {
                    case 4:
                        byte unlockType = reader.ReadByte();
                        short npcID = reader.ReadInt16();
                        int index = GetIndexFromSocket(args.socket);
                        if (Non238Clients.ContainsKey(index))
                        {
                            Non238Clients.TryGetValue(index, out int playerVersion);
                            MaxNPCID.TryGetValue(playerVersion, out int maxNpcID);
                            if (npcID > maxNpcID)
                            {
                                args.Handled = true;
                            }
                        }
                        break;
                }
            }
        }

        private void OnLeave(LeaveEventArgs args)
        {
            Non238Clients.Remove(args.Who);
        }

        private string Convert(int version)
        {
            string protocol = $"Terraria{version}";
            switch (protocol)
            {
                case "Terraria230":
                    return "v1.4.0.5";
                case "Terraria233":
                    return "v1.4.1.1";
                case "Terraria234":
                    return "v1.4.1.2";
            }
            return "";
        }

        public static void Log(string message, bool debug = false, ConsoleColor color = ConsoleColor.White)
        {
            Console.ForegroundColor = color;
            if (debug)
            {
                if (Config.Settings.EnablePacketDebugging)
                {
                    Console.WriteLine($"[Crossplay Debug] {message}");
                    Console.ResetColor();
                }
                return;
            }
            Console.WriteLine($"[Crossplay] {message}");
            Console.ResetColor();
        }
    }
}
