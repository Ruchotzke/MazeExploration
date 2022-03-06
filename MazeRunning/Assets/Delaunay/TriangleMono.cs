using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions.Must;
using Random = UnityEngine.Random;

namespace Delaunay.Triangulation
{
    public class TriangleMono : MonoBehaviour
    {
        private Triangulator triangulator;
        private Mesh mesh;
        
        private void Awake()
        {
            triangulator = new Triangulator();

            for (int i = 0; i < 10; i++)
            {
                triangulator.AddVertex(new float2(Random.Range(0f, 10f), Random.Range(0f, 10f)));
            }

            // triangulator.AddVertex(new float2(0, 0));
            // triangulator.AddVertex(new float2(0, 1));
            // triangulator.AddVertex(new float2(1, 0));
            // triangulator.AddVertex(new float2(1, 1));
            
            mesh = triangulator.GenerateTriangulation();
            
            Debug.Log("Mesh generated: " + mesh.Triangles.Count + " triangles used for " + triangulator.vertices.Count + " points.");
        }

        private void OnDrawGizmos()
        {
            if (mesh != null)
            {
                /* Draw the points */
                Gizmos.color = Color.red;
                foreach (var point in triangulator.vertices)
                {
                    Gizmos.DrawSphere(new Vector3(point.x, 0, point.y), 0.1f);
                }
                
                /* Draw the triangulation */
                Gizmos.color = Color.green;
                foreach (var tri in mesh.Triangles)
                {
                    Gizmos.DrawLine(new Vector3(tri.a.x, 0, tri.a.y), new Vector3(tri.b.x, 0, tri.b.y));
                    Gizmos.DrawLine(new Vector3(tri.b.x, 0, tri.b.y), new Vector3(tri.c.x, 0, tri.c.y));
                    Gizmos.DrawLine(new Vector3(tri.c.x, 0, tri.c.y), new Vector3(tri.a.x, 0, tri.a.y));
                }
            }
        }
    }
}