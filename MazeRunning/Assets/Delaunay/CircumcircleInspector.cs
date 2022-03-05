using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Delaunay.Geometry
{
    public class CircumcircleInspector : MonoBehaviour
    {
        public float2 A = float2.zero;
        public float2 B = new float2(1.0f, 0.0f);
        public float2 C = new float2(0.0f, 1.0f);

        private void OnDrawGizmos()
        {
            if (Application.isPlaying)
            {
                /* Draw the triangle */
                Gizmos.color = Color.red;
                Gizmos.DrawLine(new Vector3(A.x, 0, A.y), new Vector3(B.x, 0, B.y));
                Gizmos.DrawLine(new Vector3(B.x, 0, B.y), new Vector3(C.x, 0, C.y));
                Gizmos.DrawLine(new Vector3(C.x, 0, C.y), new Vector3(A.x, 0, A.y));
                
                /* Draw the circumcircle */
                Triangle tri = new Triangle(A, B, C);
                Circle circum = tri.GetCircumcircle();
                Gizmos.color = new Color(0.0f, 1.0f, 0.0f, 0.2f);
                DrawGizmoCircle(circum);
            }
        }

        private void DrawGizmoCircle(Circle circle)
        {
            const int segments = 30;
            const float deltaAngle = Mathf.PI * 2 / segments;

            /* First segment */
            Vector3 prev = new Vector3(1.0f, 0.0f, 0.0f) * circle.radius + new Vector3(circle.center.x, 0, circle.center.y);

            /* Next segments */
            for (int i = 1; i < segments; i++)
            {
                /* angle */
                float angle = i * deltaAngle;
                Vector3 next = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * circle.radius + new Vector3(circle.center.x, 0, circle.center.y);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
            
            /* Final Segment */
            Gizmos.DrawLine(prev, new Vector3(1.0f, 0.0f, 0.0f) * circle.radius + new Vector3(circle.center.x, 0, circle.center.y));
        }
    }
}

