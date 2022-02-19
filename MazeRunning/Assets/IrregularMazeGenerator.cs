using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Utilities;

public class IrregularMazeGenerator : MonoBehaviour
{
    [Header("Settings")]
    public Bounds Boundary;
    public float MinDistance = 1.0f;
    public float ConnectionDistance = 1.5f;
    
    [Header("Prefabs")]
    public Waypoint pf_Waypoint;

    private List<AdjacencyNode> samples;

    private void Start()
    {
        /* Perform a sampling operation */
        PoissonSampler sampler =
            new PoissonSampler(new Rect(Boundary.min.x, Boundary.min.z, Boundary.size.x, Boundary.size.z), MinDistance);

        samples = sampler.SampleWithAdjacency(Vector2.zero, ConnectionDistance);
        
        /* Do the backtrace */
        DoBacktrace(samples[0]);
    }

    /// <summary>
    /// Perform a backtracing algorithm to generate a random walk through
    /// all nodes (only once per node). No cycles - perfect maze.
    /// </summary>
    /// <param name="source"></param>
    private void DoBacktrace(AdjacencyNode source)
    {
        /* setup infrastructure */
        var translator = new Dictionary<AdjacencyNode, bool>();
        Stack<AdjacencyNode> stack = new Stack<AdjacencyNode>();

        /* set up first node */
        translator.Add(source, true);
        stack.Push(source);
        
        /* perform the backtrace algorithm */
        while (stack.Count > 0)
        {
            /* Get the current endpoint */
            var curr = stack.Peek();
            
            /* explore neighbors */
            bool movedToNeighbor = false;
            foreach (var neighbor in curr.Neighbors)
            {
                /* If this node isn't in the translator, it's not visited. */
                if (!translator.ContainsKey(neighbor))
                {
                    translator.Add(neighbor, false);
                    stack.Push(neighbor);
                    movedToNeighbor = true;
                    break;
                }
                else
                {
                    /* This node was already visited */
                    continue;
                }
            }
            
            /* If we didn't move to a neighbor, remove this node from the stack (dead end) */
            if (!movedToNeighbor)
            {
                stack.Pop();
            }
            else
            {
                /* We moved to a neighbor - mark that movement as a valid path */
                var endPoint = stack.Peek();
                curr.OpenNeighbors.Add(endPoint);
                curr.Neighbors.Remove(endPoint);
                endPoint.Neighbors.Remove(curr);
                endPoint.OpenNeighbors.Add(curr);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (samples != null)
        {
            /* Draw a sphere for each sample */
            foreach (var sample in samples)
            {
                /* Draw this sample */
                Gizmos.color = Color.red;
                Gizmos.DrawSphere(sample.position, 0.1f);
                
                /* Draw the connections */
                // Gizmos.color = Color.green;
                // foreach (var neighbor in sample.Neighbors)
                // {
                //     Gizmos.DrawLine(sample.position, neighbor.position);
                // }
                
                /* Draw the path */
                Gizmos.color = Color.red;
                foreach (var neighbor in sample.OpenNeighbors)
                {
                    Gizmos.DrawLine(sample.position, neighbor.position);
                }
            }
        }
    }
}
