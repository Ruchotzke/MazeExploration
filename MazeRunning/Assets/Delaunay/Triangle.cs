using System;
using System.Collections.Generic;
using Unity.Mathematics;


namespace Delaunay.Geometry
{
    /// <summary>
    /// A representation of a triangle (a polygon with
    /// 3 sides).
    /// </summary>
    public class Triangle
    {
        public float2 a;
        public float2 b;
        public float2 c;

        /// <summary>
        /// Construct a new triangle from 3 vertices.
        /// If out of order, they will be put into the correct winding order.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        public Triangle(float2 a, float2 b, float2 c)
        {
            this.a = a;

            float3 a3 = new float3(a.x, a.y, 0.0f);
            float3 b3 = new float3(b.x, b.y, 0.0f);
            float3 c3 = new float3(c.x, c.y, 0.0f);

            float winding = math.cross(b3 - a3, c3 - a3).z;

            if (winding > 0) //flip if you want a different winding order, this gives clockwise ordering
            {
                this.b = c;
                this.c = b;
            }
            else
            {
                this.b = b;
                this.c = c;
            }
        }

        /// <summary>
        /// Calculate whether this point lies within the circumcircle of this triangle.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool PointLiesInInteriorOfCircumcircle(float2 point)
        {
            Circle circumcircle = GetCircumcircle();
            return circumcircle.PointLiesInside(point);
        }

        /// <summary>
        /// Compute the circumcircle for this triangle.
        /// Algorithm Source: https://web.archive.org/web/20071030134248/http://www.exaflop.org/docs/cgafaq/cga1.html#Subject%201.01:%20How%20do%20I%20rotate%20a%202D%20point
        /// </summary>
        /// <returns>The circumcircle for this triangle.</returns>
        public Circle GetCircumcircle()
        {
            float A = b.x - a.x;
            float B = b.y - a.y;
            float C = c.x - a.x;
            float D = c.y - a.y;

            float E = A * (a.x + b.x) + B * (a.y + b.y);
            float F = C * (a.x + c.x) + D * (a.y + c.y);

            float G = 2 * (A * (c.y - b.y) - B * (c.x - b.x));

            if (G == 0)
            {
                /* This is a colinear set of points - a degenerate triangle */
                return null;
            }
            else
            {
                /* We can compute a circumcircle */
                float2 center = new float2((D * E - B * F) / G, (A * F - C * E) / G);
                float radius = (a.x - center.x) * (a.x - center.x) + (a.y - center.y) * (a.y - center.y);
                return new Circle(center, radius);
            }
        }

        /// <summary>
        /// Get the edges of this triangle.
        /// </summary>
        /// <returns></returns>
        public Edge[] GetEdges()
        {
            return new Edge[3] {new Edge(a, b), new Edge(b, c), new Edge(c, a)};
        }

        public override string ToString()
        {
            return "TRI:[" + a + ", " + b + ", " + c + "]";
        }
    }
}
