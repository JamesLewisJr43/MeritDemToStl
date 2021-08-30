using MapControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace MeritDemToStl
{
    /// <summary>
    /// Settings for creating a STL from DEM data
    /// </summary>
    public class ExtractSettings
    {
        /// <summary>
        /// Resolution of a mesh grid cell
        /// </summary>
        public double Resolution { get; set; }

        /// <summary>
        /// How wide the generated STL is
        /// </summary>
        public double Width { get; set; }

        /// <summary>
        /// How tall the generated STL is
        /// </summary>
        public double Height { get; set; }

        /// <summary>
        /// How thick the generated STL is
        /// </summary>
        public double Thickness { get; set; }

        /// <summary>
        /// If the thickness of the STL should automatically scale based on the min and max altitude in the DEM data
        /// </summary>
        public bool AutoScaleThickness { get; set; }

        /// <summary>
        /// Minimum altitude to use for scaling the thickness
        /// </summary>
        public double MinAltitude { get; set; }

        /// <summary>
        /// Maximum altitude to use for scaling the thickness
        /// </summary>
        public double MaxAltitude { get; set; }

        /// <summary>
        /// Bounds of DEM data to convert to STL
        /// </summary>
        public BoundingBox Bounds { get; set; }

        /// <summary>
        /// Create with default settings
        /// </summary>
        public ExtractSettings()
        {
            Resolution = 0.25;
            Width = 914.4;
            Height = 609.6;
            Thickness = 25.4;
            AutoScaleThickness = true;
            // Lowest point in North America
            MinAltitude = -85.9536;
            // Highest point in North America
            MaxAltitude = 6190;
        }
    }
}
