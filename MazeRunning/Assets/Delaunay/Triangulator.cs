using System.Collections.Generic;
using System.Security;
using Delaunay.Geometry;
using Unity.Mathematics;
using UnityEngine;

namespace Delaunay.Triangulation
{
    /// <summary>
    /// A triangulator is responsible for triangulating a set of vertices into
    /// a delaunay triangle mesh.
    /// </summary>
    public class Triangulator
    {
        public static float2 SUPER_A = new float2(0f, -20f);
        public static float2 SUPER_B = new float2(-20f, 20f);
        public static float2 SUPER_C = new float2(20f, 20f);
        
        private Mesh mesh;

        public List<float2> vertices;

        /// <summary>
        /// Construct a new triangulator.
        /// </summary>
        public Triangulator()
        {
            vertices = new List<float2>();
        }

        public Mesh GenerateTriangulation()
        {
            /* first generate a new mesh for our triangles */
            mesh = new Mesh();
            
            /* Create a super triangle to compute bowyer watson */
            Triangle superTriangle = new Triangle(SUPER_A, SUPER_B, SUPER_C); //TODO: Find a more elegant way to find the super triangle.
            mesh.AddTriangle(superTriangle);
            
            /* Incrementally build a triangulation */
            int count = 0;
            foreach (var vertex in vertices)
            {
                /* Debug */
                Debug.Log(vertex);
                Debug.Log(string.Join(",\n", mesh.Triangles));
                
                /* Find all triangles where this point lies in the circumcircle */
                List<Triangle> badTriangles = new List<Triangle>();
                foreach (var triangle in mesh.Triangles)
                {
                    if (triangle.PointLiesInInteriorOfCircumcircle(vertex))
                    {
                        badTriangles.Add(triangle);
                    }
                }
                
                /* Find the points which we need to triangulate to fill the new hole */
                List<Edge> holeEdges = new List<Edge>();
                foreach (var bad in badTriangles)
                {
                    /* if this edge is not shared by any other triangles in the bad triangles,
                     it is part of the hole's edge. */
                    foreach (var edge in bad.GetEdges())
                    {
                        /* Check all other edges to see if this edge should be ignored */
                        bool ignoreEdge = false;
                        foreach (var other in badTriangles)
                        {
                            if (other == bad) continue;
                            
                            foreach (var otherEdge in other.GetEdges())
                            {
                                /* if the two edges are the same, we can forget this edge being included */
                                if (otherEdge.Equals(edge))
                                {
                                    /* This is not an edge edge */
                                    ignoreEdge = true;
                                    break;
                                }
                            }

                            if (ignoreEdge) break;
                        }
                        
                        /* If the edge should not be ignored, add it to the list */
                        if (!ignoreEdge)
                        {
                            Debug.Log("Unique Edge: " + edge);
                            holeEdges.Add(edge);
                        }
                    }
                }
                
                /* Delete the bad triangles */
                foreach (var bad in badTriangles)
                {
                    /* Remove the old triangle */
                    mesh.Triangles.Remove(bad);
                }
                
                /* Triangulate the polygonal hole in the mesh */
                foreach (var edge in holeEdges)
                {
                    mesh.Triangles.Add(new Triangle(edge.a, edge.b, vertex));
                }
                
                /* Checkpoint the number of triangles in the mesh */
                count++;
                // if (count >= 2) return mesh;
                Debug.Log("TRIANGLES: " + mesh.Triangles.Count);
            }
            
            return mesh;
            
            /* Remove ANY triangles involving the super triangle vertices */
            List<Triangle> superTriangles = new List<Triangle>();
            foreach (var tri in mesh.Triangles)
            {
                if (tri.a.Equals(SUPER_A) || tri.a.Equals(SUPER_B) || tri.a.Equals(SUPER_C))
                {
                    superTriangles.Add(tri);
                    continue;
                }
                
                if (tri.b.Equals(SUPER_A) || tri.b.Equals(SUPER_B) || tri.b.Equals(SUPER_C))
                {
                    superTriangles.Add(tri);
                    continue;
                }
                
                if (tri.c.Equals(SUPER_A) || tri.c.Equals(SUPER_B) || tri.c.Equals(SUPER_C))
                {
                    superTriangles.Add(tri);
                }
            }
            foreach (var bad in superTriangles)
            {
                mesh.Triangles.Remove(bad);
            }
            
            /* Finally, return the completed mesh */
            return mesh;
        }

        /// <summary>
        /// Add a single vertex to the triangulator.
        /// </summary>
        /// <param name="vertex"></param>
        public void AddVertex(float2 vertex)
        {
            vertices.Add(vertex);
        }

        /// <summary>
        /// Add multiple vertices to this triangulator.
        /// </summary>
        /// <param name="verts"></param>
        public void AddVertices(List<float2> verts)
        {
            vertices.AddRange(verts);
        }
    }
}