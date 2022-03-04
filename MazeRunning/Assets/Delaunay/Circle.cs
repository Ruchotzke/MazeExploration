using Unity.Mathematics;

namespace Delaunay.Geometry
{
    /// <summary>
    /// A representation of a triangle (a polygon with
    /// 3 sides).
    /// </summary>
    public class Circle
    {
        public float2 center;
        public float radius;

        private float radiusSquared;

        /// <summary>
        /// Construct a new circle from its center and its radius.
        /// </summary>
        /// <param name="center">The center of the circle.</param>
        /// <param name="radius">The radius of the circle.</param>
        public Circle(float2 center, float radius)
        {
            this.center = center;
            this.radius = radius;
            radiusSquared = radius * radius;
        }

        /// <summary>
        /// Compute whether this point lies on the interior of this circle.
        /// </summary>
        /// <param name="point">The point to compare.</param>
        /// <returns></returns>
        public bool PointLiesInside(float2 point)
        {
            float dx = point.x - center.x;
            float dy = point.y - center.y;

            return (dx * dx + dy * dy) <= radiusSquared;
        }

        public override string ToString()
        {
            return "CIR:[" + center + ", " + radius + "]";
        }
    }
}
