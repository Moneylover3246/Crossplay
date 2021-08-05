using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.Net.Sockets;
using TerrariaApi.Server;

using TShockAPI;
using TShockAPI.Hooks;

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

        public static Config Config = new Config();

        private static readonly CrossplayClient[] Clients = new CrossplayClient[Main.maxPlayers];

        private static readonly Dictionary<int, int> MaxNPCID = new Dictionary<int, int>()
        {
            { 230, 662 },
            { 233, 664 },
            { 234, 664 },
            { 235, 664 },
            { 236, 664 },
            { 237, 666 },
        };
        public static readonly Dictionary<int, int> MaxTileType = new Dictionary<int, int>()
        {
            { 230, 622 },
            { 233, 623 },
            { 234, 623 },
            { 235, 623 },
            { 236, 623 },
            { 237, 623 },
        };
        public static readonly Dictionary<int, int> MaxBuffType = new Dictionary<int, int>()
        {
            { 230, 322 },
            { 233, 329 },
            { 234, 329 },
            { 235, 329 },
            { 236, 329 },
            { 237, 329 },
        };
        public static readonly Dictionary<int, int> MaxProjectileType = new Dictionary<int, int>()
        {
            { 230, 949 },
            { 233, 953 },
            { 234, 953 },
            { 235, 955 },
            { 236, 955 },
            { 237, 955 },
        };
        public static readonly Dictionary<int, int> MaxItemType = new Dictionary<int, int>()
        {
            { 230, 5044 },
            { 233, 5087 },
            { 234, 5087 },
            { 235, 5087 },
            { 236, 5087 },
            { 237, 5087 },
        };

        public Crossplay(Main game) : base(game)
        {
            Order = -1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.NetReceiveBytes.Register(this, ReceiveBytes);
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
                ServerApi.Hooks.NetReceiveBytes.Deregister(this, ReceiveBytes);
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

        private void ReceiveBytes(ReceiveBytesEventArgs args)
        {
            var client = Clients[args.BufferIndex];
            if (client == null)
            {
                client = new CrossplayClient();
                Clients[args.BufferIndex] = client;
            }
            Buffer.BlockCopy(args.Bytes, 0, client.LeftoverBytes, client.TotalData, args.StreamLength);
            Clients[args.BufferIndex].TotalData += args.StreamLength;
            var dataLeft = Clients[args.BufferIndex].TotalData;
            var bytesRead = 0;
            List<byte> newbuffer = new List<byte>();
            while (dataLeft >= 2)
            {
                var packetLength = BitConverter.ToUInt16(Clients[args.BufferIndex].LeftoverBytes, bytesRead);
                if (packetLength == 0 || dataLeft < packetLength)
                {
                    break;
                }
                var buffer = new byte[packetLength];
                Buffer.BlockCopy(Clients[args.BufferIndex].LeftoverBytes, bytesRead, buffer, 0, packetLength);
                HandleData(ref buffer, args.BufferIndex);
                newbuffer.AddRange(buffer);

                bytesRead += packetLength;
                dataLeft -= packetLength;
            }
            if (Clients[args.BufferIndex].TotalData != dataLeft)
            {
                for (int i = 0; i < dataLeft; i++)
                {
                    Clients[args.BufferIndex].LeftoverBytes[i] = Clients[args.BufferIndex].LeftoverBytes[i + bytesRead];
                }
                Clients[args.BufferIndex].TotalData = dataLeft;
            }
            args.Bytes = newbuffer.ToArray();
            args.StreamLength = newbuffer.Count;
        }

        private void HandleData(ref byte[] buffer, int index)
        {
            if (Clients[index].Version == Main.curRelease)
            {
                return;
            }

            MemoryStream stream = new MemoryStream(buffer);

            using (BinaryReader reader = new BinaryReader(stream))
            {
                var length = reader.ReadUInt16();
                var MsgID = (PacketTypes)reader.ReadByte();
                if (Clients[index].Version == Main.curRelease && MsgID != PacketTypes.ConnectRequest)
                {
                    return;
                }
                switch (MsgID)
                {
                    case PacketTypes.ConnectRequest:
                        {
                            var version = reader.ReadString();
                            if (!int.TryParse(version.Substring(version.Length - 3), out int releaseNumber))
                            {
                                NetMessage.SendData(2, index, -1, NetworkText.FromLiteral("Invalid version string"));
                                return;
                            }
                            Clients[index].Version = releaseNumber;
                            if (!AllowedVersions.Contains(releaseNumber))
                                return;
                            NetMessage.SendData(9, index, -1, NetworkText.FromLiteral("Fixing Version..."), 1);
                            Log($"Changing version of index {index} from {Convert(releaseNumber)} => {Convert(Main.curRelease)}", color: ConsoleColor.Green);
                            buffer = new PacketFactory()
                                .SetType(1)
                                .PackString($"Terraria{Main.curRelease}")
                                .GetByteData();
                        }
                        break;
                    case PacketTypes.TileSendSquare:
                        {
                            if (Clients[index].Version >= 234)
                                return;
                            var header = reader.ReadUInt16();
                            var size = header & 32767;
                            byte changeType = 0;
                            if ((header & 0x8000) > 0)
                            {
                                changeType = reader.ReadByte();
                            }
                            var tileX = reader.ReadInt16();
                            var tileY = reader.ReadInt16();
                            buffer = new PacketFactory()
                                .SetType(20)
                                .PackInt16(tileX)
                                .PackInt16(tileY)
                                .PackByte((byte)size)
                                .PackByte((byte)size)
                                .PackByte(changeType)
                                .PackBuffer(reader.ReadToEnd())
                                .GetByteData();
                        }
                        break;
                }
            }
        }

        private void GetData(GetDataEventArgs args)
        {
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
            }
        }

        private void SendBytes(SendBytesEventArgs args)
        {
            int socketId = args.Socket.Id;
            RemoteClient client = Netplay.Clients[socketId];
            if (Clients[socketId].Version == Main.curRelease)
            {
                return;
            }
            int playerVersion = Clients[socketId].Version;
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
                                byte[] bytes = reader.ReadBytes(22);
                                string worldName = reader.ReadString();
                                byte[] bytes2 = reader.ReadBytes(103);
                                reader.ReadByte(); // Main.tenthAnniversaryWorld
                                byte[] bytes3 = reader.ReadBytes(27);
                                byte[] worldInfo = new PacketFactory()
                                    .SetType(7)
                                    .PackBuffer(bytes)
                                    .PackString(worldName)
                                    .PackBuffer(bytes2)
                                    .PackBuffer(bytes3)
                                    .GetByteData();
                                client.Socket.AsyncSend(worldInfo, 0, worldInfo.Length, client.ServerWriteCallBack);
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
                                byte[] tileSection = SectionHelper.WriteDecompressedSection(streamRead, playerVersion);
                                client.Socket.AsyncSend(tileSection, 0, tileSection.Length, client.ServerWriteCallBack);
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
                                    byte changeType = reader.ReadByte();
                                    ushort header = Math.Max(width, length);
                                    args.Handled = true;
                                    if (width != length)
                                    {
                                        Log($"/ SendTileRect - Relooping tileRect of index {socketId} because of irregular dimensions", true, ConsoleColor.Cyan);
                                        TShock.Players[socketId].SendTileRect(tileX, tileY, (byte)header, (byte)header, (TileChangeType)changeType);
                                        return;
                                    }
                                    if (changeType != 0)
                                    {
                                        header |= 32768;
                                    }
                                    var size = header & 32767;
                                    var tileWrite = new PacketFactory()
                                        .SetType(20)
                                        .PackUInt16(header);
                                    if (changeType != 0)
                                    {
                                        tileWrite.PackByte(changeType);
                                    }
                                    tileWrite.PackInt16(tileX);
                                    tileWrite.PackInt16(tileY);
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
                                                var maxTileType = MaxTileType[playerVersion];
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
                                    tileWrite.PackBuffer(reader.ReadToEnd());

                                    byte[] tileSquare = tileWrite.GetByteData();
                                    client.Socket.AsyncSend(tileSquare, 0, tileSquare.Length, client.ServerWriteCallBack);
                                }
                            }
                            break;
                        case PacketTypes.ItemDrop:
                        case PacketTypes.UpdateItemDrop:
                            {
                                var itemIdentity = reader.ReadInt16();
                                if (Main.item[itemIdentity].type > MaxItemType[playerVersion])
                                {
                                    Log($"/ ItemDrop - Blocked itemType {Main.item[itemIdentity].type} from sending to player {TShock.Players[socketId].Name}", true, ConsoleColor.DarkGreen);
                                    args.Handled = true;
                                    return;
                                }
                            }
                            break;
                        case PacketTypes.ProjectileNew:
                            {
                                if (playerVersion < 237)
                                {
                                    var identity = reader.ReadInt16();
                                    byte[] bytes = reader.ReadBytes(17);
                                    short projType = reader.ReadInt16();
                                    if (projType > MaxProjectileType[playerVersion])
                                    {
                                        var old = projType;
                                        switch (projType)
                                        {
                                            case 953:
                                                projType = 612;
                                                break;
                                            case 954:
                                                projType = 504;
                                                break;
                                            case 955:
                                                projType = 12;
                                                break;
                                            default:
                                                args.Handled = true;
                                                Log($"/ ProjectileUpdate - handled index {socketId} from exceeded maxType ({projType})", true, ConsoleColor.Red);
                                                return;
                                        }
                                        Log($"/ ProjectileUpdate - swapped type from {old} -> {projType} from previously exceeded maxType", true, ConsoleColor.DarkGreen);
                                    }

                                    BitsByte projFlags = reader.ReadByte();
                                    float AI0 = projFlags[0] ? reader.ReadSingle() : 0f;
                                    float AI1 = projFlags[1] ? reader.ReadSingle() : 0f;
                                    int bannerIdToRespondTo = projFlags[3] ? reader.ReadUInt16() : 0;
                                    var projWrite = new PacketFactory()
                                        .SetType(27)
                                        .PackInt16(identity)
                                        .PackBuffer(bytes)
                                        .PackInt16(projType)
                                        .PackByte(projFlags);
                                    if (AI0 != 0f) projWrite.PackSingle(AI0);
                                    if (AI1 != 0f) projWrite.PackSingle(AI1);

                                    projWrite.PackBuffer(reader.ReadToEnd());

                                    byte[] projectilePacket = projWrite.GetByteData();
                                    client.Socket.AsyncSend(projectilePacket, 0, projectilePacket.Length, client.ServerWriteCallBack);
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
                                var MaxNpcID = MaxNPCID[playerVersion];
                                if (type > MaxNpcID)
                                {
                                    Log($"/ NpcUpdate - Preventing NPC packet from sending to index {socketId} because it exceeds npcType limit", true, ConsoleColor.Cyan);
                                    args.Handled = true;
                                }
                            }
                            break;
                        case PacketTypes.PlayerBuff:
                            {
                                var playerId = reader.ReadByte();

                                var buffWrite = new PacketFactory();
                                buffWrite.SetType(50);
                                buffWrite.PackByte(playerId);
                                for (int i = 0; i < 22; i++)
                                {
                                    var buffType = reader.ReadUInt16();
                                    if (buffType > MaxBuffType[playerVersion])
                                    {
                                        Log($"/ PlayerBuff - Changed buffType {buffType} to 0 for player {TShock.Players[socketId].Name}", true, ConsoleColor.DarkGreen);
                                        buffType = 0;
                                    }
                                    buffWrite.PackUInt16(buffType);
                                }
                                var playerBuff = buffWrite.GetByteData();
                                client.Socket.AsyncSend(playerBuff, 0, playerBuff.Length, client.ServerWriteCallBack);
                                args.Handled = true;
                            }
                            break;
                        case PacketTypes.PlayerAddBuff:
                            {
                                var playerId = reader.ReadByte();
                                var buff = reader.ReadUInt16();
                                if (buff > MaxBuffType[playerVersion])
                                {
                                    Log($"/ PlayerAddBuff - Blocked buff add ({buff}) to player {TShock.Players[socketId].Name}", true, ConsoleColor.DarkGreen);
                                    args.Handled = true;
                                    return;
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
                        var playerVersion = Clients[index].Version;
                        if (playerVersion == Main.curRelease)
                        {
                            return;
                        }
                        var MaxNpcID = MaxNPCID[playerVersion];
                        if (npcID > MaxNpcID)
                        {
                            args.Handled = true;
                        }
                        break;
                }
            }
        }

        private void OnLeave(LeaveEventArgs args)
        {
            Clients[args.Who] = null;
        }

        private string Convert(int version)
        {
            switch (version)
            {
                case 230:
                    return "v1.4.0.5";
                case 233:
                    return "v1.4.1.1";
                case 234:
                    return "v1.4.1.2";
                case 235:
                    return "v1.4.2";
                case 236:
                    return "v1.4.2.1";
                case 237:
                    return "v1.4.2.2";
                case 238:
                    return "v1.4.2.3";
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
