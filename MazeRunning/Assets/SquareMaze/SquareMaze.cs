using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeGen
{
    [RequireComponent(typeof(Grid))]
    public class SquareMaze : MonoBehaviour
    {
        [Header("Maze Settings")]
        public Bounds MazeRegion;

        private Grid grid;
        private Vector2Int GridSize;
        private MazeCell[,] Cells;

        private void Awake()
        {
            /* Get components */
            grid = GetComponent<Grid>();
            
            /* Generate a maze */
            GenerateMaze();
        }

        private void OnDrawGizmos()
        {
            if (Cells != null)
            {
                /* just draw a cube for now */
                for (int x = 0; x < GridSize.x; x++)
                {
                    for (int y = 0; y < GridSize.y; y++)
                    {
                        if (Cells[x, y] != null)
                        {
                            Gizmos.color = Color.green;
                            Gizmos.DrawCube(grid.GetCellCenterWorld(new Vector3Int(x, 0, y)), grid.cellSize * 0.9f);
                        }
                    }
                }
            }
        }

        void GenerateMaze()
        {
            /* Ensure the bounds area is an even divisor for the maze */
            float xDiv = MazeRegion.size.x % grid.cellSize.x;
            float yDiv = MazeRegion.size.z % grid.cellSize.z;
            Debug.Log("xDiv: " + xDiv + ", yDiv" + yDiv);
            if (xDiv != 0.0f && yDiv != 0.0f)
            {
                /* Extend both sizes to evenly fit */
                MazeRegion = new Bounds(MazeRegion.center, MazeRegion.size + new Vector3(xDiv, 0, yDiv));
            }
            else if (xDiv != 0.0f)
            {
                /* Extend the X size to evenly fit */
                MazeRegion = new Bounds(MazeRegion.center, MazeRegion.size + new Vector3(xDiv, 0, 0));
            }
            else
            {
                /* Extend the Y size to evenly fit */
                MazeRegion = new Bounds(MazeRegion.center, MazeRegion.size + new Vector3(0, 0, yDiv));
            }
            
            /* First generate a container for the maze cells */
            GridSize = new Vector2Int(Mathf.CeilToInt(MazeRegion.size.x / grid.cellSize.x),
                Mathf.CeilToInt(MazeRegion.size.z / grid.cellSize.z));
            Cells = new MazeCell[GridSize.x, GridSize.y];
            
            /* Sample each cell location - if it is open, we can spawn a maze cell */
            Collider[] dummyRet = new Collider[1];
            for (int x = 0; x < GridSize.x; x++)
            {
                for (int y = 0; y < GridSize.y; y++)
                {
                    /* Percentages */
                    Vector2 amt = new Vector2((float) x / (GridSize.x - 1), (float) y / (GridSize.y - 1));
                    
                    /* Get the cube of this cell */
                    Vector3 min = new Vector3(Mathf.Lerp(MazeRegion.min.x, MazeRegion.max.x, amt.x), 0f,
                        Mathf.Lerp(MazeRegion.min.z, MazeRegion.max.z, amt.y));
                    Vector3 max = min + grid.cellSize;
                    
                    /* Do an overlap check */
                    bool overlap = (Physics.OverlapBoxNonAlloc((min + max) / 2.0f, grid.cellSize / 2, dummyRet) > 0);
                    
                    /* If this area is free, generate a new maze cell */
                    if (!overlap)
                    {
                        Cells[x, y] = new MazeCell(new Vector2Int(x, y));
                    }
                }
            }
            
            /* Use recursive backtracing to generate a maze */
            
            /* Knock out some walls to make the maze more complex */
        }
    }
}