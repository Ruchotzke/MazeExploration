using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Random = UnityEngine.Random;

public class GridMazeGenerator : MonoBehaviour
{
    [Header("Settings")]
    public Bounds Boundary;
    public int Resolution;
    
    [Header("Prefabs")]
    public Waypoint pf_Waypoint;

    private Waypoint[,] grid;

    private void Start()
    {
        GenerateMaze();
    }

    /// <summary>
    /// Generate a maze.
    /// </summary>
    void GenerateMaze()
    {
        /* Step 1: Grid place waypoints. */
        grid = new Waypoint[Resolution, Resolution];
        for (int z = 0; z < Resolution; z++)
        {
            for (int x = 0; x < Resolution; x++)
            {
                var fx = Mathf.Lerp(Boundary.min.x, Boundary.max.x, (float) x / (Resolution - 1));
                var fz = Mathf.Lerp(Boundary.min.z, Boundary.max.z, (float) z / (Resolution - 1));
                
                grid[x, z] = Instantiate(pf_Waypoint, transform);
                grid[x, z].transform.position = new Vector3(fx, 0, fz);
            }
        }
        
        /* Step 2: Use a recursive backtracing method to create a connected graph */
        Stack<Vector2Int> stack = new Stack<Vector2Int>();
        List<Waypoint> closed = new List<Waypoint>();

        // add a seed position
        Vector2Int initial = new Vector2Int(Random.Range(0, Resolution), Random.Range(0, Resolution));
        stack.Push(initial);
        closed.Add(grid[initial.x, initial.y]);
        
        // continue tracing a maze until we have either hit every node or we can no longer backtrace.
        while (stack.Count > 0 && closed.Count < Resolution * Resolution)
        {
            var next = stack.Pop();
            
        }

        /* Initialize the geometry to fit the generated maze */
    }
}
