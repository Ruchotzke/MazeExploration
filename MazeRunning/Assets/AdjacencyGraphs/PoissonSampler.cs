

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Assertions.Must;
using Random = UnityEngine.Random;

namespace Utilities
{
    public class PoissonSampler
    {
        private Rect area;
        private float distance;
        private float sqrDistance;
        private int sampleLimit;

        private int[,] sampleGrid;
        private List<Vector2> samples;
        private float cellSize;
        private Vector2Int gridSize;

        /// <summary>
        /// Construct a new poisson sampler.
        /// </summary>
        /// <param name="area"></param>
        /// <param name="distance"></param>
        /// <param name="sampleLimit"></param>
        public PoissonSampler(Rect area, float distance, int sampleLimit = 30)
        {
            this.area = area;
            this.distance = distance;
            sqrDistance = distance * distance;
            this.sampleLimit = sampleLimit;
        }

        /// <summary>
        /// Perform a poisson sample with a random seed.
        /// </summary>
        /// <returns></returns>
        public List<Vector2> Sample()
        {
            return Sample(new Vector2(Random.Range(area.xMin, area.xMax), Random.Range(area.yMin, area.yMax)));
        }

        /// <summary>
        /// Perform a poisson sample operation.
        /// </summary>
        /// <param name="seed"></param>
        /// <returns></returns>
        public List<Vector2> Sample(Vector2 seed)
        {
            /* Initialize the grid */
            cellSize = distance / Mathf.Sqrt(2);
            gridSize = Vector2Int.CeilToInt(area.size / new Vector2(cellSize, cellSize));
            sampleGrid = new int[gridSize.x, gridSize.y];
            for (int x = 0; x < gridSize.x; x++)
            {
                for (int y = 0; y < gridSize.y; y++)
                {
                    sampleGrid[x, y] = -1;
                }
            }

            /* Start a list */
            samples = new List<Vector2>();
            List<int> active = new List<int>();

            /* Add the initial sample */
            samples.Add(seed);
            Vector2Int seedPos = WorldToCell(seed);
            sampleGrid[seedPos.x, seedPos.y] = 0;
            active.Add(0);

            /* Continue to sample the grid */
            while (active.Count > 0)
            {
                /* Select a random entry */
                int sourceIndex = Random.Range(0, active.Count);
                Vector2 prevSample = samples[active[sourceIndex]];

                /* Sample until we find a valid sample (or run out of attempts) */
                bool foundSample = false;
                for (int i = 0; i < sampleLimit; i++)
                {
                    /* Generate a random point using angle and magnitude */
                    float angle = Random.value * Mathf.PI * 2;
                    float r = Random.Range(distance, 2 * distance);
                    Vector2 candidate = prevSample + r * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                    /* Check if this is a valid sample */
                    if (area.Contains(candidate) && CheckSample(candidate))
                    {
                        /* We found a sample */
                        foundSample = true;

                        /* update active, samples, and the grid bitmap */
                        samples.Add(candidate);
                        active.Add(samples.Count - 1);
                        Vector2Int gridPos = WorldToCell(candidate);
                        sampleGrid[gridPos.x, gridPos.y] = samples.Count - 1;

                        /* no more sampling from this point */
                        break;
                    }
                }

                /* if we didn't find a sample, this point is dead */
                if (!foundSample) active.RemoveAt(sourceIndex);
            }

            /* Return the result */
            return samples;
        }

        /// <summary>
        /// Perform a poisson sample with a random seed.
        /// Also return the grid to help with adjacency building.
        /// </summary>
        /// <returns></returns>
        public (List<Vector2> samples, int[,] cells) SampleWithCells()
        {
            List<Vector2> vector2s = Sample();
            return (vector2s, sampleGrid);
        }

