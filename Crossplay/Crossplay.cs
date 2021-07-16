using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using OTAPI.Tile;
using Terraria;
using Terraria.Net.Sockets;
using TerrariaApi.Server;
using TShockAPI;
using static TShockAPI.GetDataHandlers;

namespace Crossplay
{
    [ApiVersion(2, 1)]
    public class Crossplay : TerrariaPlugin
    {
        public static int Header = 3;
        public override string Name => "Crossplay";
        public override string Author => "Moneylover3246";
        public override string Description => "Enables crossplay for terraria";
        public override Version Version => new Version("1.0");

        public bool[] IsPC = new bool[Main.maxPlayers];
        public Crossplay(Main game) : base(game)
        {
            Order = -1;
        }
        public override void Initialize()
        {
            ServerApi.Hooks.NetGetData.Register(this, GetData, int.MaxValue);
            ServerApi.Hooks.NetSendBytes.Register(this, SendBytes, -int.MaxValue);
            ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                ServerApi.Hooks.NetGetData.Deregister(this, GetData);
                ServerApi.Hooks.NetSendBytes.Deregister(this, SendBytes);
                ServerApi.Hooks.ServerLeave.Deregister(this, OnLeave);
            }
            base.Dispose(disposing);
        }
        // Try to handle data manipulation before tshock + other plugins do so
        private void GetData(GetDataEventArgs args)
        {
            TSPlayer player = TShock.Players[args.Msg.whoAmI];
            MemoryStream memoryStream = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length);
            using (BinaryReader reader = new BinaryReader(memoryStream))
            {
                try
                {
                    if (IsPC[args.Msg.whoAmI] || args.MsgID == PacketTypes.ConnectRequest)
                    {
                        switch (args.MsgID)
                        {
                            case PacketTypes.ConnectRequest:
                                {
                                    string version = reader.ReadString();
                                    if (version == "Terraria238")
                                    {
                                        player.SendData(PacketTypes.Status, "Fixing Version...", 1);
                                        IsPC[args.Msg.whoAmI] = true;
                                        byte[] buffer = new PacketFactory().SetType(1).PackString("Terraria" + Main.curRelease).GetByteData();
                                        Console.ForegroundColor = ConsoleColor.Green;
                                        Console.WriteLine($"[Crossplay] Changing version of index {args.Msg.whoAmI} from {Convert(version)} => v1.4.0.5");
                                        Console.ResetColor();
                                        args.Msg.readBuffer.SwapBytes(args.Index - Header, args.Length + (Header - 1), buffer);
                                    }
                                }
                                break;
                            case PacketTypes.TileSendSquare:
                                {
                                    var tileX = reader.ReadInt16();
                                    var tileY = reader.ReadInt16();
                                    var width = reader.ReadByte();
                                    var length = reader.ReadByte();
                                    var size = Math.Min(width, length);
                                    var tileChangeType = reader.ReadByte();
                                    args.Handled = true;
                                    var eventArgs = new SendTileSquareEventArgs()
                                    {
                                        Player = player,
                                        Data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length),
                                        Size = size,
                                        TileX = tileX,
                                        TileY = tileY
                                    };
                                    SendTileSquare.Invoke(null, eventArgs);
                                    if (!eventArgs.Handled)
                                    {
                                        BitsByte flags = 0;
                                        BitsByte flags2 = 0;
                                        for (int x = tileX; x < tileX + width; x++)
                                        {
                                            for (int y = tileY; y < tileY + length; y++)
                                            {
                                                if (Main.tile[x, y] == null)
                                                {
                                                    Main.tile[x, y] = new Tile();
                                                }
                                                ITile tile = Main.tile[x, y];
                                                bool active = tile.active();
                                                flags = reader.ReadByte();
                                                flags2 = reader.ReadByte();
                                                tile.active(flags[0]);
                                                tile.wall = (ushort)(flags[2] ? 1 : 0);
                                                bool liquid = flags[3];
                                                if (Main.netMode != 2)
                                                {
                                                    tile.liquid = (byte)(liquid ? 1 : 0);
                                                }
                                                tile.wire(flags[4]);
                                                tile.halfBrick(flags[5]);
                                                tile.actuator(flags[6]);
                                                tile.inActive(flags[7]);
                                                tile.wire2(flags2[0]);
                                                tile.wire3(flags2[1]);
                                                if (flags2[2])
                                                {
                                                    tile.color(reader.ReadByte());
                                                }
                                                if (flags2[3])
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
                                                    else if (!active || tile.type != type5)
                                                    {
                                                        tile.frameX = -1;
                                                        tile.frameY = -1;
                                                    }
                                                    byte slope = 0;
                                                    if (flags2[4])
                                                    {
                                                        slope += 1;
                                                    }
                                                    if (flags2[5])
                                                    {
                                                        slope += 2;
                                                    }
                                                    if (flags2[6])
                                                    {
                                                        slope += 4;
                                                    }
                                                    tile.slope(slope);
                                                }
                                                tile.wire4(flags2[7]);
                                                if (tile.wall > 0)
                                                {
                                                    tile.wall = reader.ReadUInt16();
                                                }
                                                if (liquid)
                                                {
                                                    tile.liquid = reader.ReadByte();
                                                    tile.liquidType(reader.ReadByte());
                                                }
                                            }
                                        }
                                        WorldGen.RangeFrame(tileX, tileY, tileX + width, tileY + length);
                                        NetMessage.TrySendData(20, -1, args.Msg.whoAmI, null, Math.Max(width, length), tileX, tileY);
                                    }
                                }
                                break;
                            case PacketTypes.NpcAddBuff:
                                var npcID = reader.ReadInt16();
                                var buff = reader.ReadUInt16();
                                var time = reader.ReadInt16();
                                if (buff > 322)
                                {
                                    args.Handled = true;
                                    player.SendData(PacketTypes.NpcUpdateBuff, null, npcID);
                                }
                                break;
                            case PacketTypes.ProjectileNew:
                                {
                                    var projIndex = reader.ReadInt16();
                                    var position = reader.ReadVector2();
                                    var velocity = reader.ReadVector2();
                                    var owner = reader.ReadByte();
                                    var projType = reader.ReadInt16();
                                    BitsByte projFlags = reader.ReadByte();
                                    if (projFlags[0])
                                    {
                                        reader.ReadSingle();
                                    }
                                    if (projFlags[1])
                                    {
                                        reader.ReadSingle();
                                    }
                                    if (!projFlags[3])
                                    {
                                        return;
                                    }
                                    var hasBannerToRespondTo = reader.ReadUInt16();
                                    byte[] buffer = reader.ReadToEnd();
                                    for (int i = 0; i < buffer.Length; i++)
                                    {
                                        args.Msg.readBuffer[(int)(reader.BaseStream.Position + args.Index - 2)] = buffer[i];
                                    }
                                }
                                break;
                        }
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
            try
            {
                if (IsPC[playerIndex])
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
                                    byte[] data = new PacketFactory()
                                        .SetType(3)
                                        .PackByte(playerID)
                                        .PackByte(0)
                                        .GetByteData();
                                    TShock.Players[playerIndex].SendRawData(data);
                                    args.Handled = true;
                                }
                                break;
                            case PacketTypes.WorldInfo:
                                {
                                    byte[] buffer = reader.ReadBytes(22);
                                    string worldName = reader.ReadString();
                                    byte[] buffer2 = reader.ReadBytes(103);
                                    byte[] buffer3 = reader.ReadBytes(27);
                                    byte[] data = new PacketFactory()
                                        .SetType(7)
                                        .PackBuffer(buffer)
                                        .PackString(worldName)
                                        .PackBuffer(buffer2)
                                        .PackByte(0)
                                        .PackBuffer(buffer3)
                                        .GetByteData();
                                    TShock.Players[playerIndex].SendRawData(data);
                                    args.Handled = true;
                                }
                                break;
                            case PacketTypes.TileSendSquare:
                                {
                                    ushort size = reader.ReadUInt16();
                                    byte changeType = 0;
                                    if ((size & 32768) > 0)
                                    {
                                        changeType = reader.ReadByte();
                                    }
                                    short tileX = reader.ReadInt16();
                                    short tileY = reader.ReadInt16();
                                    byte[] tiledata = reader.ReadToEnd();
                                    TShock.Players[playerIndex].SendRawData(new PacketFactory()
                                        .SetType(20)
                                        .PackInt16(tileX)
                                        .PackInt16(tileY)
                                        .PackByte((byte)size)
                                        .PackByte((byte)size)
                                        .PackByte(changeType)
                                        .PackBuffer(tiledata)
                                        .GetByteData());
                                    args.Handled = true;
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

        private void OnLeave(LeaveEventArgs args)
        {
            IsPC[args.Who] = false;
        }

        private int GetIndexFromSocket(ISocket socket)
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

        private string Convert(string protocol)
        {
            switch (protocol)
            {
                case "Terraria230":
                    return "v1.4.0.5";
                case "Terraria233":
                    return "v1.4.1.1";
                case "Terraria234":
                    return "v1.4.1.2";
                case "Terraria238":
                    return "v1.4.2.3";
            }
            return "";
        }
    }
}
