using System;
using System.IO;
using OTAPI.Tile;
using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;

namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class Crossplay : TerrariaPlugin
    {
        public static int packetHeader = 3;
        public override string Name => "Crossplay for Terraria";
        public override string Author => "Moneylover3246";
        public override string Description => "Enables crossplay for terraria";
        public override Version Version => new Version("1.4.2.3");

        public bool[] IsMobile = new bool[Main.maxPlayers];
        public Crossplay(Main game) : base(game)
        {
            Order = -1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, GetData, -1);
            ServerApi.Hooks.NetSendBytes.Register(this, SendBytes, 10);
            ServerApi.Hooks.NetSendNetData.Register(this, HandleNetModules);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, GetData);
                ServerApi.Hooks.NetSendBytes.Deregister(this, SendBytes);
                ServerApi.Hooks.NetSendNetData.Deregister(this, HandleNetModules);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        // Try to handle data manipulation before tshock + other plugins do so
        private void GetData(GetDataEventArgs args)
        {
            MemoryStream memoryStream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                try
                {
                    switch (args.MsgID)
                    {
                        case PacketTypes.ConnectRequest:
                            {
                                string version = reader.ReadString();
                                if (version == "Terraria230")
                                {
                                    IsMobile[args.Msg.whoAmI] = true;
                                    byte[] buffer = new PacketFactory().SetType(1).PackString("Terraria" + Main.curRelease).GetByteData();
                                    Console.ForegroundColor = ConsoleColor.Green;
                                    Console.WriteLine("[Crossplay] Fixing mobile for index " + args.Msg.whoAmI);
                                    Console.ResetColor();
                                    args.Msg.readBuffer.SwapBytes(args.Index - packetHeader, args.Length + (packetHeader - 1), buffer);
                                }
                            }
                            break;
                        case PacketTypes.TileSendSquare:
                            {
                                if (IsMobile[args.Msg.whoAmI])
                                {
                                    ushort size = reader.ReadUInt16();
                                    byte changeType = 0;
                                    if ((size & 32768) > 0)
                                    {
                                        changeType = reader.ReadByte();
                                    }
                                    short tileX = reader.ReadInt16();
                                    short tileY = reader.ReadInt16();
                                    BitsByte tileFlags = 0;
                                    BitsByte tileFlags2 = 0;
                                    for (int X = tileX; X < tileX + (int)size; X++)
                                    {
                                        for (int Y = tileY; Y < tileY + (int)size; Y++)
                                        {
                                            if (Main.tile[X, Y] == null)
                                            {
                                                Main.tile[X, Y] = new Tile();
                                            }
                                            ITile tile = Main.tile[X, Y];
                                            tileFlags = reader.ReadByte();
                                            tileFlags2 = reader.ReadByte();
                                            tile.active(tileFlags[0]);
                                            tile.wall = (ushort)(tileFlags[2] ? 1 : 0);
                                            if (Main.netMode != 2)
                                            {
                                                tile.liquid = (byte)(tileFlags[3] ? 1 : 0);
                                            }
                                            tile.wire(tileFlags[4]);
                                            tile.halfBrick(tileFlags[5]);
                                            tile.actuator(tileFlags[6]);
                                            tile.inActive(tileFlags[7]);
                                            tile.wire2(tileFlags2[0]);
                                            tile.wire3(tileFlags2[1]);
                                            if (tileFlags2[2])
                                            {
                                                tile.color(reader.ReadByte());
                                            }
                                            if (tileFlags2[3])
                                            {
                                                tile.wallColor(reader.ReadByte());
                                            }
                                            if (tile.active())
                                            {
                                                int type5 = tile.type;
                                                tile.type = reader.ReadUInt16();
                                                if (Main.tileFrameImportant[tile.type])
                                                {
                                                    tile.frameX = reader.ReadInt16();
                                                    tile.frameY = reader.ReadInt16();
                                                }
                                                else if (!tile.active() || tile.type != type5)
                                                {
                                                    tile.frameX = -1;
                                                    tile.frameY = -1;
                                                }
                                                byte slope = 0;
                                                if (tileFlags2[4])
                                                {
                                                    slope += 1;
                                                }
                                                if (tileFlags2[5])
                                                {
                                                    slope += 2;
                                                }
                                                if (tileFlags2[6])
                                                {
                                                    slope += 4;
                                                }
                                                tile.slope(slope);
                                            }
                                            tile.wire4(tileFlags2[7]);
                                            if (tile.wall > 0)
                                            {
                                                tile.wall = reader.ReadUInt16();
                                            }
                                            if (tileFlags[3])
                                            {
                                                tile.liquid = reader.ReadByte();
                                                tile.liquidType(reader.ReadByte());
                                            }
                                        }
                                    }
                                    WorldGen.RangeFrame(tileX, tileY, tileX + size, tileY + size);
                                    NetMessage.SendData(20, -1, args.Msg.whoAmI, null, tileX, tileY, size, size, changeType);
                                    args.Handled = true;
                                }
                            }
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                }
            }
        }

        // Try to handle data manipulation after other plugins do so
        private void SendBytes(SendBytesEventArgs args)
        {
            int playerIndex = args.Socket.Id;
            RemoteClient client = Netplay.Clients[playerIndex];
            try
            {
                if (IsMobile[playerIndex])
                {
                    using (BinaryReader reader = new BinaryReader(new MemoryStream(args.Buffer, 0, args.Buffer.Length)))
                    {
                        int packetlength = reader.ReadInt16();
                        int msgID = reader.ReadByte();
                        switch ((PacketTypes)msgID)
                        {
                            case PacketTypes.ContinueConnecting:
                                {
                                    byte playerID = reader.ReadByte();
                                    bool value = reader.ReadBoolean();
                                    byte[] data = new PacketFactory()
                                        .SetType(3)
                                        .PackByte(playerID)
                                        .GetByteData();
                                    TShock.Players[playerIndex].SendRawData(data);
                                }
                                break;
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
                                    TShock.Players[playerIndex].SendRawData(data);
                                    Array.Clear(args.Buffer, 0, args.Buffer.Length);
                                    args.Handled = true;
                                }
                                break;
                            case PacketTypes.TileSendSquare:
                                {
                                    short tileX = reader.ReadInt16();
                                    short tileY = reader.ReadInt16();
                                    ushort width = reader.ReadByte();
                                    ushort length = reader.ReadByte();
                                    byte tileChangeType = reader.ReadByte();
                                    ushort size = Math.Max(width, length);
                                    PacketFactory data = new PacketFactory()
                                        .SetType(20)
                                        .PackUInt16(size);
                                    if ((size & 0x8000) != 0)
                                    {
                                        data.PackByte(tileChangeType);
                                    }
                                    data.PackInt16(tileX);
                                    data.PackInt16(tileY);
                                    data.PackBuffer(reader.ReadBytes((int)(reader.BaseStream.Length - reader.BaseStream.Position)));
                                    byte[] buffer = data.GetByteData();
                                    TShock.Players[playerIndex].SendRawData(buffer);
                                    args.Handled = true;
                                }
                                break;
                            case PacketTypes.ProjectileNew:
                                {
                                    reader.ReadBytes(19);
                                    short projectileType = reader.ReadInt16();
                                    if (projectileType > 949)
                                    {
                                        Array.Clear(args.Buffer, 0, args.Buffer.Length);
                                        args.Handled = true;
                                    }
                                }
                                break;
                            case PacketTypes.NpcUpdate:
                                {
                                    short npcID = reader.ReadInt16();
                                    NPC npc = Main.npc[npcID];
                                    if (npc.type > 662)
                                    {
                                        Array.Clear(args.Buffer, 0, args.Buffer.Length);
                                        args.Handled = true;
                                    }
                                }
                                break;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
            }
        }

        /*
         * As of (shortly after) the release of TShock 1.4.2.3, TerrariaServerAPI introduced the NetSendNetData hook,
         * which allows for NetModules to be read on non-proxy platforms, so LoadNetModule can be fixed for TShock.
         */
        private void HandleNetModules(SendNetDataEventArgs args)
        {
            byte[] moduleData = args.packet.Buffer.Data;
            using (BinaryReader reader = new BinaryReader(new MemoryStream(moduleData)))
            {
                reader.ReadInt16(); // Packet Length
                reader.ReadByte(); // Msg Type
                ushort netModuleID = reader.ReadUInt16();
                switch (netModuleID)
                {
                    case 4:
                        byte unlockType = reader.ReadByte();
                        short npcID = reader.ReadInt16();
                        if (npcID > 662)
                        {
                            int index = GetIndexFromSocket(args.socket);
                            if (IsMobile[index])
                            {
                                args.Handled = true;
                                return;
                            }
                        }
                        break;
                    case 5:
                        short itemID = reader.ReadInt16();
                        if (itemID > 5044)
                        {
                            int index = GetIndexFromSocket(args.socket);
                            if (IsMobile[index])
                            {
                                args.Handled = true;
                                return;
                            }
                        }
                        break;
                }
            }
        }
        private void OnLeave(LeaveEventArgs args)
        {
            IsMobile[args.Who] = false;
        }

        private int GetIndexFromSocket(ISocket socket)
        {
            for (int i = 0; i < 255; i++)
            {
                if (Netplay.Clients[i] != null && Netplay.Clients[i].Socket == socket)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
