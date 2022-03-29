using System.Collections.Generic;
using Delaunay.Geometry;
using Delaunay.Triangulation;
using Unity.Mathematics;
using UnityEngine;
using Utilities;
using Utilities.Meshing;
using Mesh = Delaunay.Triangulation.Mesh;
using Random = UnityEngine.Random;
using Unity.AI.Navigation;
using MazeRunning.Utilities;

namespace MazeRunning.MazeGen
{
    public class IrregularMazeGenerator : MonoBehaviour
    {
        [Header("Maze Settings")]
        public int Seed;                        /* The random seed used for this maze */
        public Bounds Boundary;                 /* The general rectangular region to shape the maze */
        public float MinDistance = 1.0f;        /* The minimum distance between cells */
        public float MinOpenWallLength = 1.0f;  /* The minimum length a wall must be to be considered for an opening */
        public float WallHeight = 0.5f;         /* The height of the generated walls */
        [Range(0.01f, 1.0f)]
        public float BorderThickness = 0.1f;    /* The percentage of the original cells which should become border */
        [Range(0f, 1f)]
        public float WallRemovePercentage;      /* The percentage of the total walls which should be removed */

        [Header("Chunk Settings")]
        public Vector3 ChunkSize;               /* The size of a chunk in the final triangulation */
        public NavMeshSurface ChunkContainer;   /* The object all chunks should be instantiated under */

        [Header("Render Settings")]
        public bool TriangulateFloor = true;    /* Should the floor of this maze be triangulated? */
        public Material WallMaterial;
        public Material FloorMaterial;

        [Header("Debug Settings")]
        public bool DrawSites = true;
        public bool DrawBaseGrid = true;
        public bool DrawMazeGrid = true;
        public bool DrawVoronoi = true;
        public bool ProfileMazeGeneration = false;

        /* Triangulation Globals */
        int edgeCount = 0;                                                  /* The count of the total number of the edges in the maze, roughly doubled */
        private List<AdjacencyNode> samples;                                /* All adjacency nodes */
        private Dictionary<Edge, List<AdjacencyNode>> edgeToNodeBorder;     /* Convert an edge to a list of adjacency nodes (useful for adjacency graphing) */
        private Dictionary<Polygon, AdjacencyNode> polyToNode;              /* Convert a polygon to it's associated adjacency node */
        private Dictionary<AdjacencyNode, Polygon> nodeToPoly;              /* Convert an adjacency node to its associated polygon */
        private Dictionary<Polygon, List<Edge>> edgePolygons;               /* A dictionary containing edge polygons (and the edges which are edges) */
        private Dictionary<Polygon, Polygon> scaledPolygons;                /* The scaled versions of polygons */
        private HashSet<float2> innerTriangles;                             /* The set of all inner triangles which have been triangulated */
        private List<Polygon> centerPolygons;                               /* The polygons which were removed from the colliders, useful to triangulate later */

        /* Voronoi Globals */
        private Mesh generatedMesh;
        private List<(float2 site, Polygon polygon)> generatedVoronoi;

        /* Chunking Globals */
        Vector2Int NumChunks;                                               /* The number of chunks present in the final triangulation. (x,y) */
        MeshFilter[,] WallFilters;                                          /* The mesh filters used to display the final chunk walls */
        MeshFilter[,] FloorFilters;                                         /* The mesh filters used to display the final chunk flooring */
        MeshCollider[,] WallColliders;                                      /* The mesh colliders used on the walls */
        MeshCollider[,] FloorColliders;                                     /* The mesh colliders used on the floors */
        List<AdjacencyNode>[,] ChunkNodes;                                  /* The nodes sorted into their respective chunk's responsibility based on position */

        private void Start()
        {
            Random.InitState(Seed);
            GenerateMaze();
        }

        private void GenerateMaze()
        {
            System.Diagnostics.Stopwatch watch = new System.Diagnostics.Stopwatch();
            double totalRuntime = 0.0f;

            /* First sample the region where the maze will lie */
            if(ProfileMazeGeneration) watch.Start();
                    PoissonSampler sampler = new PoissonSampler(new Rect(Boundary.min.x, Boundary.min.z, Boundary.size.x, Boundary.size.z), MinDistance);
                    List<Vector2> points = sampler.Sample();
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Poisson Sampling complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }

