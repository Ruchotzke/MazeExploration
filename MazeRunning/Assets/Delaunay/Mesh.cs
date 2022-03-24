using System.Collections.Generic;
using Codice.Client.GameUI.Update;
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
        /// <returns>A list relating sites to voronoi polygons.</returns>
        public (List<(float2 site, Polygon polygon)> voronoi, List<float2> circumcenters) GenerateDualGraph(float2 min, float2 max)
        {
            /* Generate a list of edges in the current mesh */
            var edges = GetEdges();
            
            /* Create a container for the output structure */
            List<Edge> dual = new List<Edge>();
            Dictionary<float2, List<Edge>> Polygons = new Dictionary<float2, List<Edge>>(); //a mapping from sites to a list of edges
            Dictionary<float2, byte> CircumcenterDict = new Dictionary<float2, byte>();

            /* To simplify the voronoi code below, add all sites to the polygons dictionary */
            foreach (var tri in Triangles)
            {
                if (!Polygons.ContainsKey(tri.a))
                {
                    Polygons.Add(tri.a, new List<Edge>());
                }
                
                if (!Polygons.ContainsKey(tri.b))
                {
                    Polygons.Add(tri.b, new List<Edge>());
                }
                
                if (!Polygons.ContainsKey(tri.c))
                {
                    Polygons.Add(tri.c, new List<Edge>());
                }
            }
            
            /* Keep track of any edges cut off by the boundary. We need to add edges to finish these polygons */
            var minXEdges = new List<(float2 point, List<float2> sites)>();
            var minYEdges = new List<(float2 point, List<float2> sites)>();
            var maxXEdges = new List<(float2 point, List<float2> sites)>();
            var maxYEdges = new List<(float2 point, List<float2> sites)>();
            var borderEdges = new List<(Edge edge, List<float2> sites)>();
            
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

                    if (!CircumcenterDict.ContainsKey(a)) CircumcenterDict.Add(a, 0);
                    if (!CircumcenterDict.ContainsKey(b)) CircumcenterDict.Add(b, 0);

                    /* Generate a new edge and clip it*/
                    Edge voronoiEdge = new Edge(a, b);
                    var clipResult = Clipping.ClipEdge(voronoiEdge, min, max);
                    if (clipResult.isVisible)
                    {
                        /* If the edge was not clipped, add it */
                        if (!clipResult.editedEdge)
                        {
                            dual.Add(voronoiEdge);
                            Polygons[edge.a].Add(voronoiEdge);
                            Polygons[edge.b].Add(voronoiEdge);
                        }
                        else
                        {
                            /* This edge was clipped. We will need to add it into a list to finish later */
                            borderEdges.Add((voronoiEdge, new List<float2>(){edge.a, edge.b}));
                            dual.Add(voronoiEdge);
                            Polygons[edge.a].Add(voronoiEdge);
                            Polygons[edge.b].Add(voronoiEdge);
                        }
                    }
                }
                else
                {
                    /* This edge has only 1 neighbor - it is an edge edge. */
                    /* Still generate an edge, just perpendicular to this edge and a distance outward */
                    float2 a = edges[edge][0].GetCircumcenter();
                    
                    /* Before we do a lot of math, check to see if A lies within the boundary. */
                    /* If A is outside of the boundary, we don't need to draw to an edge. This will count as an edge */
                    if (a.x < min.x || a.x > max.x || a.y < min.y || a.y > max.y) continue;
                    
                    /* Calculate the perp bisector direction, then generate a second point */
                    /* The second point will intersect with the boundary since it is infinite */
                    float dx = -(edge.b.y - edge.a.y);
                    float dy = edge.b.x - edge.a.x;
                    
                    /* Draw a line far larger than necessary, then clip it */
                    Edge voronoiEdge = new Edge(a, a + new float2(dx * 100f, dy * 100f));
                    var clipResult = Clipping.ClipEdge(voronoiEdge, min, max);
                    dual.Add(voronoiEdge);
                    Polygons[edge.a].Add(voronoiEdge);
                    Polygons[edge.b].Add(voronoiEdge);
                    borderEdges.Add(((voronoiEdge), new List<float2>(){edge.a, edge.b}));
                }
            }
            
            /* Assemble border edges into their correct arrays */
            while (borderEdges.Count > 0)
            {
                float2 a = borderEdges[0].edge.a;
                float2 b = borderEdges[0].edge.b;
                
                /* put A into the correct bin if necessary */
                if (a.x == min.x)
                {
                    minXEdges.Add((a, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                else if (a.x == max.x)
                {
                    maxXEdges.Add((a, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                else if (a.y == min.y)
                {
                    minYEdges.Add((a, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                else if (a.y == max.y)
                {
                    maxYEdges.Add((a, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                
                /* put B into the correct bin if necessary */
                if (b.x == min.x)
                {
                    minXEdges.Add((b, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                else if (b.x == max.x)
                {
                    maxXEdges.Add((b, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                else if (b.y == min.y)
                {
                    minYEdges.Add((b, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                else if (b.y == max.y)
                {
                    maxYEdges.Add((b, new List<float2>(){borderEdges[0].sites[0], borderEdges[0].sites[1]}));
                }
                
                /* Remove this edge from the list */
                borderEdges.RemoveAt(0);
            }

            /* Fill in the border edges */
            minXEdges.Sort((a, b) => a.point.y.CompareTo(b.point.y));
            maxXEdges.Sort((a, b) => a.point.y.CompareTo(b.point.y));
            minYEdges.Sort((a, b) => a.point.x.CompareTo(b.point.x));
            maxYEdges.Sort((a, b) => a.point.x.CompareTo(b.point.x));
            
            /* MinX */
            float2 prev = min;
            foreach (var t in minXEdges)
            {
                dual.Add(new Edge(prev, t.point));

                /* add this edge to the correct polygon */
                t.sites.Sort((a, b) => a.y.CompareTo(b.y));
                Polygons[t.sites[0]].Add(new Edge(prev, t.point));
                
                prev = t.point;
            }
            dual.Add(new Edge(prev, new float2(min.x, max.y)));
            Polygons[minXEdges[^1].sites[1]].Add(new Edge(prev, new float2(min.x, max.y)));
            
            /* MaxX */
            prev = new float2(max.x, min.y);
            foreach (var t in maxXEdges)
            {
                dual.Add(new Edge(prev, t.point));
                
                /* add this edge to the correct polygon */
                t.sites.Sort((a, b) => a.y.CompareTo(b.y));
                Polygons[t.sites[0]].Add(new Edge(prev, t.point));
                
                prev = t.point;
            }
            dual.Add(new Edge(prev, max));
            Polygons[maxXEdges[^1].sites[1]].Add(new Edge(prev, max));
            
            /* MinY */
            prev = min;
            foreach (var t in minYEdges)
            {
                dual.Add(new Edge(prev, t.point));

                /* add this edge to the correct polygon */
                t.sites.Sort((a, b) => a.x.CompareTo(b.x));
                Polygons[t.sites[0]].Add(new Edge(prev, t.point));
                
                prev = t.point;
            }
            dual.Add(new Edge(prev, new float2(max.x, min.y)));
            Polygons[minYEdges[^1].sites[1]].Add(new Edge(prev, new float2(max.x, min.y)));
            
            /* MaxY */
            prev = new float2(min.x, max.y);
            foreach (var t in maxYEdges)
            {
                dual.Add(new Edge(prev, t.point));

                /* add this edge to the correct polygon */
                t.sites.Sort((a, b) => a.x.CompareTo(b.x));
                Polygons[t.sites[0]].Add(new Edge(prev, t.point));
                
                prev = t.point;
            }
            dual.Add(new Edge(prev, max));
            Polygons[maxYEdges[^1].sites[1]].Add(new Edge(prev, max));

            /* Assemble edges into polygons */
            int polygonCount = 0;
            List<(float2 site, Polygon polygon)> polygonOutput = new List<(float2 site, Polygon polygon)>();
            foreach (var key in Polygons.Keys)
            {
                polygonOutput.Add((key, new Polygon(Polygons[key])));
                polygonCount++;
            }

            /* Assemble circumcenters into a list to return */
            List<float2> circumcenters = new List<float2>();
            circumcenters.AddRange(CircumcenterDict.Keys);
            
            /* Return the complete dual of this mesh */
            return (polygonOutput, circumcenters);
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