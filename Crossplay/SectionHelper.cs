using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using OTAPI.Tile;
using Terraria;

namespace Crossplay
{
	public class SectionHelper
	{
		public static byte[] WriteDecompressedSection(MemoryStream decompressionStream, int version)
		{
			int maxTileType = Crossplay.MaxTileType[version];
			using (BinaryReader reader = new BinaryReader(decompressionStream))
			{
				BinaryWriter writer = new BinaryWriter(decompressionStream);
				var xStart = reader.ReadInt32();
				var yStart = reader.ReadInt32();
				var width = reader.ReadInt16();
				var height = reader.ReadInt16();
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
									var typeShort = reader.ReadByte();
									var typeLong = reader.ReadByte();
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
										Crossplay.Log($"/ SendSection - Processed a tile conversion from {(typeLong << 8) | typeShort} -> {newType}", true, ConsoleColor.Red);
									}
								}
								else
								{
									newType = reader.ReadByte();
								}
								if (Main.tileFrameImportant[newType])
								{
									reader.ReadInt16(); // FrameX
									reader.ReadInt16(); // FrameY
								}
								if ((header3 & 8) == 8)
								{
									reader.ReadByte(); // Tile color
								}
							}
							if ((header & 4) == 4)
							{
								reader.ReadByte(); // Wall Type
								if ((header3 & 16) == 16)
								{
									reader.ReadByte(); // Wall color
								}
							}
							var liquidHeader = (byte)((header & 24) >> 3);
							if (liquidHeader != 0)
							{
								reader.ReadByte(); // Liquid amount
							}
							if (header3 > 0)
							{
								if ((header3 & 64) == 64)
								{
									reader.ReadByte(); // Long wall
								}
							}
							var tileCopyHeader = (byte)((header & 192) >> 6);
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
