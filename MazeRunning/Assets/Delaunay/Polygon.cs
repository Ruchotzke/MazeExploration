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
        public Polygon(List<Edge> edges)
        {
            this.edges = edges;
        }

        public override string ToString()
        {
            return "POLY:[" + (string.Join(", ", edges)) +  "]";
        }
    }
}
