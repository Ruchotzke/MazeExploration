using System;
using Unity.Mathematics;

namespace Delaunay.Geometry
{
    /// <summary>
    /// An edge is a connection between two points.
    /// </summary>
    public class Edge
    {
        public float2 a;
        public float2 b;

        public Edge(float2 a, float2 b)
        {
            this.a = a;
            this.b = b;
        }

        protected bool Equals(Edge other)
        {
            return a.Equals(other.a) && b.Equals(other.b) || a.Equals(other.b) && b.Equals(other.a); //edges can be reversed and still equivalent
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((Edge) obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(a, b);
        }

        public override string ToString()
        {
            return "EDGE:[" + a + ", " + b + "]";
        }
    }
}