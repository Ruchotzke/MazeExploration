using Delaunay.Geometry;
using Unity.Mathematics;

namespace Delaunay
{
    /// <summary>
    /// A utility responsible for cohen sutherland clipping.
    /// </summary>
    public static class Clipping
    {
        /// <summary>
        /// Use cohen sutherland to clip this edge to a bounding rectangle.
        /// Pseudocode taken from https://en.wikipedia.org/wiki/Cohen%E2%80%93Sutherland_algorithm.
        /// </summary>
        /// <param name="edge">The edge to be clipped</param>
        /// <param name="min">The bottom left corner of the clipping window.</param>
        /// <param name="max">The upper right corner of the clipping window.</param>
        /// <returns>True if the edge was accepted, or false if it was unable to be clipped.</returns>
        public static (bool isVisible, bool editedEdge) ClipEdge(Edge edge, float2 min, float2 max)
        {
            /* Get the two endpoint outcodes */
            byte a = GetOutcode(edge.a, min, max);
            byte b = GetOutcode(edge.b, min, max);
            
            /* Continue to loop until we have a valid edge or decide this edge is helpless */
            bool accept = false;
            bool editedEdge = false;

            while (true)
            {
                /* Trivial accept / deny immediately */
                if ((a | b) == 0b0)
                {
                    /* trivial accept - no endpoint is outside of the window */
                    accept = true;
                    break;
                }
                else if ((a & b) != 0b0)
                {
                    /* trivial deny - both endpoints are outside of the window in the same region.  not visible. */
                    accept = false;
                    break;
                }
                
                /* Clipping, as the line partially overlaps the window */
                float x = float.NaN;
                float y = float.NaN;
                byte outcodeOut = b > a ? b : a;

                if ((outcodeOut & 0b1000) != 0b0000)
                {
                    /* Top OOB */
                    x = edge.a.x + (edge.b.x - edge.a.x) * (max.y - edge.a.y) / (edge.b.y - edge.a.y);
                    y = max.y;
                }
                else if ((outcodeOut & 0b0100) != 0b0000)
                {
                    /* Bottom OOB */
                    x = edge.a.x + (edge.b.x - edge.a.x) * (min.y - edge.a.y) / (edge.b.y - edge.a.y);
                    y = min.y;
                }
                else if ((outcodeOut & 0b0010) != 0b0000)
                {
                    /* Right OOB */
                    y = edge.a.y + (edge.b.y - edge.a.y) * (max.x - edge.a.x) / (edge.b.x - edge.a.x);
                    x = max.x;
                }
                else if ((outcodeOut & 0b0001) != 0b0000)
                {
                    /* left OOB */
                    y = edge.a.y + (edge.b.y - edge.a.y) * (min.x - edge.a.x) / (edge.b.x - edge.a.x);
                    x = min.x;
                }
                
                /* Clipping complete - update variables and continue */
                if (outcodeOut == a)
                {
                    edge.a.x = x;
                    edge.a.y = y;
                    a = GetOutcode(edge.a, min, max);
                }
                else
                {
                    edge.b.x = x;
                    edge.b.y = y;
                    b = GetOutcode(edge.b, min, max);
                }

                editedEdge = true;
            }

            return (accept, editedEdge);
        }

        /// <summary>
        /// Compute the cohen sutherland outcode for this point.
        /// </summary>
        /// <param name="point">The point to find the outcode for.</param>
        /// <param name="min">The bottom left corner of the clipping window.</param>
        /// <param name="max">The upper right corner of the clipping window.</param>
        /// <returns>A byte containing outcode information. 0000UPDOWNRIGHTLEFT</returns>
        public static byte GetOutcode(float2 point, float2 min, float2 max)
        {
            byte outcode = 0;
            if (point.x < min.x)
            {
                outcode |= 0b0001;
            }
            else if (point.x > max.x)
            {
                outcode |= 0b0010;
            }

            if (point.y < min.y)
            {
                outcode |= 0b0100;
            }
            else if(point.y > max.y)
            {
                outcode |= 0b1000;
            }

            return outcode;
        }
    }
}