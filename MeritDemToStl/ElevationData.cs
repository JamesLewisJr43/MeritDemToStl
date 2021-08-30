using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeritDemToStl
{
    /// <summary>
    /// DEM data from GeoTiff, or temporary sqlite database
    /// </summary>
    public class ElevationData
    {
        /// <summary>
        /// SQL for creating the table
        /// </summary>
        public static readonly string CREATE_TABLE_SQL = 
            @"CREATE TABLE ElevationData
(
    File varchar(255) not null,
    XCoord int not null,
    YCoord int not null,
    North real not null,
    South real not null,
    East real not null,
    West real not null,
    Elevation real,
    PRIMARY KEY(File ASC, XCoord ASC, YCoord ASC)
);";

        /// <summary>
        /// SQL for inserting records
        /// </summary>
        public static readonly string INSERT_SQL =
            @"INSERT INTO ElevationData (File, Xcoord, YCoord, North, South, East, West, Elevation) 
    VALUES (@File, @XCoord, @YCoord, @North, @South, @East, @West, @Elevation);";

        /// <summary>
        /// DEM file that data comes from
        /// </summary>
        public string File { get; private set; }

        /// <summary>
        /// X location in GeoTiff
        /// </summary>
        public int XCoord { get; private set; }

        /// <summary>
        /// Y location in GeoTiff
        /// </summary>
        public int YCoord { get; private set; }

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
        /// Elevation in meters for the region
        /// </summary>
        public float? Elevation { get; private set; }

        /// <summary>
        /// Create GeoTiff cell data
        /// </summary>
        /// <param name="file">DEM file that data comes from</param>
        /// <param name="xCoord">X location in GeoTiff</param>
        /// <param name="yCoord">Y location in GeoTiff</param>
        /// <param name="north">North bound of region data is for</param>
        /// <param name="south">South bound of region data is for</param>
        /// <param name="east">East bound of region data is for</param>
        /// <param name="west">West bound of region data is for</param>
        /// <param name="elevation">Elevation in meters for the region</param>
        public ElevationData(string file, int xCoord, int yCoord, double north, double south, double east, double west, float? elevation)
        {
            File = file;
            XCoord = xCoord;
            YCoord = yCoord;
            North = north;
            South = south;
            East = east;
            West = west;
            Elevation = elevation;
        }

        /// <summary>
        /// Creates the table using the given sqlite database connection
        /// </summary>
        /// <param name="conn">sqlite database connection</param>
        public static void CreateTable(SQLiteConnection conn)
        {
            using (var command = conn.CreateCommand())
            {
                command.CommandText = CREATE_TABLE_SQL;
                command.CommandType = System.Data.CommandType.Text;
                command.ExecuteNonQuery();
            }
        }

        /// <summary>
        /// Prepares the insert command using the given sqlite database connection
        /// </summary>
        /// <param name="conn">sqlite database connection</param>
        /// <returns>Created insert command</returns>
        public static SQLiteCommand CreateInsertCommand(SQLiteConnection conn)
        {
            var command = conn.CreateCommand();
            command.CommandText = INSERT_SQL;
            command.CommandType = System.Data.CommandType.Text;
            command.Parameters.Add(new SQLiteParameter("@File", System.Data.DbType.AnsiString, 255));
            command.Parameters.Add(new SQLiteParameter("@XCoord", System.Data.DbType.Int32));
            command.Parameters.Add(new SQLiteParameter("@YCoord", System.Data.DbType.Int32));
            command.Parameters.Add(new SQLiteParameter("@North", System.Data.DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@South", System.Data.DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@East", System.Data.DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@West", System.Data.DbType.Double));
            command.Parameters.Add(new SQLiteParameter("@Elevation", System.Data.DbType.Single));
            return command;
        }

        /// <summary>
        /// Adds this record to the given command created from CreateInsertCommand
        /// </summary>
        /// <param name="command">Insert command</param>
        public void AddToInsertBatch(SQLiteCommand command)
        {
            command.Parameters["@File"].Value = File;
            command.Parameters["@XCoord"].Value = XCoord;
            command.Parameters["@YCoord"].Value = YCoord;
            command.Parameters["@North"].Value = North;
            command.Parameters["@South"].Value = South;
            command.Parameters["@East"].Value = East;
            command.Parameters["@West"].Value = West;
            command.Parameters["@Elevation"].Value = Elevation.HasValue ? (object)Elevation.Value : DBNull.Value;
        }
    }
}
