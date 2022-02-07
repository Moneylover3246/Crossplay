using System.IO;
using OTAPI.Tile;
using Terraria;

namespace Crossplay
{
    public class TileProcessor
    {
        public static void ProcessTile(ITile tile, BinaryReader reader)
        {
            BitsByte flags = reader.ReadByte();
            BitsByte flags2 = reader.ReadByte();
            bool oldActive = tile.active();

            tile.wire(flags[4]);
            tile.wire2(flags2[0]);
            tile.wire3(flags2[1]);
            tile.wire4(flags2[7]);

            tile.halfBrick(flags[5]);
            tile.actuator(flags[6]);
            tile.inActive(flags[7]);
            tile.slope((byte)((flags2 & 112) >> 4));

            if (flags2[2])
            {
                tile.color(reader.ReadByte());
            }
            if (flags2[3])
            {
                tile.wallColor(reader.ReadByte());
            }
            if (flags[0])
            {
                ushort oldType = tile.type;
                tile.active(true);
                tile.type = reader.ReadUInt16();
                if (Main.tileFrameImportant[tile.type])
                {
                    tile.frameX = reader.ReadInt16();
                    tile.frameY = reader.ReadInt16();
                }
                else if (!oldActive && tile.type != oldType)
                {
                    tile.frameX = -1;
                    tile.frameY = -1;
                }
            }
            if (flags[2])
            {
                tile.wall = reader.ReadUInt16();
            }
            if (flags[3])
            {
                tile.liquid = reader.ReadByte();
                tile.liquidType(reader.ReadByte());
            }
        }
    }
}
