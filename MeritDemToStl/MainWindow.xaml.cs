using MapControl;
using OSGeo.GDAL;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace MeritDemToStl
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        /// <summary>
        /// Bounds for the STL generation
        /// </summary>
        private MapPolygon _currentBounds;

        /// <summary>
        /// Settings for creating the STL
        /// </summary>
        private ExtractSettings _extractSettings;

        /// <summary>
        /// Path to opened or saved settings file
        /// </summary>
        private string _settingsPath;

        public MainWindow()
        {
            _extractSettings = new ExtractSettings();
            InitializeComponent();
        }

        private void SaveSettings(string path)
        {
            if (_currentBounds == null)
            {
                return;
            }

            if (path == null)
            {
                // Use most recently opened or saved file
                path = _settingsPath;
            }
            if (path == null)
            {
                // No path given or most recent file, ask the user
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set local environment variables needed by GDAL
            string path = Environment.GetEnvironmentVariable("PATH");
            path = string.Format(
                @"{0}bin;{0}bin\gdal\python\osgeo;{0}bin\proj6\apps;{0}bin\gdal\apps;{0}bin\ms\apps;{0}bin\gdal\csharp;{0}bin\ms\csharp;{0}bin\curl;{1}",
                Properties.Settings.Default.GdalDir, path);
            Environment.SetEnvironmentVariable("PATH", path);
            Environment.SetEnvironmentVariable("GDAL_DATA", string.Format(@"{0}bin\gdal-data", Properties.Settings.Default.GdalDir));
            Environment.SetEnvironmentVariable("GDAL_DRIVER_PATH", string.Format(@"{0}bin\gdal\plugins", Properties.Settings.Default.GdalDir));
            Environment.SetEnvironmentVariable("PYTHONPATH", string.Format(@"{0}bin\gdal\python;{0}bin\ms\python", Properties.Settings.Default.GdalDir));
            Environment.SetEnvironmentVariable("PROJ_LIB", string.Format(@"{0}bin\proj7\SHARE", Properties.Settings.Default.GdalDir));
            // Initialize GDAL
            Gdal.AllRegister();

            map.Center = new Location(34.00525271981912, -84.0356810349676);
            map.ZoomLevel = 10;
            DataContext = _extractSettings;

            // Add tile bounds map items
            foreach (var tile in DemTile.TILES)
            {
                if (!tile.Exists)
                    continue;

                MapPolygon mapItem = new MapPolygon();
                mapItem.Stroke = Brushes.DarkGray;
                mapItem.StrokeThickness = 1;

                mapItem.Locations = new Location[]
                {
                    new Location(tile.Bounds.North, tile.Bounds.West),
                    new Location(tile.Bounds.North, tile.Bounds.East),
                    new Location(tile.Bounds.South, tile.Bounds.East),
                    new Location(tile.Bounds.South, tile.Bounds.West)
                };

                map.Children.Add(mapItem);
            }
        }

        private void Close_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            Close();
        }

        private void SetBounds_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBounds != null)
            {
                map.Children.Remove(_currentBounds);
                _currentBounds = null;
            }

            _currentBounds = new MapPolygon();
            _currentBounds.Stroke = Brushes.Black;
            _currentBounds.StrokeThickness = 2;

            double testWidth = map.ActualHeight * _extractSettings.Width / _extractSettings.Height;
            double testHeight = map.ActualWidth * _extractSettings.Height / _extractSettings.Width;
            Rect usedRect;
            if (testHeight <= map.ActualHeight)
            {
                // Use screen width, scale height
                usedRect = new Rect(0, (map.ActualHeight - testHeight) / 2, map.ActualWidth, testHeight);
            }
            else
            {
                // Use screen height, scale width
                usedRect = new Rect((map.ActualWidth - testWidth) / 2, 0, testWidth, map.ActualHeight);
            }
            var bounds = map.ViewRectToBoundingBox(usedRect);
            _currentBounds.Locations = new Location[]
            {
                new Location(bounds.North, bounds.West),
                new Location(bounds.North, bounds.East),
                new Location(bounds.South, bounds.East),
                new Location(bounds.South, bounds.West)
            };

            map.Children.Add(_currentBounds);

            _extractSettings.Bounds = bounds;
        }

        private void New_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            _extractSettings = new ExtractSettings();
            DataContext = _extractSettings;
        }

        private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            // TODO
        }

        private void Save_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveSettings(_settingsPath);
        }

        private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e)
        {
            SaveSettings(null);
        }

        /// <summary>
        /// Loads the given GeoTiff into the working database using the given insert command
        /// </summary>
        /// <param name="tile">GeoTiff to load</param>
        /// <param name="conn">Connection to the working database</param>
        /// <param name="demInsertCmd">Insert command to use</param>
        private void LoadGeoTiff(DemTile tile, SQLiteConnection conn, SQLiteCommand demInsertCmd)
        {
            using (Dataset ds = Gdal.Open(tile.FilePath, Access.GA_ReadOnly))
            {
                if (ds == null)
                {
                    throw new Exception("Unable to open " + tile.FilePath);
                }

                Driver drv = ds.GetDriver();
                if (drv == null)
                {
                    throw new Exception("Can't get driver for " + tile.FilePath);
                }

                if (ds.RasterCount == 0)
                {
                    // No data
                    return;
                }

                // Assume one band with float32 data, matches MERIT DEM files
                using (Band band = ds.GetRasterBand(1))
                { 
                    band.GetNoDataValue(out double noDataValue, out int hasNoDataValue);
                    float? noDataTestValue = null;
                    if (hasNoDataValue != 0)
                    {
                        noDataTestValue = (float)noDataValue;
                    }
                    using (var transaction = conn.BeginTransaction())
                    {
                        try
                        {
                            for (int y = 0; y < DemTile.TILE_PIXELS; ++y)
                            {
                                double north = tile.Bounds.North - (y * DemTile.TILE_PIXEL_SIZE);
                                double south = north - DemTile.TILE_PIXEL_SIZE;

                                float[] elevations = new float[DemTile.TILE_PIXELS];
                                band.ReadRaster(0, y, DemTile.TILE_PIXELS, 1, elevations, DemTile.TILE_PIXELS, 1, 0, 0);
                                for (int x = 0; x < DemTile.TILE_PIXELS; ++x)
                                {
                                    double west = tile.Bounds.West + (x * DemTile.TILE_PIXEL_SIZE);
                                    double east = west + DemTile.TILE_PIXEL_SIZE;
                                    float? elevation = null;
                                    if (noDataTestValue.HasValue && (elevations[x] != noDataTestValue.Value))
                                    {
                                        elevation = elevations[x];
                                    }
                                    ElevationData data = new ElevationData(tile.FilePath, x, y, north, south, east, west, elevation);
                                    data.AddToInsertBatch(demInsertCmd);
                                    demInsertCmd.ExecuteNonQuery();
                                }
                            }
                            transaction.Commit();
                        }
                        catch
                        {
                            transaction.Rollback();
                            throw;
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts the elevation data from the given GeoTiff
        /// </summary>
        /// <param name="tile">GeoTiff to extrac data from</param>
        /// <param name="readBounds">Area to read DEM data from</param>
        /// <param name="elevationCells">Elevation cells for STL mesh</param>
        /// <param name="cellRows">Rows in the elevation cell matrix</param>
        /// <param name="cellColumns">Columns in the elevation cell matrix</param>
        /// <param name="cellHeight">Size of the STL mesh cell vertically</param>
        /// <param name="cellWidth">Size of the STL mesh cell horizontally</param>
        /// <param name="minElevation">Minimum elevation that has been extracted</param>
        /// <param name="maxElevation">Maximum elevation that has been extracted</param>
        private void ExtractElevationFromGeoTiff(DemTile tile, BoundingBox readBounds, ElevationCell[,] elevationCells, int cellRows, int cellColumns, double cellHeight, double cellWidth, ref float? minElevation, ref float? maxElevation)
        {
            using (Dataset ds = Gdal.Open(tile.FilePath, Access.GA_ReadOnly))
            {
                if (ds == null)
                {
                    throw new Exception("Unable to open " + tile.FilePath);
                }

                Driver drv = ds.GetDriver();
                if (drv == null)
                {
                    throw new Exception("Can't get driver for " + tile.FilePath);
                }

                if (ds.RasterCount == 0)
                {
                    // No data
                    return;
                }

                // Assume one band with float32 data, matches MERIT DEM files
                using (Band band = ds.GetRasterBand(1))
                {
                    band.GetNoDataValue(out double noDataValue, out int hasNoDataValue);
                    float? noDataTestValue = null;
                    if (hasNoDataValue != 0)
                    {
                        noDataTestValue = (float)noDataValue;
                    }

                    for (int y = 0; y < DemTile.TILE_PIXELS; ++y)
                    {
                        double north = tile.Bounds.North - (y * DemTile.TILE_PIXEL_SIZE);
                        double south = north - DemTile.TILE_PIXEL_SIZE;

                        if ((south > readBounds.North) || (north < readBounds.South))
                        {
                            // Row in GeoTiff outside the extract bounds
                            continue;
                        }

                        float[] elevations = new float[DemTile.TILE_PIXELS];
                        band.ReadRaster(0, y, DemTile.TILE_PIXELS, 1, elevations, DemTile.TILE_PIXELS, 1, 0, 0);
                        for (int x = 0; x < DemTile.TILE_PIXELS; ++x)
                        {
                            double west = tile.Bounds.West + (x * DemTile.TILE_PIXEL_SIZE);
                            double east = west + DemTile.TILE_PIXEL_SIZE;

                            if ((west > readBounds.East) || (east < readBounds.West))
                            {
                                // Column in GeoTiff outside the extract bounds
                                continue;
                            }

                            float? elevation = null;
                            if (noDataTestValue.HasValue && (elevations[x] != noDataTestValue.Value))
                            {
                                elevation = elevations[x];
                            }

                            if (elevation.HasValue)
                            {
                                // Update elevation range
                                if (!minElevation.HasValue || (elevation.Value < minElevation.Value))
                                {
                                    minElevation = elevation;
                                }
                                if (!maxElevation.HasValue || (elevation.Value > maxElevation.Value))
                                {
                                    maxElevation = elevation;
                                }

                                // Update overlapping cells
                                int startRow = (int)Math.Floor((south - _extractSettings.Bounds.South) / cellHeight);
                                if (startRow < 0) startRow = 0;
                                int endRow = (int)Math.Ceiling((north - _extractSettings.Bounds.South) / cellHeight);
                                int startColumn = (int)Math.Floor((west - _extractSettings.Bounds.West) / cellWidth);
                                if (startColumn < 0) startColumn = 0;
                                int endColumn = (int)Math.Ceiling((east - _extractSettings.Bounds.West) / cellWidth);
                                for (int row = startRow; (row <= endRow) && (row < cellRows); ++row)
                                {
                                    for (int column = startColumn; (column <= endColumn) && (column < cellColumns); ++column)
                                    {
                                        var cell = elevationCells[row, column];
                                        cell.ElevationSum += elevation.Value;
                                        cell.ElevationCount++;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void Generate_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBounds == null)
            {
                return;
            }

            // Extra row and column is to have the outside edge at the top and right
            int cellRows = (int)Math.Ceiling(_extractSettings.Height / _extractSettings.Resolution) + 1;
            int cellColumns = (int)Math.Ceiling(_extractSettings.Width / _extractSettings.Resolution) + 1;
            ElevationCell[,] elevationCells = new ElevationCell[cellRows, cellColumns];
            double cellHeight = _extractSettings.Bounds.Height / cellRows;
            double cellWidth = _extractSettings.Bounds.Width / cellColumns;
            // Need extra buffer space for reading in elevation data for the region around mesh points at edge
            BoundingBox readBounds = new BoundingBox(_extractSettings.Bounds.South - cellHeight, _extractSettings.Bounds.West - cellWidth,
                _extractSettings.Bounds.North + cellHeight, _extractSettings.Bounds.East + cellWidth);

            for (int row = 0; row < cellRows; ++row)
            {
                double south = _extractSettings.Bounds.South + (row * cellHeight);
                double north = south + cellHeight;
                float yCoord = row * (float)_extractSettings.Resolution;
                for (int column = 0; column < cellColumns; ++column)
                {
                    double west = _extractSettings.Bounds.West + (column * cellWidth);
                    double east = west + cellWidth;
                    float xCoord = column * (float)_extractSettings.Resolution;
                    elevationCells[row, column] = new ElevationCell(xCoord, yCoord, north, south, east, west);
                }
            }

            float? minElevation = null;
            float? maxElevation = null;
            for (int y = 0; y < DemTile.Y_TILES; y++)
            {
                for (int x = 0; x < DemTile.X_TILES; x++)
                {
                    DemTile tile = DemTile.TILES[y, x];
                    if (!tile.Exists || (tile.Bounds.North < readBounds.South) || (tile.Bounds.South > readBounds.North)
                        || (tile.Bounds.East < readBounds.West) || (tile.Bounds.West > readBounds.East))
                    {
                        continue;
                    }

                    try
                    {
                        ExtractElevationFromGeoTiff(tile, readBounds, elevationCells, cellRows, cellColumns, cellHeight, cellWidth, ref minElevation, ref maxElevation);
                    }
                    catch (Exception ex)
                    {
                        // Problem processing the GeoTIFF
                        MessageBox.Show("Problem processing GeoTIFF:\n\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }

            // Calculate the average elevations, only once
            foreach (var cell in elevationCells)
            {
                cell.CalculateElevationAverage();
            }
        }

        private void Goto_Click(object sender, RoutedEventArgs e)
        {
            double latitude = double.Parse(LatitudeTextBox.Text);
            double longitude = double.Parse(LongitudeTextBox.Text);
            double zoomLevel = double.Parse(ZoomLevelTextBox.Text);

            map.Center = new Location(latitude, longitude);
            map.ZoomLevel = zoomLevel;
        }

        private void LoadDatabase_Click(object sender, RoutedEventArgs e)
        {
            if (_currentBounds == null)
            {
                return;
            }

            if (File.Exists(Properties.Settings.Default.WorkingDatabaseFile))
            {
                // Remove previous working database;
                File.Delete(Properties.Settings.Default.WorkingDatabaseFile);
            }

            SQLiteConnection.CreateFile(Properties.Settings.Default.WorkingDatabaseFile);
            using (var conn = new SQLiteConnection(string.Format("Data Source={0};Version=3;", Properties.Settings.Default.WorkingDatabaseFile)))
            {
                conn.Open();

                ElevationData.CreateTable(conn);

                int cellRows = (int)Math.Ceiling(_extractSettings.Height / _extractSettings.Resolution);
                int cellColumns = (int)Math.Ceiling(_extractSettings.Width / _extractSettings.Resolution);
                double cellHeight = _extractSettings.Bounds.Height / cellRows;
                double cellWidth = _extractSettings.Bounds.Width / cellColumns;

                using (var demInsertCmd = ElevationData.CreateInsertCommand(conn))
                {
                    for (int y = 0; y < DemTile.Y_TILES; y++)
                    {
                        for (int x = 0; x < DemTile.X_TILES; x++)
                        {
                            DemTile tile = DemTile.TILES[y, x];
                            if (!tile.Exists || (tile.Bounds.North < _extractSettings.Bounds.South) || (tile.Bounds.South > _extractSettings.Bounds.North)
                                || (tile.Bounds.East < _extractSettings.Bounds.West) || (tile.Bounds.West > _extractSettings.Bounds.East))
                            {
                                continue;
                            }

                            try
                            {
                                LoadGeoTiff(tile, conn, demInsertCmd);
                            }
                            catch (Exception ex)
                            {
                                // Problem processing the GeoTIFF
                                MessageBox.Show("Problem processing GeoTIFF:\n\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                            }
                        }
                    }
                }
            }
        }
    }
}
