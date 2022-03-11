using System.Collections.Generic;
using Delaunay.Geometry;
using UnityEngine;

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

        /// <summary>
        /// Remove all skinny triangles from the mesh.
        /// </summary>
        /// <param name="angleHeuristic">If any angle in the tested triangle is smaller than this heuristic, it is
        /// considered skinny and clipped from the mesh. </param>
        public void RemoveSkinnyTriangles(float angleHeuristic = Mathf.PI / 8)
        {
            /* Compile a list of bad triangles */
            List<Triangle> badTriangles = new List<Triangle>();
            foreach (var triangle in Triangles)
            {
                foreach (var angle in triangle.GetAngles())
                {
                    if (angle < angleHeuristic)
                    {
                        badTriangles.Add(triangle);
                        break;
                    }
                }
            }
            
            /* Remove the bad triangles */
            foreach (var bad in badTriangles)
            {
                Triangles.Remove(bad);
            }
        }
    }
}