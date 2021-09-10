using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeritDemToStl
{
    /// <summary>
    /// Land area polygon for checking if a point is inside general land boundaries
    /// </summary>
    public class LandPolygon
    {
        /// <summary>
        /// Northern most point of the polygon
        /// </summary>
        private double _boundsNorth;

        /// <summary>
        /// Southern most point of the polygon
        /// </summary>
        private double _boundsSouth;

        /// <summary>
        /// Eastern most point of the polygon
        /// </summary>
        private double _boundsEast;

        /// <summary>
        /// Western most point of the polygon
        /// </summary>
        private double _boundsWest;

        /// <summary>
        /// Points in the polygon
        /// </summary>
        private SharpKml.Base.Vector[] _points;

        /// <summary>
        /// Creates the land polygon for testing points from the given polygon
        /// </summary>
        /// <param name="polygon">KML polygon</param>
        public LandPolygon(SharpKml.Dom.Polygon polygon)
        {
            _boundsNorth = double.MinValue;
            _boundsSouth = double.MaxValue;
            _boundsEast = double.MinValue;
            _boundsWest = double.MaxValue;

            var outerBoundary = polygon.OuterBoundary;
            var linearRing = outerBoundary.LinearRing;
            _points = new SharpKml.Base.Vector[linearRing.Coordinates.Count];
            int index = 0;
            foreach (var point in linearRing.Coordinates)
            {
                _points[index++] = point;
                if (point.Latitude > _boundsNorth)
                {
                    _boundsNorth = point.Latitude;
                }
                if (point.Latitude < _boundsSouth)
                {
                    _boundsSouth = point.Latitude;
                }
                if (point.Longitude > _boundsEast)
                {
                    _boundsEast = point.Longitude;
                }
                if (point.Longitude < _boundsWest)
                {
                    _boundsWest = point.Longitude;
                }
            }
        }

        /// <summary>
        /// Test if the given point location is inside this polygon
        /// </summary>
        /// <param name="longitude">Longitude for the point</param>
        /// <param name="latitude">Latitude for the point</param>
        /// <returns>If given point is inside this polygon</returns>
        public bool IsPointInPolygon(double longitude, double latitude)
        {
            if ((longitude < _boundsWest) || (longitude > _boundsEast) 
                || (latitude < _boundsSouth) || (latitude > _boundsNorth))
            {
                // Point outside bound, no reason to do expensive test
                return false;
            }
            // Derived from: https://www.eecs.umich.edu/courses/eecs380/HANDOUTS/PROJ2/InsidePoly.html
            int i, j;
            bool inside = false;
            for (i = 0, j = _points.Length - 1; i < _points.Length; j = i++)
            {
                if ((((_points[i].Latitude <= latitude) && (latitude < _points[j].Latitude)) ||
                     ((_points[j].Latitude <= latitude) && (latitude < _points[i].Latitude))) &&
                    (longitude < (_points[j].Longitude - _points[i].Longitude) * (latitude - _points[i].Latitude) / (_points[j].Latitude - _points[i].Latitude) + _points[i].Longitude))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}
