using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Assertions.Must;

namespace Delaunay.Triangulation
{
    public class TriangleMono : MonoBehaviour
    {
        private Triangulator triangulator;
        private Mesh mesh;
        
        private void Awake()
        {
            triangulator = new Triangulator();
            triangulator.AddVertex(new float2(0.0f, 0.0f));
            triangulator.AddVertex(new float2(0.0f, 1.0f));
            triangulator.AddVertex(new float2(1.0f, 1.0f));
            triangulator.AddVertex(new float2(1.0f, 0.0f));
            mesh = triangulator.GenerateTriangulation();
            
            Debug.Log("Mesh generated: " + mesh.Triangles.Count + " triangles used.");
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