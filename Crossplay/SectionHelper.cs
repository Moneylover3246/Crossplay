using System;
using System.IO;
using System.IO.Compression;

using Terraria;

namespace Crossplay
{
    public class SectionHelper
    {
        public static byte[] CompressTileSection(MemoryStream decompressionStream, int version)
        {
			int maxTileType = CrossplayPlugin.MaxTiles[version];
			using (BinaryReader reader = new BinaryReader(decompressionStream))
			{
				BinaryWriter writer = new BinaryWriter(decompressionStream);
				int xStart = reader.ReadInt32();
				int yStart = reader.ReadInt32();
				short width = reader.ReadInt16();
				short height = reader.ReadInt16();
				int tileCopies = 0;
				for (int y = yStart; y < yStart + height; y++)
				{
					for (int x = xStart; x < xStart + width; x++)
					{
						if (tileCopies != 0)
						{
							tileCopies--;
						}
						else
						{
							byte header = reader.ReadByte();
							byte header3 = 0;
							if ((header & 1) == 1)
							{
								byte header2 = reader.ReadByte();
								if ((header2 & 1) == 1)
								{
									header3 = reader.ReadByte();
								}
							}
							int newType;
							if ((header & 2) == 2)
							{
								if ((header & 32) == 32)
								{
									byte typeShort = reader.ReadByte();
									byte typeLong = reader.ReadByte();
									newType = (typeLong << 8) | typeShort;
									if (newType > maxTileType)
									{
										if (Main.tileFrameImportant[newType])
										{
											newType = 72;
										}
										else
										{
											newType = 1;
										}
										writer.BaseStream.Position -= 2;
										writer.Write((ushort)newType);
										CrossplayPlugin.Log($"/ SendSection - Processed a tile conversion from {(typeLong << 8) | typeShort} -> {newType}", true, ConsoleColor.Red);
									}
								}
								else
								{
									newType = reader.ReadByte();
								}
								if (Main.tileFrameImportant[newType])
								{
									reader.BaseStream.Position += 4; // FrameX + FrameY
								}
								if ((header3 & 8) == 8)
								{
									reader.BaseStream.Position++; // Tile color (byte)
								}
							}
							if ((header & 4) == 4)
							{
								reader.BaseStream.Position++; // Wall type (byte)
								if ((header3 & 16) == 16)
								{
									reader.BaseStream.Position++; // Wall color (byte)
								}
							}
							byte liquidHeader = (byte)((header & 24) >> 3);
							if (liquidHeader != 0)
							{
								reader.BaseStream.Position++; // Liquid amount (byte)
							}
							if (header3 > 0)
							{
								if ((header3 & 64) == 64)
								{
									reader.BaseStream.Position++; // Long wall
								}
							}
							byte tileCopyHeader = (byte)((header & 192) >> 6);
							switch (tileCopyHeader)
							{
								case 0:
									tileCopies = 0;
									break;
								case 1:
									tileCopies = reader.ReadByte();
									break;
								default:
									tileCopies = reader.ReadInt16();
									break;
							}
						}
					}
				}
				decompressionStream.Position = 0L;
				MemoryStream compressed = new MemoryStream();
				using (DeflateStream deflateStream = new DeflateStream(compressed, CompressionMode.Compress, true))
				{
					decompressionStream.CopyTo(deflateStream);
					deflateStream.Close();
				}
				return new PacketFactory()
					.SetType(10)
					.PackByte(1)
					.PackBuffer(compressed.ToArray())
					.GetByteData();
			}
		}
    }
}