        /// <summary>
        /// Perform a poisson sample.
        /// Also return the grid to help with adjacency building.
        /// </summary>
        /// <returns></returns>
        public (List<Vector2> samples, int[,] cells) SampleWithCells(Vector2 seed)
        {
            List<Vector2> vector2s = Sample(seed);
            return (vector2s, sampleGrid);
        }

        public List<AdjacencyNode> SampleWithAdjacency(Vector2 seed, float connectionRadius)
        {
            /* First perform a normal sample */
            Sample(seed);

            /* Create infrastructure */
            List<AdjacencyNode> nodes = new List<AdjacencyNode>();
            Dictionary<Vector2, AdjacencyNode> translator = new Dictionary<Vector2, AdjacencyNode>();

            /* Perform the adjacency searches */
            int deltaRadius = Mathf.CeilToInt(connectionRadius / cellSize);
            float sqrRadius = connectionRadius * connectionRadius;
            foreach (var sample in samples)
            {
                /* Start by either making or retrieving the node for this sample */
                if (!translator.ContainsKey(sample))
                {
                    translator.Add(sample, new AdjacencyNode(new Vector3(sample.x, 0, sample.y)));
                    nodes.Add(translator[sample]);
                }

                AdjacencyNode currNode = translator[sample];

                /* Sample the neighboring grid cells to attempt to find neighbors */
                Vector2Int gridSample = WorldToCell(sample);
                for (int x = Mathf.Max(gridSample.x - deltaRadius, 0);
                     x < Mathf.Min(gridSample.x + deltaRadius, gridSize.x);
                     x++)
                {
                    for (int y = Mathf.Max(gridSample.y - deltaRadius, 0);
                         y < Mathf.Min(gridSample.y + deltaRadius, gridSize.y);
                         y++)
                    {
                        if (sampleGrid[x, y] > 0)
                        {
                            Vector2 other = samples[sampleGrid[x, y]];
                            Vector2 diff = other - sample;
                            if (diff != Vector2.zero && diff.x * diff.x + diff.y * diff.y < sqrRadius)
                            {
                                /* This is a valid connection */
                                /* Connect this node to this neighbor */
                                /* Generate a new adjacency node if necessary */
                                if (!translator.ContainsKey(other))
                                {
                                    translator.Add(other, new AdjacencyNode(new Vector3(other.x, 0, other.y)));
                                    nodes.Add(translator[other]);
                                }

                                currNode.Neighbors.Add(translator[other]);
                            }
                        }
                    }
                }
            }

            /* We can now return our nodes */
            return nodes;
        }

        /// <summary>
        /// Check a sample to make sure it is in the correct range of
        /// other samples.
        /// </summary>
        /// <param name="sample"></param>
        /// <returns></returns>
        bool CheckSample(Vector2 sample)
        {
            /* Get the grid cell this point lies within */
            Vector2Int gridPos = WorldToCell(sample);

            /* Generate a rectangle of cells to check */
            int xmin = Mathf.Max(gridPos.x - 2, 0);
            int ymin = Mathf.Max(gridPos.y - 2, 0);
            int xmax = Mathf.Min(gridPos.x + 2, gridSize.x - 1);
            int ymax = Mathf.Min(gridPos.y + 2, gridSize.y - 1);

            /* Check for close neighbors */
            for (int y = ymin; y <= ymax; y++)
            {
                for (int x = xmin; x <= xmax; x++)
                {
                    int index = sampleGrid[x, y];
                    if (index >= 0)
                    {
                        Vector2 diff = sample - samples[index];
                        if (diff.x * diff.x + diff.y * diff.y < sqrDistance) return false;
                    }
                }
            }

            /* If we aren't too close to any neighbors, we are fine */
            return true;
        }

        /// <summary>
        /// Convert a real space coordinate into a grid space coordinate.
        /// </summary>
        /// <param name="pos"></param>
        /// <returns></returns>
        private Vector2Int WorldToCell(Vector2 pos)
        {
            return Vector2Int.FloorToInt((pos - area.min) / cellSize);
        }


    }
}