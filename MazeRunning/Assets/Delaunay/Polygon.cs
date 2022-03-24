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

        public List<float2> vertices;

        public bool ValidPolygon = true;
        
        /// <summary>
        /// Construct a new polygon.
        /// </summary>
        public Polygon(List<Edge> inEdges)
        {
            /* Assemble the vertices in a correct ordering */
            vertices = new List<float2>();

            vertices.Add(inEdges[0].a);
            inEdges.RemoveAt(0);

            while (inEdges.Count > 0)
            {
                /* Attempt to find the connected edge */
                float2? next = null;
                foreach (var edge in inEdges)
                {
                    if (edge.a.Equals(vertices[^1]))
                    {
                        /* This edge is the next */
                        next = edge.b;
                        inEdges.Remove(edge);
                        break;
                    }
                    else if (edge.b.Equals(vertices[^1]))
                    {
                        /* Flip this edge to get the next edge */
                        next = edge.a;
                        inEdges.Remove(edge);
                        break;
                    }
                }
                
                /* If we found an edge, continue. If no edge was found, this is an incomplete polygon */
                if (next != null)
                {
                    vertices.Add(next.Value);
                    
                    /* Sanity check - if we completely looped around, this polygon is complete. */
                    if (inEdges.Count > 0)
                    {
                        if (vertices[0].Equals(vertices[^1]))
                        {
                            Debug.LogWarning("Polygon formed with extra edges not used. Throwing away extras.");
                            break;
                        }
                    }
                }
                else
                {
                    Debug.LogError("Incomplete polygon found. Breaking early." + "\n" +
                                   "currVerts: " + string.Join(", ", vertices) + "\n" +
                                   "inEdges: " + string.Join(", ", inEdges));

                    ValidPolygon = false;
                    break;
                }
            }

            /* If this is a valid polygon, perform a winding order test */
            if (ValidPolygon)
            {
                float3 a = new float3(vertices[0], 0.0f);
                float3 b = new float3(vertices[1], 0.0f);
                float3 c = new float3(vertices[2], 0.0f);
                float windingOrder = math.cross(a - b, c - b).z;
                if (windingOrder < 0) //check the sign of the winding order. if wrong, reverse the polygon to ensure easy triangulation
                {
                    /* Reverse the vertices order */
                    var oldVerts = vertices;
                    vertices = new List<float2>();
                    for (int i = oldVerts.Count - 1; i >= 0; i--)
                    {
                        vertices.Add(oldVerts[i]);
                    }
                }
            }
        }

        /// <summary>
        /// A private constructor which just assigns directly, doesn't need to do the extra
        /// polygonization from edges.
        /// </summary>
        /// <param name="verts"></param>
        private Polygon(List<float2> verts)
        {
            vertices = verts;
        }

        /// <summary>
        /// Compute the edges of this polygon and return a list.
        /// </summary>
        /// <returns></returns>
        public List<Edge> GetEdges()
        {
            List<Edge> edges = new List<Edge>();

            for(int i = 0; i < vertices.Count - 1; i++)
            {
                edges.Add(new Edge(vertices[i], vertices[i+1]));
            }
            edges.Add(new Edge(vertices[^1], vertices[0]));

            return edges;
        }

        /// <summary>
        /// Scale the polygon (relative to its center) by the scale provided.
        /// Does not need to be uniform, thus the float2 argument.
        /// </summary>
        /// <param name="scale">The multiplier for local vertex positions.</param>
        /// <returns>A new polygon which is a scaled version of this one.</returns>
        public Polygon ScalePolygon(float2 scale)
        {
            /* Compute the center of this polygon */
            float2 center = float2.zero;
            foreach(var vert in vertices)
            {
                center += vert;
            }
            center /= vertices.Count;

            /* Generate a list of localPositions */
            /* Scale the positions as well while we are iterating */
            /* To finish the scale, move the transformed vertex back into position */
            List<float2> local = new List<float2>();
            foreach(var vert in vertices)
            {
                float2 localVert = vert - center;
                localVert *= scale;
                local.Add(localVert + center);
            }

            /* Generate a new polygon to return */
            return new Polygon(local);
        }

        public override string ToString()
        {
            return "POLY:[" + (string.Join(", ", vertices)) +  "]";
        }
    }
}
