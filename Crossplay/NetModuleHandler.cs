using System.Runtime.CompilerServices;
using Terraria;
using Terraria.Net;

namespace Crossplay
{
    internal class NetModuleHandler
    {
        internal static void OnBroadcast(On.Terraria.Net.NetManager.orig_Broadcast_NetPacket_int orig, NetManager self, NetPacket packet, int ignoreClient)
        {
            for (int i = 0; i <= Main.maxPlayers; i++)
            {
                if (i != ignoreClient && Netplay.Clients[i].IsConnected() && !InvalidNetPacket(packet, i))
                {
                    self.SendData(Netplay.Clients[i].Socket, packet);
                }
            }
        }

        internal static void OnSendToClient(On.Terraria.Net.NetManager.orig_SendToClient orig, NetManager self, NetPacket packet, int playerId)
        {
            if (!InvalidNetPacket(packet, playerId))
            {
                orig(self, packet, playerId);
            }
        }

        private static bool InvalidNetPacket(NetPacket packet, int playerId)
        {
            switch (packet.Id)
            {
                case 5:
                    {
                        var itemNetID = Unsafe.As<byte, short>(ref packet.Buffer.Data[3]); // https://unsafe.as/
                        
                        if (itemNetID > CrossplayPlugin.Instance.MaxItems[CrossplayPlugin.Instance.ClientVersions[playerId]])
                        {
                            return true;
                        }
                    }
                    break;
            }
            return false;
        }
    }
}
