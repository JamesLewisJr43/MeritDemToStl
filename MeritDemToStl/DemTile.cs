using MapControl;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeritDemToStl
{
    /// <summary>
    /// Details for a DEM tile
    /// </summary>
    public class DemTile
    {
        /// <summary>
        /// Bottom of first tile 
        /// </summary>
        public static readonly int STARTING_TILE_BOTTOM = 85;

        /// <summary>
        /// Right of the first tile
        /// </summary>
        public static readonly int STARTING_TILE_RIGHT = -180;

        /// <summary>
        /// Width and height of tiles in degrees
        /// </summary>
        public static readonly int TILE_SIZE = 5;

        /// <summary>
        /// Number of pixels wide or tall for the tile
        /// </summary>
        public static readonly int TILE_PIXELS = 6000;

        /// <summary>
        /// Size of a tile pixel in degrees
        /// </summary>
        public static readonly double TILE_PIXEL_SIZE = ((double)TILE_SIZE) / TILE_PIXELS;

        /// <summary>
        /// All tiles for the entire world
        /// </summary>
        public static readonly DemTile[,] TILES;

        /// <summary>
        /// Number of tiles vertically that cover the world
        /// </summary>
        public static readonly int Y_TILES = 180 / TILE_SIZE;

        /// <summary>
        /// Number of tiles horizontally that cover the world
        /// </summary>
        public static readonly int X_TILES = 360 / TILE_SIZE;

        /// <summary>
        /// Initialize TILES set
        /// </summary>
        static DemTile()
        {
            TILES = new DemTile[Y_TILES, X_TILES];
            for (int y = 0; y < Y_TILES; y++)
            {
                int tileY = -90 + (y * TILE_SIZE);
                for (int x = 0; x < X_TILES; x++)
                {
                    int tileX = -180 + (x * TILE_SIZE);
                    TILES[y, x] = new DemTile(tileY, tileX);
                }
            }
        }

        /// <summary>
        /// Name of the file
        /// </summary>
        public string FileName { get; private set; }

        /// <summary>
        /// Full path to the file
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Bounds for the tile
        /// </summary>
        public BoundingBox Bounds { get; private set; }

        /// <summary>
        /// If the tile exists
        /// </summary>
        public bool Exists { get; private set; }

        private DemTile(int tileBottom, int tileLeft)
        {
            FileName = string.Format("{0}{1:D2}{2}{3:D3}_dem.tif", (tileBottom < 0) ? "s" : "n", Math.Abs(tileBottom), (tileLeft < 0) ? "w" : "e", Math.Abs(tileLeft));
            // Tile name is center of the southwest pixel
            double south = tileBottom - TILE_PIXEL_SIZE / 2;
            double west = tileLeft + TILE_PIXEL_SIZE / 2;
            double north = south + TILE_SIZE;
            double east = west + TILE_SIZE;
            Bounds = new BoundingBox(south, west, north, east);
            try
            {
                FilePath = Path.Combine(Properties.Settings.Default.DemDirectory, FileName);
                Exists = File.Exists(FilePath);
            }
            catch
            {
                FilePath = null;
                Exists = false;
            }
        }
    }
}
