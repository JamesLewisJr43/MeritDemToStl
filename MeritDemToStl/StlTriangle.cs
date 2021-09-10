using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MeritDemToStl
{
    /// <summary>
    /// Represents an STL triangle
    /// </summary>
    public class StlTriangle
    {
        /// <summary>
        /// Normal vector for the face
        /// </summary>
        public StlVertex Normal { get; private set; }

        /// <summary>
        /// First vertex of the triangle
        /// </summary>
        public StlVertex Vertex1 { get; private set; }

        /// <summary>
        /// Second vertex of the triangle
        /// </summary>
        public StlVertex Vertex2 { get; private set; }

        /// <summary>
        /// Third vertex of the triangle
        /// </summary>
        public StlVertex Vertex3 { get; private set; }

        /// <summary>
        /// Attribute byte count, unused
        /// </summary>
        public ushort AttributeByteCount { get; private set; }

        public StlTriangle(StlVertex vertex1, StlVertex vertex2, StlVertex vertex3)
        {
            // Calculate the surface normal for the triangle as the cross
            // product of the vectors from the first vertex to the second
            // vertex and the first vertex to the third vertex
            float ux = vertex2.X - vertex1.X;
            float uy = vertex2.Y - vertex1.Y;
            float uz = vertex2.Z - vertex1.Z;
            float vx = vertex3.X - vertex1.X;
            float vy = vertex3.Y - vertex1.Y;
            float vz = vertex3.Z - vertex1.Z;
            Normal = new StlVertex( uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx);
            Vertex1 = vertex1;
            Vertex2 = vertex2;
            Vertex3 = vertex3;
            AttributeByteCount = 0;
        }

        /// <summary>
        /// Writes this triangle as binary STL to the given stream
        /// </summary>
        /// <param name="stream">Stream to write to</param>
        public void WriteToStream(Stream stream)
        {
            Normal.WriteToStream(stream);
            Vertex1.WriteToStream(stream);
            Vertex2.WriteToStream(stream);
            Vertex3.WriteToStream(stream);
            byte[] bytes = BitConverter.GetBytes(AttributeByteCount);
            if (!BitConverter.IsLittleEndian)
            {
                // STL is Little Endian, need to reverse the bytes
                bytes = new byte[] { bytes[1], bytes[0] };
            }
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Writes the given STL triangle count to the given stream as binary
        /// </summary>
        /// <param name="count">Triangle count to write</param>
        /// <param name="stream">Stream to write to</param>
        public static void WriteTriangleCount(uint count, Stream stream)
        {
            byte[] bytes = BitConverter.GetBytes(count);
            if (!BitConverter.IsLittleEndian)
            {
                // STL is Little Endian, need to reverse the bytes
                bytes = new byte[] { bytes[3], bytes[2], bytes[1], bytes[0] };
            }
            stream.Write(bytes, 0, bytes.Length);
        }
    }
}
