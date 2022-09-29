﻿using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

using OTAPI.Tile;

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
    public class CrossplayPlugin : TerrariaPlugin
    {
        public override string Name => "Crossplay";
        public override string Author => "Moneylover3246";
        public override string Description => "Enables crossplay for terraria";
        public override Version Version => new Version("1.10.2");

        public static CrossplayConfig Config = new CrossplayConfig();

        public static string ConfigPath => Path.Combine(TShock.SavePath, "Crossplay.json");

        private readonly List<int> AllowedVersions = new List<int>() { 230, 233, 234, 235, 236, 237, 238, 242, 243, 244, 245, 246, 247, 269, 270 };

        private readonly int[] ClientVersions = new int[Main.maxPlayers];

        private static int ServerVersion;

        public static readonly Dictionary<int, int> MaxNPCs = new Dictionary<int, int>()
        {
            { 230, 662 },
            { 233, 664 },
            { 234, 664 },
            { 235, 664 },
            { 236, 664 },
            { 237, 666 },
            { 238, 667 },
            { 242, 669 },
            { 243, 669 },
            { 244, 669 },
            { 245, 669 },
            { 246, 669 },
            { 247, 669 },
            { 269, 688 },
            { 270, 688 }
        };
        public static readonly Dictionary<int, int> MaxTiles = new Dictionary<int, int>()
        {
            { 230, 622 },
            { 233, 623 },
            { 234, 623 },
            { 235, 623 },
            { 236, 623 },
            { 237, 623 },
            { 238, 623 },
            { 242, 624 },
            { 243, 624 },
            { 244, 624 },
            { 245, 624 },
            { 246, 624 },
            { 247, 624 },
            { 269, 693 },
            { 270, 693 }
        };
        public static readonly Dictionary<int, int> MaxBuffs = new Dictionary<int, int>()
        {
            { 230, 322 },
            { 233, 329 },
            { 234, 329 },
            { 235, 329 },
            { 236, 329 },
            { 237, 329 },
            { 238, 329 },
            { 242, 335 },
            { 243, 335 },
            { 244, 335 },
            { 245, 335 },
            { 246, 335 },
            { 247, 335 },
            { 269, 355 },
            { 270, 355 }
        };
        public static readonly Dictionary<int, int> MaxProjectiles = new Dictionary<int, int>()
        {
            { 230, 949 },
            { 233, 953 },
            { 234, 953 },
            { 235, 955 },
            { 236, 955 },
            { 237, 955 },
            { 238, 955 },
            { 242, 970 },
            { 243, 970 },
            { 244, 970 },
            { 245, 970 },
            { 246, 970 },
            { 247, 970 },
            { 269, 1022 },
            { 270, 1022 }
        };
        public static readonly Dictionary<int, int> MaxItems = new Dictionary<int, int>()
        {
            { 230, 5044 },
            { 233, 5087 },
            { 234, 5087 },
            { 235, 5087 },
            { 236, 5087 },
            { 237, 5087 },
            { 238, 5087 },
            { 242, 5124 },
            { 243, 5124 },
            { 244, 5124 },
            { 245, 5124 },
            { 246, 5124 },
            { 247, 5124 },
            { 269, 5453 },
            { 270, 5453 }
        };

        public CrossplayPlugin(Main game) : base(game)
        {
            Order = -1;
        }

        public override void Initialize()
        {
            ServerApi.Hooks.GameInitialize.Register(this, OnInitialize);
            ServerApi.Hooks.GamePostInitialize.Register(this, OnPostInitialize);
            ServerApi.Hooks.NetGetData.Register(this, OnGetData, int.MaxValue);
            ServerApi.Hooks.NetSendBytes.Register(this, SendBytes, int.MinValue);
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
                ServerApi.Hooks.GamePostInitialize.Deregister(this, OnPostInitialize);
                ServerApi.Hooks.NetGetData.Deregister(this, OnGetData);
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
            if (Config.Settings.FakeVersionEnabled)
            {
                ServerVersion = Config.Settings.FakeVersion;
                Log($"Fake Version set to {ServerVersion}", false, ConsoleColor.Yellow);
            }
        }

        private void OnPostInitialize(EventArgs e)
        {
            ServerVersion = Main.curRelease;
            if (Config.Settings.FakeVersionEnabled)
            {
                ServerVersion = Config.Settings.FakeVersion;
                Log($"Fake Version set to {ServerVersion}", false, ConsoleColor.Yellow);
            }

            StringBuilder sb = new StringBuilder();
            sb.Append("Crossplay has been enabled & has whitelisted the following versions:\n");
            sb.Append(string.Join(", ", AllowedVersions.Select(v => ParseVersion(v))));
            sb.Append("\n\nIf there are any issues please report them here: https://github.com/Moneylover3246/Crossplay");

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
            MemoryStream stream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);
            int index = args.Msg.whoAmI;
            using (BinaryReader reader = new BinaryReader(stream))
            {
                if ((int)args.MsgID == 1)
                {
                    string clientVersion = reader.ReadString();
                    if (!int.TryParse(clientVersion.Substring(clientVersion.Length - 3), out int versionNum))
                    {
                        return;
                    }
                    if (versionNum == ServerVersion)
                    {
                        return;
                    }
                    if (!AllowedVersions.Contains(versionNum) && !Config.Settings.FakeVersionEnabled)
                    {
                        return;
                    }
                    ClientVersions[index] = versionNum;
                    NetMessage.SendData(9, args.Msg.whoAmI, -1, NetworkText.FromLiteral("Fixing Version..."), 1);
                    byte[] connectRequest = new PacketFactory()
                        .SetType(1)
                        .PackString($"Terraria{ServerVersion}")
                        .GetByteData();
                    Log($"Changing version of index {args.Msg.whoAmI} from {ParseVersion(versionNum)} => {ParseVersion(ServerVersion)}", color: ConsoleColor.Green);

                    Buffer.BlockCopy(connectRequest, 0, args.Msg.readBuffer, args.Index - 3, connectRequest.Length);
                    return;
                }
                if (ClientVersions[index] == 0 && args.MsgID != PacketTypes.PlayerInfo)
                {
                    return;
                }
                int version = ClientVersions[index];
                switch (args.MsgID)
                {
                    case PacketTypes.PlayerInfo:
                        {
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
                        }
                        break;
                    case PacketTypes.TileSendSquare:
                        {
                            if (version > 233)
                            {
                                return;
                            }
                            ushort header = reader.ReadUInt16();
                            int size = header & 32767;
                            byte changeType = 0;
                            if ((header & 0x8000) > 0)
                            {
                                changeType = reader.ReadByte();
                            }
                            short tileX = reader.ReadInt16();
                            short tileY = reader.ReadInt16();

                            args.Handled = true;
                            GetDataHandlers.SendTileRectEventArgs strEventArgs = new GetDataHandlers.SendTileRectEventArgs()
                            {
                                Player = TShock.Players[args.Msg.whoAmI],
                                TileX = tileX,
                                TileY = tileY,
                                ChangeType = (TileChangeType)changeType,
                                Data = stream,
                                Width = (byte)size,
                                Length = (byte)size,
                            };
                            GetDataHandlers.SendTileRect.Invoke(null, strEventArgs);
                            if (strEventArgs.Handled)
                            {
                                return;
                            }

                            for (int x = tileX; x < tileX + size; x++)
                            {
                                for (int y = tileY; y < tileY + size; y++)
                                {
                                    ITile tile = Main.tile[x, y];
                                    TileProcessor.ProcessTile(tile, reader);
                                }
                            }
                            WorldGen.RangeFrame(tileX, tileY, tileX + size, tileY + size);
                            NetMessage.SendData(20, -1, index, null, tileX, tileY, size, size, changeType);
                        }
                        break;
                }
            }
        }

        private void SendBytes(SendBytesEventArgs args)
        {
            int socketId = args.Socket.Id;
            RemoteClient client = Netplay.Clients[socketId];
            if (ClientVersions[socketId] == 0)
            {
                return;
            }
            int playerVersion = ClientVersions[socketId];
            MemoryStream stream = new MemoryStream(args.Buffer, 0, args.Buffer.Length);
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
                                if (playerVersion >= 238)
                                {
                                    return;
                                }
                                byte[] bytes = reader.ReadBytes(22);
                                string worldName = reader.ReadString();
                                byte[] bytes2 = reader.ReadBytes(103);
                                reader.BaseStream.Position++; // bitFlags[8]
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
                                byte[] tileSection = SectionHelper.CompressTileSection(streamRead, playerVersion);
                                client.Socket.AsyncSend(tileSection, 0, tileSection.Length, client.ServerWriteCallBack);
                                args.Handled = true;
                            }
                            break;
                        case PacketTypes.TileSendSquare:
                            {
                                short tileX = reader.ReadInt16();
                                short tileY = reader.ReadInt16();
                                ushort width = reader.ReadByte();
                                ushort length = reader.ReadByte();
                                byte changeType = reader.ReadByte();
                                ushort header = Math.Max(width, length);
                                if (width != length)
                                {
                                    args.Handled = true;
                                    Log($"/ SendTileRect - Relooping tileRect for index {socketId} because of irregular dimensions", true, ConsoleColor.Cyan);
                                    TShock.Players[socketId].SendTileRect(tileX, tileY, (byte)header, (byte)header, (TileChangeType)changeType);
                                    return;
                                }
                                if (changeType != 0)
                                {
                                    header |= 32768;
                                }
                                int size = header & 32767;
                                PacketFactory tileWrite = new PacketFactory();
                                tileWrite.SetType(20);
                                if (playerVersion < 234)
                                {
                                    tileWrite.PackUInt16(header);
                                    if (changeType != 0)
                                    {
                                        tileWrite.PackByte(changeType);
                                    }
                                    tileWrite.PackInt16(tileX);
                                    tileWrite.PackInt16(tileY);
                                }
                                else
                                {
                                    tileWrite.PackInt16(tileX)
                                        .PackInt16(tileY)
                                        .PackByte((byte)width)
                                        .PackByte((byte)length)
                                        .PackByte(changeType);
                                }
                                args.Handled = true;
                                long position = reader.BaseStream.Position;

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
                                            reader.BaseStream.Position++; // color
                                        }
                                        if (tileflags2[3])
                                        {
                                            reader.BaseStream.Position++; // wall color
                                        }
                                        if (tileflags[0])
                                        {
                                            ushort tileType = reader.ReadUInt16();
                                            int maxTile = MaxTiles[playerVersion];
                                            if (tileType > maxTile)
                                            {
                                                stream.Position -= 2;
                                                writer.Write((ushort)(Main.tileFrameImportant[tileType] ? 72 : 1));
                                            }
                                            if (Main.tileFrameImportant[tileType])
                                            {
                                                reader.BaseStream.Position += 4; // FrameX/Y
                                            }
                                        }
                                        if (tileflags[2])
                                        {
                                            reader.BaseStream.Position += 2; // Wall type
                                        }
                                        if (tileflags[3])
                                        {
                                            reader.BaseStream.Position += 2; // Liquid type + amount
                                        }
                                    }
                                }
                                stream.Position = position;
                                tileWrite.PackBuffer(reader.ReadToEnd());

                                byte[] tileRect = tileWrite.GetByteData();
                                client.Socket.AsyncSend(tileRect, 0, tileRect.Length, client.ServerWriteCallBack);
                            }
                            break;
                        case PacketTypes.ItemDrop:
                        case PacketTypes.UpdateItemDrop:
                            {
                                short itemIdentity = reader.ReadInt16();
                                if (Main.item[itemIdentity].type > MaxItems[playerVersion])
                                {
                                    Log($"/ ItemDrop - Blocked itemType {Main.item[itemIdentity].type} from sending to player {TShock.Players[socketId].Name}", true, ConsoleColor.DarkGreen);
                                    args.Handled = true;
                                    return;
                                }
                            }
                            break;
                        case PacketTypes.ProjectileNew:
                            {
                                short identity = reader.ReadInt16();
                                byte[] bytes = reader.ReadBytes(17);
                                short projType = reader.ReadInt16();
                                if (projType > MaxProjectiles[playerVersion])
                                {
                                    short old = projType;
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
                                if (playerVersion > 236)
                                {
                                    return;
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
                            break;
                        case PacketTypes.NpcUpdate:
                            {
                                reader.BaseStream.Position += 20;
                                BitsByte npcFlags = reader.ReadByte();
                                reader.BaseStream.Position++;
                                for (int i = 2; i < 6; i++)
                                {
                                    if (npcFlags[i])
                                    {
                                        reader.BaseStream.Position += 4;
                                    }
                                }
                                int type = reader.ReadInt16();
                                int maxNpcId = MaxNPCs[playerVersion];
                                if (type > maxNpcId)
                                {
                                    Log($"/ NpcUpdate - Preventing NPC packet from sending to index {socketId} because it exceeds npcType limit", true, ConsoleColor.Cyan);
                                    args.Handled = true;
                                }
                            }
                            break;
                        case PacketTypes.PlayerBuff:
                            {
                                byte plr = reader.ReadByte();
                                var buffWrite = new PacketFactory()
                                    .SetType(50)
                                    .PackByte(plr);
                                for (int i = 0; i < 22; i++)
                                {
                                    ushort buffType = reader.ReadUInt16();
                                    if (buffType > MaxBuffs[playerVersion])
                                    {
                                        Log($"/ PlayerBuff - Changed buffType {buffType} to 0 for player {TShock.Players[socketId].Name}", true, ConsoleColor.DarkGreen);
                                        buffType = 0;
                                    }
                                    buffWrite.PackUInt16(buffType);
                                }
                                byte[] data = buffWrite.GetByteData();
                                client.Socket.AsyncSend(data, 0, data.Length, client.ServerWriteCallBack);
                                args.Handled = true;
                            }
                            break;
                        case PacketTypes.PlayerAddBuff:
                            {
                                byte playerId = reader.ReadByte();
                                ushort buff = reader.ReadUInt16();
                                if (buff > MaxBuffs[playerVersion])
                                {
                                    Log($"/ PlayerAddBuff - Blocked buff add ({buff}) to player {TShock.Players[socketId].Name}", true, ConsoleColor.DarkGreen);
                                    args.Handled = true;
                                    return;
                                }
                            }
                            break;
                    }
                }
                catch (IOException)
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
                reader.BaseStream.Position += 3;
                ushort netModuleID = reader.ReadUInt16();
                switch (netModuleID)
                {
                    case 4:
                        {
                            byte unlockType = reader.ReadByte();
                            short npcId = reader.ReadInt16();
                            int index = GetIndexFromSocket(args.socket);
                            int playerVersion = ClientVersions[index];
                            if (playerVersion == 0)
                            {
                                return;
                            }
                            int maxNpcs = MaxNPCs[playerVersion];
                            if (npcId > maxNpcs)
                            {
                                Log($"/ NetModule (Bestiary) Blocked NpcType {maxNpcs} for index: {index}", true, ConsoleColor.Yellow);
                                args.Handled = true;
                            }
                        }
                        break;
                    case 5:
                        {
                            short itemType = reader.ReadInt16();
                            int index = GetIndexFromSocket(args.socket);
                            int playerVersion = ClientVersions[index];
                            if (playerVersion == 0)
                            {
                                return;
                            }
                            if (itemType > MaxItems[playerVersion])
                            {
                                Log($"/ NetModule (Creative Unlocks) Blocked ItemType {itemType} for index: {index}", true, ConsoleColor.Yellow);
                                args.Handled = true;
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

        private string ParseVersion(int version)
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
                case 242:
                    return "v1.4.3";
                case 243:
                    return "v1.4.3.1";
                case 244:
                    return "v1.4.3.2";
                case 245:
                    return "v1.4.3.3";
                case 246:
                    return "v1.4.3.4";
                case 247:
                    return "v1.4.3.5";
                case 248:
                    return "v1.4.3.6";
                case 269:
                    return "v1.4.4";
                case 270:
                    return "v1.4.4.1";
            }
            return $"Unknown{version}";
        }

        public static void Log(string message, bool debug = false, ConsoleColor color = ConsoleColor.White)
        {
            if (debug)
            {
                if (Config.Settings.EnablePacketDebugging)
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
