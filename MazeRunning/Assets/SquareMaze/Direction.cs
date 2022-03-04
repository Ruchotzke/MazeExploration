using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MazeGen
{
    public enum Direction
    {
        X, Y, NX, NY
    }

    public static class DirectionExtensions
    {
        public static Vector2Int ToVector(this Direction dir)
        {
            switch (dir)
            {
                case Direction.X:
                    return Vector2Int.right;
                case Direction.Y:
                    return Vector2Int.up;
                case Direction.NX:
                    return Vector2Int.left;
                case Direction.NY:
                    return Vector2Int.down;
                default:
                    Debug.LogError("INVALID CASE");
                    return Vector2Int.zero;
            }
        }
    }
}