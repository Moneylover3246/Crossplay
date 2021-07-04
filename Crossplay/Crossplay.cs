using System;
using System.Collections.Generic;
using System.IO;
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
        public override Version Version => new Version("1.4.1");

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

        public Crossplay(Main game) : base(game)
        {
            Order = -1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetGetData.Register(this, GetData, 1);
            ServerApi.Hooks.NetSendBytes.Register(this, SendBytes, -1);
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
                            NetMessage.SendData(9, args.Msg.whoAmI, -1, NetworkText.FromLiteral("Fixing Version"), 1);
                            byte[] buffer = new PacketFactory()
                                .SetType(1)
                                .PackString("Terraria" + Main.curRelease)
                                .GetByteData();
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.WriteLine($"[Crossplay] Changing version of index {args.Msg.whoAmI} from {Convert(version)} => v1.4.2.3");
                            Console.ResetColor();
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
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine($"[Crossplay] {(Main.GameMode == 3 ? "Enabled" : "Disabled")} journey mode for index {args.Msg.whoAmI}");
                                    Console.ResetColor();
                                }
                            }
                            break;
                        case PacketTypes.TileSendSquare:
                            {
                                if (playerVersion < 234)
                                {
                                    ushort size = reader.ReadUInt16();
                                    byte changeType = 0;
                                    if ((size & 0x8000) > 0)
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
            try
            {
                if (Non238Clients.ContainsKey(playerIndex))
                {
                    Non238Clients.TryGetValue(playerIndex, out int playerVersion);
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(args.Buffer, 0, args.Buffer.Length)))
                    {
                        int packetlength = reader.ReadInt16();
                        int msgID = reader.ReadByte();
                        switch ((PacketTypes)msgID)
                        {
                            case PacketTypes.WorldInfo:
                                {
                                    byte[] buffer = reader.ReadBytes(22);
                                    string worldName = reader.ReadString();
                                    byte[] buffer2 = reader.ReadBytes(103);
                                    reader.ReadByte(); // Main.tenthAnniversaryWorld
                                    byte[] buffer3 = reader.ReadBytes(27);
                                    byte[] data = new PacketFactory()
                                        .SetType(7)
                                        .PackBuffer(buffer)
                                        .PackString(worldName)
                                        .PackBuffer(buffer2)
                                        .PackBuffer(buffer3)
                                        .GetByteData();
                                    client.Socket.AsyncSend(data, 0, data.Length, client.ServerWriteCallBack);
                                    args.Handled = true;
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
                                        byte tileChangeType = reader.ReadByte();
                                        args.Handled = true;
                                        ushort size = Math.Max(width, length);
                                        if (width != length)
                                        {
                                            if (Config.Settings.EnablePacketDebugging)
                                            {
                                                Console.WriteLine($"[Crossplay Debug] SendTileRect width and length are uneven, sending tile square with a size of {size}");
                                            }
                                            TShock.Players[playerIndex].SendTileSquare(tileX, tileY, size);
                                            return;
                                        }
                                        PacketFactory data = new PacketFactory()
                                            .SetType(20)
                                            .PackUInt16(size);
                                        if (tileChangeType != 0)
                                        {
                                            data.PackByte(tileChangeType);
                                        }
                                        data.PackInt16(tileX);
                                        data.PackInt16(tileY);
                                        data.PackBuffer(reader.ReadToEnd());
                                        byte[] buffer = data.GetByteData();
                                        client.Socket.AsyncSend(buffer, 0, buffer.Length, client.ServerWriteCallBack); 
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
                                            args.Handled = true;
                                            return;
                                        }
                                        BitsByte projFlags = reader.ReadByte();
                                        float AI0 = 0f;
                                        float AI1 = 0f;
                                        if (projFlags[0])
                                        {
                                            AI0 = reader.ReadSingle();
                                        }
                                        if (projFlags[1])
                                        {
                                            AI1 = reader.ReadSingle();
                                        }
                                        int bannerIdToRespondTo = projFlags[3] ? reader.ReadUInt16() : 0;
                                        var newData = new PacketFactory()
                                            .SetType(27)
                                            .PackBuffer(buffer)
                                            .PackInt16(projID)
                                            .PackByte(projFlags);
                                        if (projFlags[0])
                                        {
                                            newData.PackSingle(AI0);
                                        }
                                        if (projFlags[1])
                                        {
                                            newData.PackSingle(AI1);
                                        }
                                        newData.PackBuffer(reader.ReadToEnd());
                                        byte[] newdataBuffer = newData.GetByteData();
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
                                        args.Handled = true;
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            catch
            {
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
    }
}
