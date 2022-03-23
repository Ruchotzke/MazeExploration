using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace Utilities.Meshing
{
    public class Mesher
    {

        public List<int> indices;
        public Dictionary<Vector3, int> vertices;

        public Mesher()
        {
            indices = new List<int>();
            vertices = new Dictionary<Vector3, int>();
        }

        /// <summary>
        /// Add a triangle to this mesh.
        /// Winding order is not guaranteed - it must be provided in the correct order ABC clockwise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        public void AddTriangle(Vector3 a, Vector3 b, Vector3 c)
        {
            if (!vertices.ContainsKey(a)) vertices.Add(a, vertices.Count);
            if (!vertices.ContainsKey(b)) vertices.Add(b, vertices.Count);
            if (!vertices.ContainsKey(c)) vertices.Add(c, vertices.Count);

            indices.Add(vertices[a]);
            indices.Add(vertices[b]);
            indices.Add(vertices[c]);
        }

        /// <summary>
        /// Add a triangle to this mesh. Convert float2s into Vector3s.
        /// Winding order is not guaranteed - it must be provided in the correct order ABC clockwise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        public void AddTriangle(float2 a, float2 b, float2 c)
        {
            AddTriangle(new Vector3(a.x, 0, a.y), new Vector3(b.x, 0, b.y), new Vector3(c.x, 0, c.y));
        }

        /// <summary>
        /// Add a quad to this mesh. The quad will be formed from ABC, CDA.
        /// Winding order is not guaranteed - it must be provided in the correct order ABCD clockwise.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            if (!vertices.ContainsKey(a)) vertices.Add(a, vertices.Count);
            if (!vertices.ContainsKey(b)) vertices.Add(b, vertices.Count);
            if (!vertices.ContainsKey(c)) vertices.Add(c, vertices.Count);
            if (!vertices.ContainsKey(d)) vertices.Add(d, vertices.Count);

            indices.Add(vertices[a]);
            indices.Add(vertices[b]);
            indices.Add(vertices[c]);

            indices.Add(vertices[c]);
            indices.Add(vertices[d]);
            indices.Add(vertices[a]);
        }

        /// <summary>
        /// Construct a mesh from this mesher's current state.
        /// </summary>
        /// <returns></returns>
        public Mesh GenerateMesh()
        {
            /* Generate attribute arrays */
            Vector3[] verticesArray = new Vector3[vertices.Count];
            foreach(var key in vertices.Keys)
            {
                verticesArray[vertices[key]] = key;
            }

            /* Generate the mesh */
            Mesh mesh = new Mesh();
            mesh.SetVertices(verticesArray);
            mesh.SetTriangles(indices.ToArray(), 0);
            
            /* Recalculate attributes */
            mesh.RecalculateBounds();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();

            /* Return the completed mesh */
            return mesh;
        }

    }
}
