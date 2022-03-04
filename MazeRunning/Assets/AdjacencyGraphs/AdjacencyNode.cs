using System.Collections.Generic;
using UnityEngine;

namespace Utilities
{
    public class AdjacencyNode
    {
        public Vector3 position;
        public List<AdjacencyNode> Neighbors;
        public List<AdjacencyNode> OpenNeighbors;

        public AdjacencyNode(Vector3 position)
        {
            this.position = position;
            Neighbors = new List<AdjacencyNode>();
            OpenNeighbors = new List<AdjacencyNode>();
        }
    }
}