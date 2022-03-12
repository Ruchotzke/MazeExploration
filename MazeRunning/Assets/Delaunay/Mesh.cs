using System.Collections.Generic;
using Delaunay.Geometry;
using PlasticGui.WorkspaceWindow.BranchExplorer;
using Unity.Mathematics;
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
        /// Compile together a list of all edges in this polygon, along
        /// with the triangles they are a part of.
        /// </summary>
        /// <returns></returns>
        public Dictionary<Edge, List<Triangle>> GetEdges()
        {
            Dictionary<Edge, List<Triangle>> edgeDict = new Dictionary<Edge, List<Triangle>>();
            
            /* Iterate over each triangle */
            foreach (var triangle in Triangles)
            {
                /* Iterate over each edge */
                foreach (var edge in triangle.GetEdges())
                {
                    /* If this edge is new, create a dictionary entry. */
                    if (!edgeDict.ContainsKey(edge))
                    {
                        edgeDict.Add(edge, new List<Triangle>());
                    }
                    
                    /* Add it to an existing entry */
                    edgeDict[edge].Add(triangle);
                }
            }

            return edgeDict;
        }

        /// <summary>
        /// Generate the dual of this mesh, returning a list of edges
        /// representing the dual graph.
        /// </summary>
        /// <param name="min">The lower left corner of the rect containing this dual graph</param>
        /// <param name="max">The upper right corner of the rect containing this dual graph.</param>
        /// <returns>A list of edges describing the generated polygon.</returns>
        public List<Edge> GenerateDualGraph(float2 min, float2 max)
        {
            /* Generate a list of edges in the current mesh */
            var edges = GetEdges();
            
            /* Create a container for the output structure */
            List<Edge> dual = new List<Edge>();
            
            /* Keep track of any edges cut off by the boundary. We need to add edges to finish these polygons */
            List<float2> minXEdges = new List<float2>();
            List<float2> minYEdges = new List<float2>();
            List<float2> maxXEdges = new List<float2>();
            List<float2> maxYEdges = new List<float2>();
            
            /* Dual graph algorithm: Generate a new edge for each
             connected face. This will create a non-triangular structure,
             thus a mesh can no longer hold it. */
            foreach (var edge in edges.Keys)
            {
                /* Connect up each neighbor of this edge */
                if (edges[edge].Count > 1)
                {
                    float2 a = edges[edge][0].GetCircumcenter();
                    float2 b = edges[edge][1].GetCircumcenter();
                    float2 delta = b - a;
                    
                    /* If A and B are in bounds, just add the edge */
                    if (a.x >= min.x && a.x <= max.x && a.y >= min.y && a.y <= max.y &&
                        b.x >= min.x && b.x <= max.x && b.y >= min.y && b.y <= max.y)
                    {
                        dual.Add(new Edge(a, b));
                        continue;
                    }

                    /* If A or B lies outside of the boundary, we need to clip this edge short */
                    /* EDGE CASE NOT HANDLED: WHAT HAPPENS IF A POINT IS ON A CORNER OOB (over max x and max y) */
                    /* TODO: Fix edge case with COHEN SUTHERLAND */
                    if (a.x < min.x)
                    {
                        float t = (min.x - a.x) / delta.x;
                        float y = a.y + delta.y * t;
                        Edge newEdge = new Edge(new float2(min.x, y), b);
                        minXEdges.Add(newEdge.a);
                        dual.Add(newEdge);
                    }
                    else if (a.x > max.x)
                    {
                        float t = (max.x - b.x) / -delta.x;
                        float y = b.y - delta.y * t;
                        Edge newEdge = new Edge(b, new float2(max.x, y));
                        maxXEdges.Add(newEdge.b);
                        dual.Add(newEdge);
                    }
                    else if(a.y < min.y)
                    {
                        float t = (min.y - a.y) / delta.y;
                        float x = a.x + delta.x * t;
                        Edge newEdge = new Edge(new float2(x, min.y), b);
                        minYEdges.Add(newEdge.a);
                        dual.Add(newEdge);
                    }
                    else if(a.y > max.y)
                    {
                        float t = (max.y - b.y) / -delta.y;
                        float x = b.x - delta.x * t;
                        Edge newEdge = new Edge(b, new float2(x, max.y));
                        maxYEdges.Add(newEdge.b);
                        dual.Add(newEdge);
                    }
                    
                    if (b.x < min.x)
                    {
                        float t = (min.x - b.x) / delta.x;
                        float y = b.y + delta.y * t;
                        Edge newEdge = new Edge(new float2(min.x, y), a);
                        minXEdges.Add(newEdge.a);
                        dual.Add(newEdge);
                    }
                    else if (b.x > max.x)
                    {
                        float t = (max.x - a.x) / -delta.x;
                        float y = a.y - delta.y * t;
                        Edge newEdge = new Edge(a, new float2(max.x, y));
                        maxXEdges.Add(newEdge.b);
                        dual.Add(newEdge);
                    }
                    else if(b.y < min.y)
                    {
                        float t = (min.y - b.y) / delta.y;
                        float x = b.x + delta.x * t;
                        Edge newEdge = new Edge(new float2(x, min.y), a);
                        minYEdges.Add(newEdge.a);
                        dual.Add(newEdge);
                    }
                    else if(b.y > max.y)
                    {
                        float t = (max.y - a.y) / -delta.y;
                        float x = a.x - delta.x * t;
                        Edge newEdge = new Edge(a, new float2(x, max.y));
                        maxYEdges.Add(newEdge.b);
                        dual.Add(newEdge);
                    }
                }
                else
                {
                    /* This edge has only 1 neighbor - it is an edge edge. */
                    /* Still generate an edge, just perpindicular to this edge and a distance outward */
                    float2 a = edges[edge][0].GetCircumcenter();
                    
                    /* Before we do a lot of math, check to see if A lies within the boundary. */
                    /* If A is outside of the boundary, we don't need to draw to an edge. This will count as an edge */
                    if (a.x < min.x || a.x > max.x || a.y < min.y || a.y > max.y) continue;
                    
                    /* Calculate the perp bisector direction, then generate a second point */
                    /* The second point will intersect with the boundary since it is infinite */
                    float dx = -(edge.b.y - edge.a.y);
                    float dy = edge.b.x - edge.a.x;

                    /* Vertical Collision Check */
                    if (dx != 0)
                    {
                        if (dx > 0)
                        {
                            /* Right collision */
                            float t = (max.x - a.x) / dx;
                            float y = a.y + t * dy;
                            
                            /* Was this collision valid? If so, we found our point. If not we need to test a top/bot */
                            if (y >= min.y && y <= max.y)
                            {
                                float2 b = new float2(max.x, y);
                                Edge newEdge = new Edge(a, b);
                                maxXEdges.Add(newEdge.b);
                                dual.Add(newEdge);
                            }
                        }
                        else
                        {
                            /* Left collision */
                            float t = (min.x - a.x) / dx;
                            float y = a.y + t * dy;
                            
                            /* Was this collision valid? If so, we found our point. If not we need to test a top/bot */
                            if (y >= min.y && y <= max.y)
                            {
                                float2 b = new float2(min.x, y);
                                Edge newEdge = new Edge(a, b);
                                minXEdges.Add(newEdge.b);
                                dual.Add(newEdge);
                            }
                        }
                    }

                    /* Horizontal Collision Check */
                    if (dy != 0)
                    {
                        if (dy > 0)
                        {
                            /* Top Collision */
                            float t = (max.y - a.y) / dy;
                            float x = a.x + t * dx;
                            
                            /* Was this collision valid? If so, we found our point. If not we need to test a top/bot */
                            if (x >= min.x && x <= max.x)
                            {
                                float2 b = new float2(x, max.y);
                                Edge newEdge = new Edge(a, b);
                                maxYEdges.Add(newEdge.b);
                                dual.Add(newEdge);
                            }
                        }
                        else
                        {
                            /* Bottom Collision */
                            float t = (min.y - a.y) / dy;
                            float x = a.x + t * dx;
                            
                            /* Was this collision valid? If so, we found our point. If not we need to test a top/bot */
                            if (x >= min.x && x <= max.x)
                            {
                                float2 b = new float2(x, min.y);
                                Edge newEdge = new Edge(a, b);
                                minYEdges.Add(newEdge.b);
                                dual.Add(newEdge);
                            }
                        }
                    }
                }
            }

            /* We now have internal voronoi, but edge cells need to be completed */
            /* Start by sorting each list in an order to make new edge calculation easier */
            minXEdges.Sort((a, b) => (int)(a.y - b.y));
            minYEdges.Sort((a, b) => (int)(a.x - b.x));
            maxXEdges.Sort((a, b) => (int)(a.y - b.y));
            maxYEdges.Sort((a, b) => (int)(a.x - b.x));
            
            /* Add edges to fill in the outer boundary of the cells */
            /* MinX */
            float2 prev = min;
            foreach (var t in minXEdges)
            {
                dual.Add(new Edge(prev, t));
                prev = t;
            }
            dual.Add(new Edge(prev, new float2(min.x, max.y)));
            
            /* MaxX */
            prev = new float2(max.x, min.y);
            foreach (var vert in maxXEdges)
            {
                dual.Add(new Edge(prev, vert));
                prev = vert;
            }
            dual.Add(new Edge(prev, max));
            
            /* MinY */
            prev = min;
            foreach (var t in minYEdges)
            {
                dual.Add(new Edge(prev, t));
                prev = t;
            }
            dual.Add(new Edge(prev, new float2(max.x, min.y)));
            
            /* MaxY */
            prev = new float2(min.x, max.y);
            foreach (var vert in maxYEdges)
            {
                dual.Add(new Edge(prev, vert));
                prev = vert;
            }
            dual.Add(new Edge(prev, max));
            
            /* Return the complete dual of this mesh */
            return dual;
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