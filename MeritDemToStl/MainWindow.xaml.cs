using MapControl;
using Microsoft.Win32;
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

            SharpKml.Engine.KmlFile file;
            try
            {
                using (FileStream stream = File.Open(Properties.Settings.Default.LandKml, FileMode.Open))
                {
                    file = SharpKml.Engine.KmlFile.Load(stream);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.GetType() + "\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                file = null;
            }

            List<LandPolygon> land = new List<LandPolygon>();
            if ((file != null) && (file.Root != null) && file.Root is SharpKml.Dom.Kml kml)
            {
                ExtractPlacemarks(kml.Feature, land);
            }

            // Fill in unknown areas between known areas
            FillUnknownElevation(elevationCells, cellRows, cellColumns, land);

            // Calculate filled in elevations, or if base height should be used
            for (int row = 0; row < cellRows; ++row)
            {
                for (int col = 0; col < cellColumns; ++col)
                {
                    ElevationCell cell = elevationCells[row, col];
                    // Only need to calculate fill in elevation if we don't already know what to do
                    if (!cell.Elevation.HasValue && !cell.UseBaseHeight)
                    {
                        // Need to determine fill in value
                        if (cell.VerticalScanElevation.HasValue && cell.HorizontalScanElevation.HasValue)
                        {
                            // Scan found fill in elevation in both directions, use average found elevation
                            cell.Elevation = (cell.VerticalScanElevation.Value + cell.HorizontalScanElevation.Value) / 2;
                        }
                        else if (cell.VerticalScanElevation.HasValue)
                        {
                            // Scan only found fill in elevation vertically, use found elevation
                            cell.Elevation = cell.VerticalScanElevation;
                        }
                        else if (cell.HorizontalScanElevation.HasValue)
                        {
                            // Scan only found fill in elevation horizontally, use found elevation
                            cell.Elevation = cell.HorizontalScanElevation;
                        }
                        else
                        {
                            // No fill in, use base height
                            cell.UseBaseHeight = true;
                        }
                    }
                }
            }

            SaveStl(elevationCells, cellRows, cellColumns, minElevation, maxElevation);
        }

        /// <summary>
        /// Fills in the elevation data when unknown using the surounding known data
        /// </summary>
        /// <param name="elevationCells">Elevation mesh filled with DEM data</param>
        /// <param name="cellRows">Number of elevation mesh rows</param>
        /// <param name="cellColumns">Number of elevation mesh columns</param>
        /// <param name="land">Loaded Natural Earth land polygons</param>
        private void FillUnknownElevation(ElevationCell[,] elevationCells, int cellRows, int cellColumns, List<LandPolygon> land)
        {
            // Scan horizontally
            for (int scanRow = 0; scanRow < cellRows; ++scanRow)
            {
                ElevationCell[] row = new ElevationCell[cellColumns];
                for (int col = 0; col < cellColumns; ++col)
                {
                    row[col] = elevationCells[scanRow, col];
                }
                ScanUnknownElevation(false, row, land);
            }
            // Scan vertically
            for (int scanCol = 0; scanCol < cellColumns; ++ scanCol)
            {
                ElevationCell[] col = new ElevationCell[cellRows];
                for (int row = 0; row < cellRows; ++row)
                {
                    col[row] = elevationCells[row, scanCol];
                }
                ScanUnknownElevation(true, col, land);
            }
        }

        /// <summary>
        /// Fills in the elevation data when unknown using the surounding known data, for the given row or column
        /// </summary>
        /// <param name="vertical">If setting vertical scan value</param>
        /// <param name="elevationCells">Elevation mesh filled with DEM data, for column or row</param>
        /// <param name="land">Loaded Natural Earth land polygons</param>
        private void ScanUnknownElevation(bool vertical, ElevationCell[] elevationCells, List<LandPolygon> land)
        {
            int startUnknown = -1;
            for (int index = 0; index < elevationCells.Length; ++index)
            {
                ElevationCell cell = elevationCells[index];
                if (cell.Elevation.HasValue)
                {
                    if (startUnknown >= 0)
                    {
                        // At end of an unknown range
                        FillUnknownElevation(vertical, startUnknown - 1, index, elevationCells);
                    }
                }
                else if (cell.UseBaseHeight)
                {
                    // Already determined it is not land to fill in
                    // Unknown area has to start inside Natural Earth polygons
                    startUnknown = -1;
                }
                else if (!InNaturalEarthLand(cell, land))
                {
                    // Not land by Natural Earth polygons, assume base height, ocean
                    // Avoid determining this again
                    cell.UseBaseHeight = true;
                    // Unknown area has to start inside Natural Earth polygons
                    startUnknown = -1;
                }
                else if (startUnknown < 0)
                {
                    startUnknown = index;
                }
            }
            if (startUnknown >= 0)
            {
                // Have unknown area at end
                FillUnknownElevation(vertical, startUnknown - 1, elevationCells.Length, elevationCells);
            }
        }

        /// <summary>
        /// Calculates a linear regression for elevation between the cells before and after an unknown area
        /// </summary>
        /// <param name="vertical">If setting vertical scan value</param>
        /// <param name="start">Cell before unknown area</param>
        /// <param name="end">Cell after unknown area</param>
        /// <param name="elevationCells">Elevation mesh filled with DEM data, for column or row</param>
        private void FillUnknownElevation(bool vertical, int start, int end, ElevationCell[] elevationCells)
        {
            float? startingElevation = (start >= 0) ? elevationCells[start].Elevation : null;
            float? endingElevation = (end < elevationCells.Length) ? elevationCells[end].Elevation : null;
            if (startingElevation.HasValue && endingElevation.HasValue)
            {
                // Have enough for a linear regression
                float slope = (endingElevation.Value - startingElevation.Value) / (end - start);
                for (int index = start + 1; (index < end) && (index < elevationCells.Length); ++index)
                {
                    float elevation = slope * (index - start) + startingElevation.Value;
                    if (vertical)
                    {
                        elevationCells[index].VerticalScanElevation = elevation;
                    }
                    else
                    {
                        elevationCells[index].HorizontalScanElevation = elevation;
                    }
                }
            }
            else if (startingElevation.HasValue)
            {
                // Assume same as start, since no end
                for (int index = start + 1; (index < end) && (index < elevationCells.Length); ++index)
                {
                    if (vertical)
                    {
                        elevationCells[index].VerticalScanElevation = startingElevation;
                    }
                    else
                    {
                        elevationCells[index].HorizontalScanElevation = startingElevation;
                    }
                }
            }
            else if (endingElevation.HasValue)
            {
                // Assume same as end, since no start
                for (int index = start + 1; (index < end) && (index < elevationCells.Length); ++index)
                {
                    if (vertical)
                    {
                        elevationCells[index].VerticalScanElevation = endingElevation;
                    }
                    else
                    {
                        elevationCells[index].HorizontalScanElevation = endingElevation;
                    }
                }
            }
        }

        /// <summary>
        /// Saves the given elevation cells as an equivalent STL
        /// </summary>
        /// <param name="elevationCells">Elevation mesh</param>
        /// <param name="cellRows">Number of elevation mesh rows</param>
        /// <param name="cellColumns">Number of elevation mesh columns</param>
        /// <param name="minElevation">Minimum elevation from DEM data</param>
        /// <param name="maxElevation">Maximum elevation from DEM data</param>
        private void SaveStl(ElevationCell[,] elevationCells, int cellRows, int cellColumns, float? minElevation, float? maxElevation)
        {
            try
            {
                SaveFileDialog stlSaveDlg = new SaveFileDialog();
                stlSaveDlg.Filter = "STL File (*.stl)|*.stl";
                stlSaveDlg.DefaultExt = "stl";
                stlSaveDlg.Title = "Save STL As";
                if (true == stlSaveDlg.ShowDialog())
                {
                    if (!minElevation.HasValue || !_extractSettings.AutoScaleThickness)
                    {
                        minElevation = _extractSettings.MinAltitude;
                    }
                    if (!maxElevation.HasValue || !_extractSettings.AutoScaleThickness)
                    {
                        maxElevation = _extractSettings.MaxAltitude;
                    }

                    float scaleElevationSlope = (_extractSettings.Thickness - _extractSettings.BaseThickness) / (maxElevation.Value - minElevation.Value);
                    uint triangleCount =
                        (uint)((cellColumns - 1) * (cellRows - 1) * 2)          // Top triangles
                        + (uint)((cellColumns - 1) * 4 + (cellRows - 1) * 4)    // Side triangles
                        + 2;                                                    // Bottom triangles

                    using (Stream stream = File.Open(stlSaveDlg.FileName, FileMode.Create))
                    {
                        // Header for a binary STL is 80 bytes
                        byte[] header = new byte[80];
                        Encoding.ASCII.GetBytes("Generated by MERIT DEM to STL").CopyTo(header, 0);
                        stream.Write(header, 0, header.Length);
                        StlTriangle.WriteTriangleCount(triangleCount, stream);

                        for (int row = 1; row < cellRows; ++row)
                        {
                            // Left edge triangles
                            ElevationCell topLeftMost = elevationCells[row, 0];
                            ElevationCell bottomLeftMost = elevationCells[row - 1, 0];

                            StlVertex baseTop = new StlVertex(topLeftMost.XCoord, topLeftMost.YCoord, 0);
                            StlVertex baseBottom = new StlVertex(bottomLeftMost.XCoord, bottomLeftMost.YCoord, 0);
                            StlVertex topLeftMostVertex = new StlVertex(
                                topLeftMost.XCoord, topLeftMost.YCoord,
                                CalculateScaledHeight(scaleElevationSlope, topLeftMost.Elevation, minElevation.Value));
                            StlVertex bottomLeftMostVertex = new StlVertex(
                                bottomLeftMost.XCoord, bottomLeftMost.YCoord,
                                CalculateScaledHeight(scaleElevationSlope, bottomLeftMost.Elevation, minElevation.Value));

                            StlTriangle triangle = new StlTriangle(topLeftMostVertex, baseTop, baseBottom);
                            triangle.WriteToStream(stream);
                            triangle = new StlTriangle(topLeftMostVertex, baseBottom, bottomLeftMostVertex);
                            triangle.WriteToStream(stream);

                            // Top mesh triangles
                            for (int col = 1; col < cellColumns; ++ col)
                            {
                                ElevationCell topLeft = elevationCells[row, col - 1];
                                ElevationCell bottomLeft = elevationCells[row - 1, col - 1];
                                ElevationCell topRight = elevationCells[row, col];
                                ElevationCell bottomRight = elevationCells[row - 1, col];

                                StlVertex topLeftVertex = new StlVertex(
                                    topLeft.XCoord, topLeft.YCoord, 
                                    CalculateScaledHeight(scaleElevationSlope, topLeft.Elevation, minElevation.Value));
                                StlVertex bottomLeftVertex = new StlVertex(
                                    bottomLeft.XCoord, bottomLeft.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, bottomLeft.Elevation, minElevation.Value));
                                StlVertex topRightVertex = new StlVertex(
                                    topRight.XCoord, topRight.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, topRight.Elevation, minElevation.Value));
                                StlVertex bottomRightVertex = new StlVertex(
                                    bottomRight.XCoord, bottomRight.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, bottomRight.Elevation, minElevation.Value));

                                triangle = new StlTriangle(topLeftVertex, bottomRightVertex, topRightVertex);
                                triangle.WriteToStream(stream);
                                triangle = new StlTriangle(topLeftVertex, bottomLeftVertex, bottomRightVertex);
                                triangle.WriteToStream(stream);
                            }

                            // Right edge triangles
                            ElevationCell topRightMost = elevationCells[row, cellColumns - 1];
                            ElevationCell bottomRightMost = elevationCells[row - 1, cellColumns - 1];

                            baseTop = new StlVertex(topRightMost.XCoord, topRightMost.YCoord, 0);
                            baseBottom = new StlVertex(bottomRightMost.XCoord, bottomRightMost.YCoord, 0);
                            StlVertex topRightMostVertex = new StlVertex(
                                topRightMost.XCoord, topRightMost.YCoord,
                                CalculateScaledHeight(scaleElevationSlope, topRightMost.Elevation, minElevation.Value));
                            StlVertex bottomRightMostVertex = new StlVertex(
                                bottomRightMost.XCoord, bottomRightMost.YCoord,
                                CalculateScaledHeight(scaleElevationSlope, bottomRightMost.Elevation, minElevation.Value));

                            triangle = new StlTriangle(topRightMostVertex, baseBottom, baseTop);
                            triangle.WriteToStream(stream);
                            triangle = new StlTriangle(topRightMostVertex, bottomRightMostVertex, baseBottom);
                            triangle.WriteToStream(stream);
                        }

                        // Top/Bottom edge triangles
                        for (int col = 1; col < cellColumns; ++col)
                        {
                            // Top edge triangles
                            ElevationCell topLeft = elevationCells[cellRows - 1, col - 1];
                            ElevationCell topRight = elevationCells[cellRows - 1, col];

                            StlVertex baseLeft = new StlVertex(topLeft.XCoord, topLeft.YCoord, 0);
                            StlVertex baseRight = new StlVertex(topRight.XCoord, topRight.YCoord, 0);
                            StlVertex topLeftVertex = new StlVertex(
                                    topLeft.XCoord, topLeft.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, topLeft.Elevation, minElevation.Value));
                            StlVertex topRightVertex = new StlVertex(
                                    topRight.XCoord, topRight.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, topRight.Elevation, minElevation.Value));

                            StlTriangle triangle = new StlTriangle(topLeftVertex, baseRight, baseLeft);
                            triangle.WriteToStream(stream);
                            triangle = new StlTriangle(topLeftVertex, topRightVertex, baseRight);
                            triangle.WriteToStream(stream);

                            // Bottom edge triangles
                            topLeft = elevationCells[0, col - 1];
                            topRight = elevationCells[0, col];

                            baseLeft = new StlVertex(topLeft.XCoord, topLeft.YCoord, 0);
                            baseRight = new StlVertex(topRight.XCoord, topRight.YCoord, 0);
                            topLeftVertex = new StlVertex(
                                    topLeft.XCoord, topLeft.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, topLeft.Elevation, minElevation.Value));
                            topRightVertex = new StlVertex(
                                    topRight.XCoord, topRight.YCoord,
                                    CalculateScaledHeight(scaleElevationSlope, topRight.Elevation, minElevation.Value));

                            triangle = new StlTriangle(topLeftVertex, baseLeft, baseRight);
                            triangle.WriteToStream(stream);
                            triangle = new StlTriangle(topLeftVertex, baseRight, topRightVertex);
                            triangle.WriteToStream(stream);
                        }

                        // Base triangles
                        ElevationCell baseTopLeft = elevationCells[cellRows - 1, 0];
                        ElevationCell baseTopRight = elevationCells[cellRows - 1, cellColumns - 1];
                        ElevationCell baseBottomLeft = elevationCells[0, 0];
                        ElevationCell baseBottomRight = elevationCells[0, cellColumns - 1];

                        StlVertex baseTopLeftVertex = new StlVertex(baseTopLeft.XCoord, baseTopLeft.YCoord, 0);
                        StlVertex baseTopRightVertex = new StlVertex(baseTopRight.XCoord, baseTopRight.YCoord, 0);
                        StlVertex baseBottomLeftVertex = new StlVertex(baseBottomLeft.XCoord, baseBottomLeft.YCoord, 0);
                        StlVertex baseBottomRightVertex = new StlVertex(baseBottomRight.XCoord, baseBottomRight.YCoord, 0);

                        StlTriangle baseTriangle = new StlTriangle(baseTopLeftVertex, baseBottomRightVertex, baseBottomLeftVertex);
                        baseTriangle.WriteToStream(stream);
                        baseTriangle = new StlTriangle(baseTopLeftVertex, baseTopRightVertex, baseBottomRightVertex);
                        baseTriangle.WriteToStream(stream);
                    }
                }

                MessageBox.Show("Finished", "Generate STL", MessageBoxButton.OK, MessageBoxImage.None);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Problem saving STL file:\n\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Calculates the scale height from the elevation
        /// </summary>
        /// <param name="scaleElevationSlope">Pre-calculated elevation scaling slope</param>
        /// <param name="elevation">Elevation for the mesh cell</param>
        /// <param name="minElevation">Minimum elevation that is at base thickness</param>
        /// <returns>Calculated STL height</returns>
        private float CalculateScaledHeight(float scaleElevationSlope, float? elevation, float minElevation)
        {
            float height = _extractSettings.BaseThickness;
            if (elevation.HasValue)
            {
                height = scaleElevationSlope * (elevation.Value - minElevation) + _extractSettings.BaseThickness;
            }
            return height;
        }

        /// <summary>
        /// Extracts the polygons for testing points
        /// </summary>
        /// <param name="feature">KML feature to extract polygons from</param>
        /// <param name="land">Extracted polygons</param>
        private static void ExtractPlacemarks(SharpKml.Dom.Feature feature, List<LandPolygon> land)
        {
            // Is the passed in value a Placemark?
            if (feature is SharpKml.Dom.Placemark placemark)
            {
                // Assume certain characteristics of the converted Natural Earth land file
                if (placemark.Geometry is SharpKml.Dom.MultipleGeometry multiGeometry)
                {
                    foreach (var geometry in multiGeometry.Geometry)
                    {
                        if (geometry is SharpKml.Dom.Polygon polygon)
                        {
                            land.Add(new LandPolygon(polygon));
                        }
                    }
                }
            }
            else
            {
                // Is it a Container, as the Container might have a child Placemark?
                if (feature is SharpKml.Dom.Container container)
                {
                    // Check each Feature to see if it's a Placemark or another Container
                    foreach (SharpKml.Dom.Feature f in container.Features)
                    {
                        ExtractPlacemarks(f, land);
                    }
                }
            }
        }

        /// <summary>
        /// Test if the given cell is inside any land polygons
        /// </summary>
        /// <param name="cell">Cell to test</param>
        /// <param name="land">Loaded land polygons</param>
        /// <returns>If the given cell is inside any land polygons</returns>
        public bool InNaturalEarthLand(ElevationCell cell, List<LandPolygon> land)
        {
            bool inLand = false;
            foreach (var polygon in land)
            {
                if (polygon.IsPointInPolygon(cell.Longitude, cell.Latitude))
                {
                    inLand = true;
                    break;
                }
            }
            return inLand;
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
