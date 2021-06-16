using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OTAPI.Tile;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.Tile_Entities;
using Terraria.ID;
using Terraria.ObjectData;
using TShockAPI;
using TShockAPI.Net;

namespace Crossplay
{
	/// <summary>
	/// This class was taken from TShockAPI.Handlers.SendTileRectHandler, we need to recreate this class since some of the methods are internal
	/// </summary>
    class SendTileRectHandler
    {
		public static List<int> FlowerBootItems = new List<int>
		{
			ItemID.FlowerBoots,
			ItemID.FairyBoots
		};

		public static Dictionary<ushort, List<ushort>> GrassToPlantMap = new Dictionary<ushort, List<ushort>>
		{
			{ TileID.Grass, new List<ushort>            { TileID.Plants,         TileID.Plants2 } },
			{ TileID.HallowedGrass, new List<ushort>    { TileID.HallowedPlants, TileID.HallowedPlants2 } },
			{ TileID.JungleGrass, new List<ushort>      { TileID.JunglePlants,   TileID.JunglePlants2 } }
		};

		public static Dictionary<int, int> TileEntityIdToTileIdMap = new Dictionary<int, int>
		{
			{ TileID.TargetDummy,        TETrainingDummy._myEntityID },
			{ TileID.ItemFrame,          TEItemFrame._myEntityID },
			{ TileID.LogicSensor,        TELogicSensor._myEntityID },
			{ TileID.DisplayDoll,        TEDisplayDoll._myEntityID },
			{ TileID.WeaponsRack2,       TEWeaponsRack._myEntityID },
			{ TileID.HatRack,            TEHatRack._myEntityID },
			{ TileID.FoodPlatter,        TEFoodPlatter._myEntityID },
			{ TileID.TeleportationPylon, TETeleportationPylon._myEntityID }
		};
		internal void IterateTileRect(NetTile[,] tiles, bool[,] processed, GetDataHandlers.SendTileRectEventArgs args)
		{
			int tileX = args.TileX;
			int tileY = args.TileY;
			byte width = args.Width;
			byte length = args.Length;

			for (int x = 0; x < width; x++)
			{
				for (int y = 0; y < length; y++)
				{
					if (processed[x, y])
					{
						continue;
					}

					int realX = tileX + x;
					int realY = tileY + y;

					if ((realX < 0 || realX >= Main.maxTilesX)
						|| (realY < 0 || realY > Main.maxTilesY))
					{
						processed[x, y] = true;
						continue;
					}

					if (!args.Player.HasBuildPermission(realX, realY) ||
						!args.Player.IsInRange(realX, realY, 60))
					{
						processed[x, y] = true;
						continue;
					}

					NetTile newTile = tiles[x, y];
					TileObjectData data;

					if (newTile.Type < TileObjectData._data.Count && TileObjectData._data[newTile.Type] != null)
					{
						data = TileObjectData._data[newTile.Type];
						NetTile[,] newTiles;
						int objWidth = data.Width;
						int objHeight = data.Height;
						int offsetY = 0;

						if (newTile.Type == TileID.TrapdoorClosed)
						{
							objWidth = 2;
							objHeight = 3;
							offsetY = -1;
						}

						if (!DoesTileObjectFitInTileRect(x, y, objWidth, objHeight, width, length, offsetY, processed))
						{
							continue;
						}

						newTiles = new NetTile[objWidth, objHeight];

						for (int i = 0; i < objWidth; i++)
						{
							for (int j = 0; j < objHeight; j++)
							{
								newTiles[i, j] = tiles[x + i, y + j + offsetY];
								processed[x + i, y + j + offsetY] = true;
							}
						}
						ProcessTileObject(newTile.Type, realX, realY + offsetY, objWidth, objHeight, newTiles, args);
						continue;
					}
					ProcessSingleTile(realX, realY, newTile, width, length, args);
					processed[x, y] = true;
				}
			}
			TSPlayer.All.SendTileRect((short)tileX, (short)tileY, length, width, args.ChangeType);
		}

		static bool DoesTileObjectFitInTileRect(int x, int y, int width, int height, short rectWidth, short rectLength, int offsetY, bool[,] processed)
		{
			if (y + offsetY < 0)
			{
				return false;
			}

			if (x + width > rectWidth || y + height + offsetY > rectLength)
			{		
				for (int i = x; i < rectWidth; i++)
				{
					for (int j = Math.Max(0, y + offsetY); j < rectLength; j++)
					{
						processed[i, j] = true;
					}
				}

				return false;
			}

			return true;
		}

		internal void ProcessTileObject(int tileType, int realX, int realY, int width, int height, NetTile[,] newTiles, GetDataHandlers.SendTileRectEventArgs args)
		{
			if (!args.Player.HasBuildPermissionForTileObject(realX, realY, width, height))
			{
				return;
			}

			if (TShock.TileBans.TileIsBanned((short)tileType))
			{
				return;
			}

			UpdateMultipleServerTileStates(realX, realY, width, height, newTiles);
			if (TileEntityIdToTileIdMap.ContainsKey(tileType))
			{
				TileEntity.PlaceEntityNet(realX, realY, TileEntityIdToTileIdMap[tileType]);
			}
		}

		internal void ProcessSingleTile(int realX, int realY, NetTile newTile, byte rectWidth, byte rectLength, GetDataHandlers.SendTileRectEventArgs args)
		{
			if (rectWidth == 1 && rectLength == 1 && args.Player.Accessories.Any(a => a != null && FlowerBootItems.Contains(a.type)))
			{
				ProcessFlowerBoots(realX, realY, newTile, args);
				return;
			}

			ITile tile = Main.tile[realX, realY];

			if (tile.type == TileID.LandMine && !newTile.Active)
			{
				UpdateServerTileState(tile, newTile);
			}

			if (tile.type == TileID.WirePipe)
			{
				UpdateServerTileState(tile, newTile);
			}

			ProcessConversionSpreads(Main.tile[realX, realY], newTile);
		}

