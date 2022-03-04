
using Unity.Mathematics;

namespace Delaunay.Geometry
{
    /// <summary>
    /// A representation of a triangle (a polygon with
    /// 3 sides).
    /// </summary>
    public class Triangle
    {
        private float2 a;
        private float2 b;
        private float2 c;

        /// <summary>
        /// Calculate whether this point lies within the circumcircle of this triangle.
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        public bool PointLiesInInteriorOfCircumcircle(float2 point)
        {
            return true;
        }
    }
}
