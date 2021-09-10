using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeritDemToStl
{
    /// <summary>
    /// Represents a STL vertex
    /// </summary>
    public class StlVertex
    {
        /// <summary>
        /// X coordinate
        /// </summary>
        public float X { get; private set; }

        /// <summary>
        /// Y coordinate
        /// </summary>
        public float Y { get; private set; }

        /// <summary>
        /// Z coordinate
        /// </summary>
        public float Z { get; private set; }

        /// <summary>
        /// Create vertex with given coordinates
        /// </summary>
        /// <param name="x">X coordinate</param>
        /// <param name="y">Y coordinate</param>
        /// <param name="z">Z coordinate</param>
        public StlVertex(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// Writes this vertex as binary STL to the given stream
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public void WriteToStream(Stream stream)
        {
            WriteCoordinateToStream(X, stream);
            WriteCoordinateToStream(Y, stream);
            WriteCoordinateToStream(Z, stream);
        }

        /// <summary>
        /// Writes the given coordinate in binary STL to the given stream
        /// </summary>
        /// <param name="coordinate">Coordinate to write</param>
        /// <param name="stream">Stream to write to</param>
        private void WriteCoordinateToStream(float coordinate, Stream stream)
        {
            byte[] bytes = BitConverter.GetBytes(coordinate);
            if (!BitConverter.IsLittleEndian)
            {
                // STL is Little Endian, need to reverse the bytes
                bytes = new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] };
            }
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