		public static void UpdateServerTileState(ITile tile, NetTile newTile)
		{
			tile.active(newTile.Active);
			tile.type = newTile.Type;

			if (newTile.FrameImportant)
			{
				tile.frameX = newTile.FrameX;
				tile.frameY = newTile.FrameY;
			}

			if (newTile.HasWall)
			{
				tile.wall = newTile.Wall;
			}

			if (newTile.HasLiquid)
			{
				tile.liquid = newTile.Liquid;
				tile.liquidType(newTile.LiquidType);
			}

			tile.wire(newTile.Wire);
			tile.wire2(newTile.Wire2);
			tile.wire3(newTile.Wire3);
			tile.wire4(newTile.Wire4);

			tile.halfBrick(newTile.IsHalf);

			if (newTile.HasColor)
			{
				tile.color(newTile.TileColor);
			}

			if (newTile.HasWallColor)
			{
				tile.wallColor(newTile.WallColor);
			}

			tile.slope(newTile.Slope);

		}
		public static void UpdateMultipleServerTileStates(int x, int y, int width, int height, NetTile[,] newTiles)
		{
			for (int i = 0; i < width; i++)
			{
				for (int j = 0; j < height; j++)
				{
					UpdateServerTileState(Main.tile[x + i, y + j], newTiles[i, j]);
				}
			}
		}

		internal void ProcessConversionSpreads(ITile tile, NetTile newTile)
		{
			if (
				((TileID.Sets.Conversion.Stone[tile.type] || Main.tileMoss[tile.type]) && (TileID.Sets.Conversion.Stone[newTile.Type] || Main.tileMoss[newTile.Type])) ||
				((tile.type == 0 || tile.type == 59) && (newTile.Type == 0 || newTile.Type == 59)) ||
				TileID.Sets.Conversion.Grass[tile.type] && TileID.Sets.Conversion.Grass[newTile.Type] ||
				TileID.Sets.Conversion.Ice[tile.type] && TileID.Sets.Conversion.Ice[newTile.Type] ||
				TileID.Sets.Conversion.Sand[tile.type] && TileID.Sets.Conversion.Sand[newTile.Type] ||
				TileID.Sets.Conversion.Sandstone[tile.type] && TileID.Sets.Conversion.Sandstone[newTile.Type] ||
				TileID.Sets.Conversion.HardenedSand[tile.type] && TileID.Sets.Conversion.HardenedSand[newTile.Type] ||
				TileID.Sets.Conversion.Thorn[tile.type] && TileID.Sets.Conversion.Thorn[newTile.Type] ||
				TileID.Sets.Conversion.Moss[tile.type] && TileID.Sets.Conversion.Moss[newTile.Type] ||
				TileID.Sets.Conversion.MossBrick[tile.type] && TileID.Sets.Conversion.MossBrick[newTile.Type] ||
				WallID.Sets.Conversion.Stone[tile.wall] && WallID.Sets.Conversion.Stone[newTile.Wall] ||
				WallID.Sets.Conversion.Grass[tile.wall] && WallID.Sets.Conversion.Grass[newTile.Wall] ||
				WallID.Sets.Conversion.Sandstone[tile.wall] && WallID.Sets.Conversion.Sandstone[newTile.Wall] ||
				WallID.Sets.Conversion.HardenedSand[tile.wall] && WallID.Sets.Conversion.HardenedSand[newTile.Wall] ||
				WallID.Sets.Conversion.PureSand[tile.wall] && WallID.Sets.Conversion.PureSand[newTile.Wall] ||
				WallID.Sets.Conversion.NewWall1[tile.wall] && WallID.Sets.Conversion.NewWall1[newTile.Wall] ||
				WallID.Sets.Conversion.NewWall2[tile.wall] && WallID.Sets.Conversion.NewWall2[newTile.Wall] ||
				WallID.Sets.Conversion.NewWall3[tile.wall] && WallID.Sets.Conversion.NewWall3[newTile.Wall] ||
				WallID.Sets.Conversion.NewWall4[tile.wall] && WallID.Sets.Conversion.NewWall4[newTile.Wall]
			)
			{
				UpdateServerTileState(tile, newTile);
			}
		}
		internal void ProcessFlowerBoots(int realX, int realY, NetTile newTile, GetDataHandlers.SendTileRectEventArgs args)
		{
			if (!WorldGen.InWorld(realX, realY + 1))
			{
				return;
			}

			ITile tile = Main.tile[realX, realY + 1];
			if (!GrassToPlantMap.TryGetValue(tile.type, out List<ushort> plantTiles) && !plantTiles.Contains(newTile.Type))
			{
				return;
			}

			UpdateServerTileState(Main.tile[realX, realY], newTile);
		}

		internal static bool ShouldSkipProcessing(GetDataHandlers.SendTileRectEventArgs args)
		{
			var rectSize = args.Width * args.Length;
			if (rectSize > TShock.Config.Settings.TileRectangleSizeThreshold)
			{
				if (TShock.Config.Settings.KickOnTileRectangleSizeThresholdBroken)
				{
					args.Player.Kick("Unexpected tile threshold reached");
				}
				return true;
			}

			if (args.Player.IsBouncerThrottled())
			{
				args.Player.SendTileRect(args.TileX, args.TileY, args.Length, args.Width);
				return true;
			}

			if (args.Player.IsBeingDisabled())
			{
				args.Player.SendTileRect(args.TileX, args.TileY, args.Length, args.Width);
				return true;
			}

			return false;
		}
	}
}
