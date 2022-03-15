using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;


namespace Delaunay.Geometry
{
    /// <summary>
    /// A representation of a polygon, which is just a collection
    /// of edges.
    /// </summary>
    public class Polygon
    {

        public List<Edge> edges;
        
        /// <summary>
        /// Construct a new polygon.
        /// </summary>
        public Polygon(List<Edge> inEdges)
        {
            /* Assemble the vertices in a correct ordering */
            edges = new List<Edge>();

            edges.Add(inEdges[0]);
            inEdges.RemoveAt(0);

            while (inEdges.Count > 0)
            {
                /* Attempt to find the connected edge */
                Edge nextEdge = null;
                foreach (var edge in inEdges)
                {
                    if (edge.a.Equals(edges[^1].b))
                    {
                        /* This edge is the next */
                        nextEdge = edge;
                        inEdges.Remove(edge);
                        break;
                    }
                    else if (edge.b.Equals(edges[^1].b))
                    {
                        /* Flip this edge to get the next edge */
                        nextEdge = new Edge(edge.b, edge.a);
                        inEdges.Remove(edge);
                        break;
                    }
                }
                
                /* If we found an edge, continue. If no edge was found, this is an incomplete polygon */
                if (nextEdge != null)
                {
                    edges.Add(nextEdge);
                    
                    /* Sanity check - if we completely looped around, this polygon is complete. */
                    if (inEdges.Count > 0)
                    {
                        if (edges[0].a.Equals(edges[^1].b))
                        {
                            Debug.LogWarning("Polygon formed with extra edges not used. Throwing away extras.");
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogError("Incomplete polygon found. Breaking early." + "\n" +
                                   "currEdges: " + string.Join(", ", edges) + "\n" +
                                   "inEdges: " + string.Join(", ", inEdges));
                    break;
                }
            }
        }

        public override string ToString()
        {
            return "POLY:[" + (string.Join(", ", edges)) +  "]";
        }
    }
}