            /* Create a triangulation of the region */
            if (ProfileMazeGeneration) watch.Start();
                    Triangulator triangulator = new Triangulator();
                    foreach (var vertex in points)
                    {
                        triangulator.AddVertex(vertex);
                    }
                    generatedMesh = triangulator.GenerateTriangulation();
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Delaunay complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }

            /* Create a voronoi diagram to help in mesh construction */
            if (ProfileMazeGeneration) watch.Start();
                    generatedVoronoi = generatedMesh.GenerateDualGraph(new float2(Boundary.min.x, Boundary.min.z), new float2(Boundary.max.x, Boundary.max.z));
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Voronoi complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }

            /* Build up an adjacency graph */
            if (ProfileMazeGeneration) watch.Start();
                    GenerateAdjacencyGraph(MinOpenWallLength);
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Adjacency generation complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }
            

            /* Backtrace over the graph to generate a maze */
            if (ProfileMazeGeneration) watch.Start();
                    DoBacktrace(samples[0]);
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Backtrace complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }

            /* We now have a maze layout picked - we now need to generate a mesh to hold this maze */
            if (ProfileMazeGeneration) watch.Start();
                    InitializeTriangulation();
                    for (int x = 0; x < NumChunks.x; x++)
                    {
                        for (int y = 0; y < NumChunks.y; y++)
                        {
                            TriangulateMazeChunk(x, y);
                        }
                    }
                    Cleanup();
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Triangulation Complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }

            /* The center needs to have a floor to contain the hub - triangulate it */
            TriangulateCenter();

            /* Finally, we need to generate the navmesh to allow for navigation through the maze */
            if (ProfileMazeGeneration) watch.Start();
                    ChunkContainer.BuildNavMesh();
            if (ProfileMazeGeneration)
            {
                watch.Stop();
                totalRuntime += watch.ElapsedMilliseconds;
                Debug.Log("Navigation complete in " + watch.ElapsedMilliseconds + " ms.");
                watch.Reset();
            }

            /* Report the final time. */
            if (ProfileMazeGeneration) Debug.Log("Complete Generation: Complete in " + totalRuntime + " ms.");
        }

        /// <summary>
        /// Triangulate the central region which contains the removed polygons.
        /// Useful for a hub region.
        /// </summary>
        private void TriangulateCenter()
        {
            /* Create a central floor mesh object */
            GameObject centerObj = new GameObject("Center Floor");
            centerObj.transform.parent = ChunkContainer.transform;
            var filter = centerObj.AddComponent<MeshFilter>();
            centerObj.AddComponent<MeshRenderer>().material = FloorMaterial;
            var collider = centerObj.AddComponent<MeshCollider>();

            /* Mesh the floor */
            Mesher mesher = new Mesher();
            foreach(var poly in centerPolygons)
            {
                Vector3 root = new Vector3(poly.vertices[0].x, 0, poly.vertices[0].y);
                for(int i = 1; i < poly.vertices.Count - 1; i++)
                {
                    mesher.AddTriangle(root, new Vector3(poly.vertices[i].x, 0, poly.vertices[i].y), new Vector3(poly.vertices[i+1].x, 0, poly.vertices[i+1].y));
                }
            }

            /* Finish the mesh off */
            var mesh = mesher.GenerateMesh();
            mesh.name = "Central Floor";
            filter.sharedMesh = mesh;
            collider.sharedMesh = mesh;
        }

        /// <summary>
        /// Initialize variables which will be useful for triangulation globally.
        /// </summary>
        private void InitializeTriangulation()
        {
            /* Create necessary containers */
            scaledPolygons = new Dictionary<Polygon, Polygon>();
            innerTriangles = new HashSet<float2>();
            float2 scaleFactor = new float2(1.0f - BorderThickness, 1.0f - BorderThickness);

            /* Create information for chunks to be generated later */
            NumChunks = Vector2Int.CeilToInt(new Vector2(Boundary.size.x, Boundary.size.z) / new Vector2(ChunkSize.x, ChunkSize.z));
            FloorFilters = new MeshFilter[NumChunks.x, NumChunks.y];
            WallFilters = new MeshFilter[NumChunks.x, NumChunks.y];
            FloorColliders = new MeshCollider[NumChunks.x, NumChunks.y];
            WallColliders = new MeshCollider[NumChunks.x, NumChunks.y];
            ChunkNodes = new List<AdjacencyNode>[NumChunks.x, NumChunks.y];

            /* Create chunk containers */
            for (int x = 0; x < NumChunks.x; x++)
            {
                for (int y = 0; y < NumChunks.y; y++)
                {
                    /* Create a list to contain this chunk's polygons */
                    ChunkNodes[x, y] = new List<AdjacencyNode>();

                    /* Create a new container for this chunk's information */
                    GameObject container = new GameObject("Chunk (" + x + ", " + y + ")");
                    container.transform.parent = ChunkContainer.transform;

                    /* Create 2 mesh renderer objects as children of this container for rendering chunks */
                    /* Also add mesh colliders to each */
                    if (TriangulateFloor)
                    {
                        GameObject floorRenderer = new GameObject("Floor Renderer");
                        floorRenderer.transform.parent = container.transform;
                        FloorFilters[x, y] = floorRenderer.AddComponent<MeshFilter>();
                        floorRenderer.AddComponent<MeshRenderer>().material = FloorMaterial;
                        FloorColliders[x, y] = floorRenderer.AddComponent<MeshCollider>();
                    }

                    GameObject wallRenderer = new GameObject("Wall Renderer");
                    wallRenderer.transform.parent = container.transform;
                    WallFilters[x, y] = wallRenderer.AddComponent<MeshFilter>();
                    wallRenderer.AddComponent<MeshRenderer>().material = WallMaterial;
                    WallColliders[x, y] = wallRenderer.AddComponent<MeshCollider>();
                }
            }

            /* Scale all polygons */
            /* Also sort polygons while doing this */
            foreach (var node in samples)
            {
                var polygon = nodeToPoly[node];

                /* Scale the polygon down by the given amount */
                var shrunkPolygon = polygon.ScalePolygon(scaleFactor);
                scaledPolygons.Add(polygon, shrunkPolygon);

                /* Sort this polygon into the correct chunk */
                Vector2Int chunk = Vector2Int.FloorToInt(new Vector2((node.position.x - Boundary.min.x) / ChunkSize.x, (node.position.z - Boundary.min.z) / ChunkSize.z));
                ChunkNodes[chunk.x, chunk.y].Add(node);
            }
        }

        /// <summary>
        /// Cleanup and free memory associated with the triangulation and generation
        /// of this maze.
        /// </summary>
        private void Cleanup()
        {

        }

        /// <summary>
        /// Triangulate a mesh from its voronoi polygons.
        /// </summary>
        /// <returns></returns>
        private void TriangulateMazeChunk(int chunkX, int chunkY)
        {
            Mesher floorMesher = new Mesher();
            Mesher wallMesher = new Mesher();
            Vector3 wallOffset = new Vector3(0f, WallHeight, 0f);

            /* Triangulate each polygon based on its neighbors */
            foreach (var node in ChunkNodes[chunkX, chunkY])
            {
                /* Before anything, if this node is OOB of our area, don't triangulate it */
                //if (!areaToTriangulate.Contains(node.position)) continue;

                var polygon = nodeToPoly[node];

                /* Scale the polygon down by the given amount */
                var shrunkPolygon = scaledPolygons[polygon];

                /* Triangulate the floor of the polygon */
                if (TriangulateFloor)
                {
                    for (int i = 1; i < shrunkPolygon.vertices.Count - 1; i++)
                    {
                        /* Triangle fan */
                        var a = shrunkPolygon.vertices[0];
                        var b = shrunkPolygon.vertices[i];
                        var c = shrunkPolygon.vertices[i + 1];

                        floorMesher.AddTriangle(a, b, c);
                    }
                }

                /* Triangulate the walls of the polygon */
                var originalEdges = polygon.GetEdges();
                var shrunkEdges = shrunkPolygon.GetEdges();
                for (int i = 0; i < shrunkEdges.Count; i++)
                {
                    /* Helper variables */
                    Edge edge = shrunkEdges[i];
                    Edge originalEdge = originalEdges[i];
                    Vector3 a = new Vector3(edge.a.x, 0, edge.a.y);
                    Vector3 b = new Vector3(edge.b.x, 0, edge.b.y);
                    Vector3 oa = new Vector3(originalEdge.a.x, 0, originalEdge.a.y);
                    Vector3 ob = new Vector3(originalEdge.b.x, 0, originalEdge.b.y);

                    /* Only triangulate this wall if it is not open */
                    AdjacencyNode opposition = null; /* Will always be assigned if needed */
                    var borderNodes = edgeToNodeBorder[originalEdge];
                    bool isOpenEdge = false;
                    if (borderNodes.Count >= 2)
                    {
                        /* Get a reference to the opposing adjacency node */
                        opposition = edgeToNodeBorder[originalEdge][0] == node ? edgeToNodeBorder[originalEdge][1] : edgeToNodeBorder[originalEdge][0];
                        if (node.OpenNeighbors.Contains(opposition))
                        {
                            /* Open node found */
                            isOpenEdge = true;
                        }
                    }

                    if (!isOpenEdge)
                    {
                        /* Triangulate the inner portion of the wall */
                        wallMesher.AddQuad(b, a, a + wallOffset, b + wallOffset);

                        /* We only need to triangulate the rest if this edge is shared and is +z or z=0 and +x */
                        if (opposition == null)
                        {
                            /* Triangulate the top of the wall, it's a border so we are always responsible */
                            wallMesher.AddQuad(b + wallOffset, a + wallOffset, oa + wallOffset, ob + wallOffset);   /* Top of wall */
                            wallMesher.AddQuad(oa, ob, ob + wallOffset, oa + wallOffset);                           /* Opposite wall */
                        }
                        else
                        {
                            /* This wall is shared - only triangulate if we are responsible */
                            var deltaPos = opposition.position - node.position;
                            if (deltaPos.z > 0 || deltaPos.z == 0 && deltaPos.x > 0)
                            {
                                /* Triangulate the top of this wall all the way across */
                                /* Start by finding the matching indices for the two polygons to get positions */
                                int aMatchIndex = -1, bMatchIndex = -1;
                                var otherPoly = nodeToPoly[opposition];
                                for (int index = 0; index < otherPoly.vertices.Count; index++)
                                {
                                    if (otherPoly.vertices[index].Equals(originalEdge.a)) aMatchIndex = index;
                                    if (otherPoly.vertices[index].Equals(originalEdge.b)) bMatchIndex = index;

                                    if (aMatchIndex >= 0 && bMatchIndex >= 0) break;
                                }

                                /* Triangulate the top of the wall */
                                float2 oppA = scaledPolygons[otherPoly].vertices[aMatchIndex];
                                float2 oppB = scaledPolygons[otherPoly].vertices[bMatchIndex];
                                Vector3 oppAV = new Vector3(oppA.x, 0, oppA.y);
                                Vector3 oppBV = new Vector3(oppB.x, 0, oppB.y);
                                wallMesher.AddQuad(b + wallOffset, a + wallOffset, oppAV + wallOffset, oppBV + wallOffset);

                                /* We might also need to triangulate one or two inner triangles. Perform a shared edge test */
                                /* Triangle holes are formed by 3 closed edges in a triangle. Detect this condition */
                                List<AdjacencyNode> sharedNodes = new List<AdjacencyNode>();
                                foreach (var neighbor in node.Neighbors)
                                {
                                    if (!node.OpenNeighbors.Contains(neighbor))
                                    {
                                        if (opposition.Neighbors.Contains(neighbor))
                                        {
                                            if (!opposition.OpenNeighbors.Contains(neighbor))
                                            {
                                                /* This must be a shared node */
                                                sharedNodes.Add(neighbor);
                                            }
                                        }
                                    }
                                }

                                /* For each shared node, we will potentially need to triangulate an inner triangle. */
                                /* Detect the shared vertex for each shared triangle */
                                if (sharedNodes.Count > 0)
                                {
                                    Polygon oppositionPolygon = nodeToPoly[opposition];
                                    foreach (var shared in sharedNodes)
                                    {
                                        Polygon sharedPolygon = nodeToPoly[shared];
                                        bool completedPolygon = false;

                                        /* Find the shared vertex between the three polygons. AT MOST ONE! */
                                        for (int nodeIndex = 0; nodeIndex < polygon.vertices.Count && !completedPolygon; nodeIndex++)
                                        {
                                            float2 nodeVertex = polygon.vertices[nodeIndex];
                                            for (int oppIndex = 0; oppIndex < oppositionPolygon.vertices.Count && !completedPolygon; oppIndex++)
                                            {
                                                float2 oppVertex = oppositionPolygon.vertices[oppIndex];
                                                for (int sharedIndex = 0; sharedIndex < sharedPolygon.vertices.Count && !completedPolygon; sharedIndex++)
                                                {
                                                    float2 sharedVertex = sharedPolygon.vertices[sharedIndex];
                                                    if (sharedVertex.Equals(oppVertex) && sharedVertex.Equals(nodeVertex))
                                                    {
                                                        /* We found the vertex. Now check if this hole has already been filled */
                                                        if (!innerTriangles.Contains(sharedVertex))
                                                        {
                                                            /* This hole is empty. Triangulate it */
                                                            innerTriangles.Add(sharedVertex);
                                                            Vector3 nodePos = new Vector3(scaledPolygons[polygon].vertices[nodeIndex].x, 0, scaledPolygons[polygon].vertices[nodeIndex].y) + wallOffset;
                                                            Vector3 oppPos = new Vector3(scaledPolygons[oppositionPolygon].vertices[oppIndex].x, 0, scaledPolygons[oppositionPolygon].vertices[oppIndex].y) + wallOffset;
                                                            Vector3 sharedPos = new Vector3(scaledPolygons[sharedPolygon].vertices[sharedIndex].x, 0, scaledPolygons[sharedPolygon].vertices[sharedIndex].y) + wallOffset;

                                                            /* Check winding order to ensure the normal faces upwards */
                                                            bool windingOrder = Vector3.Cross(nodePos - sharedPos, oppPos - sharedPos).y < 0;

                                                            if (windingOrder)
                                                            {
                                                                wallMesher.AddTriangle(nodePos, sharedPos, oppPos);
                                                            }
                                                            else
                                                            {
                                                                wallMesher.AddTriangle(nodePos, oppPos, sharedPos);
                                                            }

                                                        }

                                                        /* No matter what, these polygons are covered. Move onto the next polygon */
                                                        completedPolygon = true;
                                                        break;
                                                    }
                                                }
                                            }
                                        }

                                        /* This polygon is done. If it wasn't for some reason, report an error */
                                        if (!completedPolygon) Debug.LogError("INCOMPLETE INNER TRIANGULATION.");
                                    }
                                }

                                /* Finally, we need to check edge inner triangles. If we are a border node and this wall goes into another border node, we are responsible for triangulating a hole */
                                if (edgePolygons.ContainsKey(polygon) && edgePolygons.ContainsKey(nodeToPoly[opposition]))
                                {
                                    /* Get the border edge(s) */
                                    var borderEdges = edgePolygons[polygon];
                                    foreach (var borderEdge in borderEdges)
                                    {
                                        /* Find the common point between this edge and the border edge. This is the triangle point */
                                        if (originalEdge.a.Equals(borderEdge.a))
                                        {
                                            Vector3 aa = a + wallOffset;
                                            Vector3 bb = oppAV + wallOffset;
                                            Vector3 cc = new Vector3(borderEdge.a.x, 0, borderEdge.a.y) + wallOffset;
                                            float winding = Vector3.Cross(aa - bb, cc - bb).y;

                                            if (winding < 0)
                                            {
                                                wallMesher.AddTriangle(aa, bb, cc);
                                            }
                                            else
                                            {
                                                wallMesher.AddTriangle(aa, cc, bb);
                                            }

                                        }
                                        else if (originalEdge.b.Equals(borderEdge.a))
                                        {
                                            Vector3 aa = b + wallOffset;
                                            Vector3 bb = oppBV + wallOffset;
                                            Vector3 cc = new Vector3(borderEdge.a.x, 0, borderEdge.a.y) + wallOffset;
                                            float winding = Vector3.Cross(aa - bb, cc - bb).y;

                                            if (winding < 0)
                                            {
                                                wallMesher.AddTriangle(aa, bb, cc);
                                            }
                                            else
                                            {
                                                wallMesher.AddTriangle(aa, cc, bb);
                                            }
                                        }
                                        else if (originalEdge.a.Equals(borderEdge.b))
                                        {
                                            Vector3 aa = a + wallOffset;
                                            Vector3 bb = oppAV + wallOffset;
                                            Vector3 cc = new Vector3(borderEdge.b.x, 0, borderEdge.b.y) + wallOffset;
                                            float winding = Vector3.Cross(aa - bb, cc - bb).y;

                                            if (winding < 0)
                                            {
                                                wallMesher.AddTriangle(aa, bb, cc);
                                            }
                                            else
                                            {
                                                wallMesher.AddTriangle(aa, cc, bb);
                                            }
                                        }
                                        else if (originalEdge.b.Equals(borderEdge.b))
                                        {
                                            Vector3 aa = b + wallOffset;
                                            Vector3 bb = oppBV + wallOffset;
                                            Vector3 cc = new Vector3(borderEdge.b.x, 0, borderEdge.b.y) + wallOffset;
                                            float winding = Vector3.Cross(aa - bb, cc - bb).y;

                                            if (winding < 0)
                                            {
                                                wallMesher.AddTriangle(aa, bb, cc);
                                            }
                                            else
                                            {
                                                wallMesher.AddTriangle(aa, cc, bb);
                                            }
                                        }
                                    }

                                }
                            }
                        }


                    }
                    else
                    {
                        /* We need to bridge the gap between the two cells */
                        /* We are only responsible for a brige if it's +z or z=0 AND +x */
                        var deltaPos = opposition.position - node.position;

                        if (deltaPos.z > 0 || deltaPos.z == 0 && deltaPos.x > 0)
                        {
                            /* Start by finding the matching indices for the two polygons to get positions */
                            int aMatchIndex = -1, bMatchIndex = -1;
                            var otherPoly = nodeToPoly[opposition];
                            for (int index = 0; index < otherPoly.vertices.Count; index++)
                            {
                                if (otherPoly.vertices[index].Equals(originalEdge.a)) aMatchIndex = index;
                                if (otherPoly.vertices[index].Equals(originalEdge.b)) bMatchIndex = index;

                                if (aMatchIndex >= 0 && bMatchIndex >= 0) break;
                            }

                            /* Triangulate the floor across the gap */
                            float2 oppA = scaledPolygons[otherPoly].vertices[aMatchIndex];
                            float2 oppB = scaledPolygons[otherPoly].vertices[bMatchIndex];
                            Vector3 oppAV = new Vector3(oppA.x, 0, oppA.y);
                            Vector3 oppBV = new Vector3(oppB.x, 0, oppB.y);
                            if(TriangulateFloor) floorMesher.AddQuad(b, a, oppAV, oppBV);

                            /* Triangulate the wall pieces left */
                            wallMesher.AddQuad(b, oppBV, oppBV + wallOffset, b + wallOffset);   /* Right wall */
                            wallMesher.AddQuad(oppAV, a, a + wallOffset, oppAV + wallOffset);   /* Left wall */

                            /* We also need to triangulate triangles to fill in top gaps. find matching indices again */
                            Polygon leftPoly = null, rightPoly = null;
                            int leftIndex = -1, rightIndex = -1;
                            foreach (var neighbor in node.Neighbors)
                            {
                                if (neighbor == opposition) continue; //we don't need to search our bridge neighbor
                                var neighborPoly = nodeToPoly[neighbor];
                                for (int index = 0; index < neighborPoly.vertices.Count; index++)
                                {
                                    if (leftPoly == null && neighborPoly.vertices[index].Equals(originalEdge.a))
                                    {
                                        leftIndex = index;
                                        leftPoly = neighborPoly;
                                        break; //this polygon is done - we found a match
                                    }
                                    if (rightPoly == null && neighborPoly.vertices[index].Equals(originalEdge.b))
                                    {
                                        rightIndex = index;
                                        rightPoly = neighborPoly;
                                        break; //this polygon is done - we found a match
                                    }
                                }

                                if (rightPoly != null && leftPoly != null) break; //we found what we needed
                            }
                            /* NOTE: At this point, if either is null, that implies this is an edge triangle, so no scaling is needed */

                            /* We now have our final triangle to complete */
                            float2 leftMatch = leftPoly != null ? scaledPolygons[leftPoly].vertices[leftIndex] : originalEdge.a;
                            float2 rightMatch = rightPoly != null ? scaledPolygons[rightPoly].vertices[rightIndex] : originalEdge.b;
                            Vector3 leftV = new Vector3(leftMatch.x, 0, leftMatch.y);
                            Vector3 rightV = new Vector3(rightMatch.x, 0, rightMatch.y);

                            wallMesher.AddTriangle(oppAV + wallOffset, a + wallOffset, leftV + wallOffset);
                            wallMesher.AddTriangle(b + wallOffset, oppBV + wallOffset, rightV + wallOffset);

                        }
                    }
                }

            }

            /* Our meshes are complete. Finish and assign them */
            if (TriangulateFloor)
            {
                var floorMesh = floorMesher.GenerateMesh();
                floorMesh.name = "Floor Mesh (" + chunkX + ", " + chunkY + ")";
                FloorFilters[chunkX, chunkY].sharedMesh = floorMesh;
                FloorColliders[chunkX, chunkY].sharedMesh = floorMesh;
            }
            var wallMesh = wallMesher.GenerateMesh();
            wallMesh.name = "Wall Mesh (" + chunkX + ", " + chunkY + ")";
            WallFilters[chunkX, chunkY].sharedMesh = wallMesh;
            WallColliders[chunkX, chunkY].sharedMesh = wallMesh;
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
                Utilities.Utilities.Shuffle(curr.Neighbors);                                                                                          //random walk
                                                                                                                                                      //curr.Neighbors.Sort((a, b) => Vector3.Distance(curr.position, a.position).CompareTo(Vector3.Distance(curr.position, b.position)));      //closest neighbor
                foreach (var neighbor in curr.Neighbors)
                {
                    /* If this node is forced to be closed, we shouldn't consider it at all */
                    if (curr.ForceClosedNeighbors.Contains(neighbor)) continue;

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
                    //curr.Neighbors.Remove(endPoint);
                    //endPoint.Neighbors.Remove(curr);
                    endPoint.OpenNeighbors.Add(curr);
                }
            }

            /* In addition, knock open some walls to allow for circular paths */
            int edgesToRemove = Mathf.FloorToInt((edgeCount / 2.0f) * WallRemovePercentage);
            for (int i = 0; i < edgesToRemove; i++)
            {
                AdjacencyNode chosen = samples[Random.Range(0, samples.Count)];
                var index = Random.Range(0, chosen.Neighbors.Count);
                if (!chosen.ForceClosedNeighbors.Contains(chosen.Neighbors[index]))
                {
                    /* Also check if breaking open this edge would form a triangle */
                    var other = chosen.Neighbors[index];
                    bool breakEdge = true;
                    foreach (var neighbor in chosen.OpenNeighbors)
                    {
                        if (other.OpenNeighbors.Contains(neighbor))
                        {
                            /* This would make a triangle. We don't want to break this edge */
                            breakEdge = false;
                            break;
                        }
                    }

                    if (breakEdge)
                    {
                        chosen.OpenNeighbors.Add(other);
                        other.OpenNeighbors.Add(chosen);
                    }
                }
            }
        }

        /// <summary>
        /// Generate an adjacency graph from the generated voronoi polygons.
        /// </summary>
        void GenerateAdjacencyGraph(float minimumEdgeSize)
        {
            /* First catalogue all edge / polygon pairs in the dual graph */
            edgeToNodeBorder = new Dictionary<Edge, List<AdjacencyNode>>();
            polyToNode = new Dictionary<Polygon, AdjacencyNode>();
            nodeToPoly = new Dictionary<AdjacencyNode, Polygon>();
            edgePolygons = new Dictionary<Polygon, List<Edge>>();
            centerPolygons = new List<Polygon>();

            /* Each time an edge is shared update the adjacency graph */
            Collider[] nonAlloc = new Collider[2];
            edgeCount = 0;
            foreach (var site in generatedVoronoi)
            {
                Polygon poly = site.polygon;

                /* Check to see if this polygon should even be used */
                bool usePolygon = true;
                var edges = poly.GetEdges();
                foreach (var edge in edges)
                {
                    /* check a */
                    if (Physics.OverlapBoxNonAlloc(new Vector3(edge.a.x, 0, edge.a.y), 0.5f * Vector3.one, nonAlloc, Quaternion.identity, LayerMask.GetMask("Maze Obstacle")) > 0)
                    {
                        /* Bad point */
                        usePolygon = false;
                        break;
                    }

                    /* Check edge */
                    float2 dir = edge.b - edge.a;
                    float magnitude = math.length(dir);
                    dir = math.normalize(dir);
                    if (Physics.Raycast(new Ray(new Vector3(edge.a.x, 0, edge.a.y), new Vector3(dir.x, 0, dir.y)), magnitude, LayerMask.GetMask("Maze Obstacle")))
                    {
                        /* Bad edge */
                        usePolygon = false;
                        break;
                    }
                }
                if (!usePolygon)
                {
                    /* Mark this polygon as a collider polygon and don't put it in adjacency */
                    centerPolygons.Add(poly);
                    continue;
                }

                /* Record the number of edges into a counter for help in making the maze easier */
                edgeCount += edges.Count;

                /* Record this polygon into an adjacency node */
                AdjacencyNode node = new AdjacencyNode(new Vector3(site.site.x, 0, site.site.y));
                polyToNode.Add(poly, node);
                nodeToPoly.Add(node, poly);

                /* Catalogue each edge */
                foreach (var edge in poly.GetEdges())
                {
                    if (!edgeToNodeBorder.ContainsKey(edge))
                    {
                        edgeToNodeBorder.Add(edge, new List<AdjacencyNode>() { node });
                    }
                    else
                    {
                        edgeToNodeBorder[edge].Add(node);
                        edgeToNodeBorder[edge][0].Neighbors.Add(node);
                        node.Neighbors.Add(edgeToNodeBorder[edge][0]);

                        /* If the edge is small, make sure it is never allowed to be opened */
                        /* This is needed for proper triangulation */
                        if (math.distance(edge.a, edge.b) < minimumEdgeSize)
                        {
                            edgeToNodeBorder[edge][0].ForceClosedNeighbors.Add(node);
                            node.ForceClosedNeighbors.Add(edgeToNodeBorder[edge][0]);
                        }
                    }
                }
            }

            /* Compile a list of edge polygons for usage in triangulation */
            foreach (var key in edgeToNodeBorder.Keys)
            {
                var nodes = edgeToNodeBorder[key];
                if (nodes.Count == 1)
                {
                    /* This is an edge polygon */
                    if (!edgePolygons.ContainsKey(nodeToPoly[nodes[0]]))
                    {
                        edgePolygons.Add(nodeToPoly[nodes[0]], new List<Edge>() { key });
                    }
                    else
                    {
                        edgePolygons[nodeToPoly[nodes[0]]].Add(key);
                    }
                }
            }

            /* Create a normal list to store the adjacency nodes into */
            samples = new List<AdjacencyNode>();
            samples.AddRange(nodeToPoly.Keys);

        }

        private void OnDrawGizmosSelected()
        {
            if (samples != null && DrawSites)
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

            if (generatedVoronoi != null && DrawVoronoi)
            {
                Gizmos.color = Color.blue;
                foreach (var polygon in generatedVoronoi)
                {
                    foreach (var edge in polygon.polygon.GetEdges())
                    {
                        Gizmos.DrawLine(new Vector3(edge.a.x, 0, edge.a.y), new Vector3(edge.b.x, 0, edge.b.y));
                    }
                }
            }
        }
    }
}
