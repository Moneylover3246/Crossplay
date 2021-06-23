using System;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
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
            ServerApi.Hooks.NetGetData.Register(this, GetData, 1);
            ServerApi.Hooks.NetSendBytes.Register(this, SendBytes, 10);
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
                    switch (args.MsgID)
                    {
                        case PacketTypes.ConnectRequest:
                            {
                                string version = reader.ReadString();
                                if (version == "Terraria238")
                                {
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
                                if (IsPC[args.Msg.whoAmI])
                                {
                                    var tileX = reader.ReadInt16();
                                    var tileY = reader.ReadInt16();
                                    var width = reader.ReadByte();
                                    var length = reader.ReadByte();
                                    var tileChangeType = reader.ReadByte();

                                    var eventArgs = new SendTileSquareEventArgs()
                                    {
                                        Player = player,
                                        Data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length),
                                        Size = Math.Min(width, length),
                                        TileX = tileX,
                                        TileY = tileY
                                    };
                                    SendTileSquare.Invoke(null, eventArgs);
                                    args.Handled = true;
                                }
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
                                var hasBannerToRespondTo = projFlags[3] ? reader.ReadUInt16() : 0;
                                var damage = projFlags[4] ? reader.ReadInt16() : 0;
                                var knockback = projFlags[5] ? reader.ReadSingle() : 0;
                                var index = TShock.Utils.SearchProjectile(projIndex, owner);
                                args.Handled = true;
                                var eventArgs = new NewProjectileEventArgs
                                {
                                    Data = new MemoryStream(args.Msg.readBuffer, args.Index, args.Length),
                                    Identity = projIndex,
                                    Position = position,
                                    Velocity = velocity,
                                    Knockback = knockback,
                                    Damage = (short)damage,
                                    Owner = owner,
                                    Type = projType,
                                    Index = index,
                                    Player = player,
                                };
                                NewProjectile.Invoke(null, eventArgs);
                                if (eventArgs.Handled)
                                {
                                    return;
                                }
                                lock (player.RecentlyCreatedProjectiles)
                                {
                                    if (!player.RecentlyCreatedProjectiles.Any(p => p.Index == index))
                                    {
                                        player.RecentlyCreatedProjectiles.Add(new GetDataHandlers.ProjectileStruct()
                                        {
                                            Index = index,
                                            Type = projType,
                                            CreatedAt = DateTime.Now
                                        });
                                    }
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
