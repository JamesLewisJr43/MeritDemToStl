using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeritDemToStl
{
    /// <summary>
    /// Data for a cell in the STL surface mesh
    /// </summary>
    public class ElevationCell
    {
        /// <summary>
        /// X location in STL mesh
        /// </summary>
        public float XCoord { get; private set; }

        /// <summary>
        /// Y location in STL mesh
        /// </summary>
        public float YCoord { get; private set; }

        /// <summary>
        /// North bound of region data is for
        /// </summary>
        public double North { get; private set; }

        /// <summary>
        /// South bound of region data is for
        /// </summary>
        public double South { get; private set; }

        /// <summary>
        /// East bound of region data is for
        /// </summary>
        public double East { get; private set; }

        /// <summary>
        /// West bound of region data is for
        /// </summary>
        public double West { get; private set; }

        /// <summary>
        /// Sum of elevation values
        /// </summary>
        public double ElevationSum { get; set; }

        /// <summary>
        /// Number of elevation in ElevationSum
        /// </summary>
        public int ElevationCount { get; set; }

        /// <summary>
        /// Calculate average elevation or filled in elevation
        /// </summary>
        public float? Elevation { get; set; }

        /// <summary>
        /// Create cell data
        /// </summary>
        /// <param name="xCoord">X location in STL mesh</param>
        /// <param name="yCoord">Y location in STL mesh</param>
        /// <param name="north">North bound of region data is for</param>
        /// <param name="south">South bound of region data is for</param>
        /// <param name="east">East bound of region data is for</param>
        /// <param name="west">West bound of region data is for</param>
        public ElevationCell(float xCoord, float yCoord, double north, double south, double east, double west)
        {
            XCoord = xCoord;
            YCoord = yCoord;
            North = north;
            South = south;
            East = east;
            West = west;
            ElevationSum = 0.0;
            ElevationCount = 0;
            Elevation = null;
        }

        /// <summary>
        /// Calculates the average elevation for the cell, no elevation data will use null for the elevation
        /// </summary>
        public void CalculateElevationAverage()
        {
            if (ElevationCount > 0)
            {
                Elevation = (float)(ElevationSum / ElevationCount);
            }
            else
            {
                Elevation = null;
            }
        }
    }
}
