using System.Collections.Generic;
using Delaunay.Geometry;

namespace Delaunay.Triangulation
{
    /// <summary>
    /// A mesh is just a collection of vertices.
    /// </summary>
    public class Mesh
    {
        public List<Triangle> Triangles = new List<Triangle>();

        /// <summary>
        /// Construct a new mesh.
        /// </summary>
        public Mesh()
        {
            
        }

        /// <summary>
        /// Add a triangle to the mesh.
        /// </summary>
        /// <param name="t"></param>
        public void AddTriangle(Triangle t)
        {
            Triangles.Add(t);
        }
    }
}