using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Networking;
using Utilities;

public class IrregularMazeGenerator : MonoBehaviour
{
    [Header("Settings")]
    public Bounds Boundary;
    public float MinDistance = 1.0f;
    public float ConnectionDistance = 1.5f;
    
    [Header("Prefabs")]
    public Waypoint pf_Waypoint;

    [Header("Draw Settings")] 
    public bool DrawBaseGrid = false;
    public bool DrawMazeGrid = true;

    private List<AdjacencyNode> samples;
    private Dictionary<AdjacencyNode, List<(AdjacencyNode a, AdjacencyNode b)>> edges;

    private void Start()
    {
        /* Perform a sampling operation */
        PoissonSampler sampler =
            new PoissonSampler(new Rect(Boundary.min.x, Boundary.min.z, Boundary.size.x, Boundary.size.z), MinDistance);

        samples = sampler.SampleWithAdjacency(Vector2.zero, ConnectionDistance);

        Debug.Log("Starting overlap detection.");
        int overlapCount = 0;
        edges = new Dictionary<AdjacencyNode, List<(AdjacencyNode a, AdjacencyNode b)>>();
        foreach (var sample in samples)
        {
            edges.Add(sample, new List<(AdjacencyNode a, AdjacencyNode b)>());
            List<AdjacencyNode> toRemove = new List<AdjacencyNode>();
            foreach (var neighbor in sample.Neighbors)
            {
                /* check to see if this edge overlaps with any other edges. if it does, ignore it */
                bool overlapFound = false;
                foreach (var list in edges.Values)
                {
                    foreach (var line in list)
                    {
                        /* First, is this line the direct inverse? If so, we want to skip it */
                        if (line.a == sample || line.a == neighbor)
                        {
                            continue;
                        }
                        
                        Vector2 a = new Vector2(sample.position.x, sample.position.z);
                        Vector2 b = new Vector2(neighbor.position.x, neighbor.position.z);
                        Vector2 c = new Vector2(line.a.position.x, line.a.position.z);
                        Vector2 d = new Vector2(line.b.position.x, line.b.position.z);
                        
                        overlapFound = Utilities.Utilities.IsLineIntersecting(a, b, c, d, false);

                        if (overlapFound)
                        {
                            overlapCount++;
                            toRemove.Add(neighbor);
                            break;
                        }
                    }

                    if (overlapFound) break;
                }
                
                if(!overlapFound) edges[sample].Add((sample, neighbor));
            }
            
            /* Remove any overlapping neighbors */
            foreach (var neighbor in toRemove)
            {
                sample.Neighbors.Remove(neighbor);
                neighbor.Neighbors.Remove(sample);
            }
        }
        Debug.Log("Stopping overlap detection. " + overlapCount + " edges overlapping removed.");

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
            Utilities.Utilities.Shuffle(curr.Neighbors);
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
                if (DrawBaseGrid)
                {
                    Gizmos.color = Color.green;
                    foreach (var neighbor in sample.Neighbors)
                    {
                        Gizmos.DrawLine(sample.position, neighbor.position);
                    }
                }

                /* Draw the path */
                if (DrawMazeGrid)
                {
                    Gizmos.color = Color.red;
                    foreach (var neighbor in sample.OpenNeighbors)
                    {
                        Gizmos.DrawLine(sample.position, neighbor.position);
                    }
                }
                
            }
        }
    }
}
