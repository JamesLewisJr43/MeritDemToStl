using OSGeo.GDAL;
using System;
using System.Collections.Generic;
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

namespace GeoTiffViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
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
            Gdal.AllRegister();
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Dataset ds = Gdal.Open(PathEdit.Text, Access.GA_ReadOnly);
                if (ds == null)
                {
                    throw new Exception("Unable to open " + PathEdit.Text);
                }

                ProjectionEdit.Text = ds.GetProjection();
                RasterCountEdit.Text = ds.RasterCount.ToString();
                RasterXSizeEdit.Text = ds.RasterXSize.ToString();
                RasterYSizeEdit.Text = ds.RasterYSize.ToString();

                Driver drv = ds.GetDriver();
                if (drv == null)
                {
                    throw new Exception("Can't get driver.");
                }
                DriverEdit.Text = drv.LongName;

                if (ds.RasterCount == 0)
                {
                    PreviewImage.Source = null;
                }
                else
                {
                    int[] bandMap = new int[4] { 1, 1, 1, 1 };
                    int channelCount = 1;
                    bool hasAlpha = false;
                    bool isIndexed = false;
                    int channelSize = 8;
                    ColorTable ct = null;
                    // Evaluate the bands and find out a proper image transfer format
                    for (int i = 0; i < ds.RasterCount; i++)
                    {
                        Band band = ds.GetRasterBand(i + 1);
                        if (Gdal.GetDataTypeSize(band.DataType) > 8)
                            channelSize = 16;
                        float[] values = new float[1];
                        band.ReadRaster(0, 0, 1, 1, values, 1, 1, 0, 0);
                        ElevationEdit.Text = values[0].ToString();
                        switch (band.GetRasterColorInterpretation())
                        {
                            case ColorInterp.GCI_AlphaBand:
                                channelCount = 4;
                                hasAlpha = true;
                                bandMap[3] = i + 1;
                                break;
                            case ColorInterp.GCI_BlueBand:
                                if (channelCount < 3)
                                    channelCount = 3;
                                bandMap[0] = i + 1;
                                break;
                            case ColorInterp.GCI_RedBand:
                                if (channelCount < 3)
                                    channelCount = 3;
                                bandMap[2] = i + 1;
                                break;
                            case ColorInterp.GCI_GreenBand:
                                if (channelCount < 3)
                                    channelCount = 3;
                                bandMap[1] = i + 1;
                                break;
                            case ColorInterp.GCI_PaletteIndex:
                                ct = band.GetRasterColorTable();
                                isIndexed = true;
                                bandMap[0] = i + 1;
                                break;
                            case ColorInterp.GCI_GrayIndex:
                                isIndexed = true;
                                bandMap[0] = i + 1;
                                break;
                            default:
                                // we create the bandmap using the dataset ordering by default
                                if (i < 4 && bandMap[i] == 0)
                                {
                                    if (channelCount < i)
                                        channelCount = i;
                                    bandMap[i] = i + 1;
                                }
                                break;
                        }
                    }

                    System.Drawing.Imaging.PixelFormat pixelFormat;
                    DataType dataType;
                    int pixelSpace;

                    if (isIndexed)
                    {
                        pixelFormat = System.Drawing.Imaging.PixelFormat.Format8bppIndexed;
                        dataType = DataType.GDT_Byte;
                        pixelSpace = 1;
                    }
                    else
                    {
                        if (channelCount == 1)
                        {
                            if (channelSize > 8)
                            {
                                pixelFormat = System.Drawing.Imaging.PixelFormat.Format16bppGrayScale;
                                dataType = DataType.GDT_Int16;
                                pixelSpace = 2;
                            }
                            else
                            {
                                pixelFormat = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
                                channelCount = 3;
                                dataType = DataType.GDT_Byte;
                                pixelSpace = 3;
                            }
                        }
                        else
                        {
                            if (hasAlpha)
                            {
                                if (channelSize > 8)
                                {
                                    pixelFormat = System.Drawing.Imaging.PixelFormat.Format64bppArgb;
                                    dataType = DataType.GDT_UInt16;
                                    pixelSpace = 8;
                                }
                                else
                                {
                                    pixelFormat = System.Drawing.Imaging.PixelFormat.Format32bppArgb;
                                    dataType = DataType.GDT_Byte;
                                    pixelSpace = 4;
                                }
                                channelCount = 4;
                            }
                            else
                            {
                                if (channelSize > 8)
                                {
                                    pixelFormat = System.Drawing.Imaging.PixelFormat.Format48bppRgb;
                                    dataType = DataType.GDT_UInt16;
                                    pixelSpace = 6;
                                }
                                else
                                {
                                    pixelFormat = System.Drawing.Imaging.PixelFormat.Format24bppRgb;
                                    dataType = DataType.GDT_Byte;
                                    pixelSpace = 3;
                                }
                                channelCount = 3;
                            }
                        }
                    }

                    // Create a Bitmap to store the GDAL image in
                    using (System.Drawing.Bitmap bitmap = new System.Drawing.Bitmap(ds.RasterXSize, ds.RasterYSize, pixelFormat))
                    {

                        if (isIndexed)
                        {
                            // setting up the color table
                            if (ct != null)
                            {
                                int iCol = ct.GetCount();
                                System.Drawing.Imaging.ColorPalette pal = bitmap.Palette;
                                for (int i = 0; i < iCol; i++)
                                {
                                    ColorEntry ce = ct.GetColorEntry(i);
                                    pal.Entries[i] = System.Drawing.Color.FromArgb(ce.c4, ce.c1, ce.c2, ce.c3);
                                }
                                bitmap.Palette = pal;
                            }
                            else
                            {
                                // grayscale
                                System.Drawing.Imaging.ColorPalette pal = bitmap.Palette;
                                for (int i = 0; i < 256; i++)
                                    pal.Entries[i] = System.Drawing.Color.FromArgb(255, i, i, i);
                                bitmap.Palette = pal;
                            }
                        }

                        // Use GDAL raster reading methods to read the image data directly into the Bitmap
                        System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(new System.Drawing.Rectangle(0, 0, ds.RasterXSize, ds.RasterYSize), System.Drawing.Imaging.ImageLockMode.ReadWrite, pixelFormat);

                        int stride = bitmapData.Stride;
                        IntPtr buf = bitmapData.Scan0;

                        ds.ReadRaster(0, 0, ds.RasterXSize, ds.RasterYSize, buf, ds.RasterXSize, ds.RasterYSize, dataType,
                            channelCount, bandMap, pixelSpace, stride, 1);

                        BitmapImage bitmapImage = new BitmapImage();
                        using (MemoryStream memory = new MemoryStream())
                        {
                            bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Png);
                            memory.Position = 0;
                            bitmapImage.BeginInit();
                            bitmapImage.StreamSource = memory;
                            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                            bitmapImage.EndInit();
                        }
                        PreviewImage.Source = bitmapImage;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Problem loading GeoTiff:\n\n" + ex.ToString(), "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_Unloaded(object sender, RoutedEventArgs e)
        {
            
        }

        private void GetElevation_Click(object sender, RoutedEventArgs e)
        {
            if (PreviewImage.Source == null)
                return;

            BitmapSource bitmap = (BitmapSource)PreviewImage.Source;
            var bytesPerPixel = (bitmap.Format.BitsPerPixel + 7) / 8;
            var bytes = new byte[bytesPerPixel];
            var rect = new Int32Rect(int.Parse(AtXEdit.Text), int.Parse(AtYEdit.Text), 1, 1);

            bitmap.CopyPixels(rect, bytes, bytesPerPixel, 0);
            StringBuilder sb = new StringBuilder();
            foreach (var b in bytes)
            {
                sb.AppendFormat("{0:X2}", b);
            }
            ElevationEdit.Text = sb.ToString();
        }
    }
}
