using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeGen
{
    /// <summary>
    /// A simple abstraction of a single cell in a maze.
    /// </summary>
    public class MazeCell
    {
        public Vector2Int CellPosition;

        public bool[] Walls;

        /// <summary>
        /// Construct a new maze cell.
        /// </summary>
        /// <param name="Cell"></param>
        public MazeCell(Vector2Int Cell)
        {
            CellPosition = Cell;
            Walls = new[] {true, true, true, true};
        }

        /// <summary>
        /// Get the neighbor's cell position in the given direction.
        /// </summary>
        /// <param name="dir"></param>
        /// <returns></returns>
        public Vector2Int GetNeighborPosition(Direction dir)
        {
            return CellPosition + dir.ToVector();
        }
    }
}