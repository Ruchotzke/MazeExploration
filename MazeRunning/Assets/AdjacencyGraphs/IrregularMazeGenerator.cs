using System.Collections.Generic;
using Delaunay.Geometry;
using Delaunay.Triangulation;
using Unity.Mathematics;
using UnityEngine;
using Utilities;
using Utilities.Meshing;
using Mesh = Delaunay.Triangulation.Mesh;

public class IrregularMazeGenerator : MonoBehaviour
{
    [Header("Settings")]
    public int Seed;                        /* The random seed used for this maze */
    public Bounds Boundary;                 /* The general rectangular region to shape the maze */
    public float MinDistance = 1.0f;        /* The minimum distance between cells */
    public float MinOpenWallLength = 1.0f;  /* The minimum length a wall must be to be considered for an opening */
    public float WallHeight = 0.5f;         /* The height of the generated walls */

    [Range(0.01f, 1.0f)]
    public float BorderThickness = 0.1f;    /* The percentage of the original cells which should become border */
    
    [Header("Prefabs")]
    public Waypoint pf_Waypoint;

    [Header("Mesh Rendering")]
    public MeshFilter FloorFilter;
    public MeshFilter WallFilter;

    [Header("Draw Settings")] 
    public bool DrawBaseGrid = true;
    public bool DrawMazeGrid = true;
    public bool DrawVoronoi = true;

    private List<AdjacencyNode> samples;
    private Dictionary<Edge, List<AdjacencyNode>> edgeToNodeBorder;
    private Dictionary<Polygon, AdjacencyNode> polyToNode;
    private Dictionary<AdjacencyNode, Polygon> nodeToPoly;

    private Mesh generatedMesh;
    private List<(float2 site, Polygon polygon)> generatedVoronoi;

    private UnityEngine.Mesh triangulatedFloorMesh;
    private UnityEngine.Mesh triangulatedWallMesh;
    

    private void Start()
    {
        UnityEngine.Random.InitState(Seed);
        GenerateMaze();
    }
    
    private void GenerateMaze()
    {
        /* First sample the region where the maze will lie */
        PoissonSampler sampler = new PoissonSampler(new Rect(Boundary.min.x, Boundary.min.z, Boundary.size.x, Boundary.size.z), MinDistance);
        List<Vector2> points = sampler.Sample();

        /* Create a triangulation of the region */
        Triangulator triangulator = new Triangulator();
        foreach (var vertex in points)
        {
            triangulator.AddVertex(vertex);
        }
        generatedMesh = triangulator.GenerateTriangulation();

        /* Clip out very skinny triangles from the generated triangulation */
        generatedMesh.RemoveSkinnyTriangles();

        /* Create a voronoi diagram to help in mesh construction */
        generatedVoronoi = generatedMesh.GenerateDualGraph(new float2(Boundary.min.x, Boundary.min.z), new float2(Boundary.max.x, Boundary.max.z));

        /* Build up an adjacency graph */
        GenerateAdjacencyGraph(MinOpenWallLength);

        /* Backtrace over the graph to generate a maze */
        DoBacktrace(samples[0]);

        /* We now have a maze layout picked - we now need to generate a mesh to hold this maze */
        TriangulateMaze();

        /* Apply this new mesh to the mesh renderer */
        FloorFilter.mesh = triangulatedFloorMesh;
        WallFilter.mesh = triangulatedWallMesh;
    }
    
    /// <summary>
    /// Triangulate a mesh from its voronoi polygons.
    /// </summary>
    /// <returns></returns>
    private void TriangulateMaze()
    {
        Mesher floorMesher = new Mesher();
        Mesher wallMesher = new Mesher();
        Vector3 wallOffset = new Vector3(0f, WallHeight, 0f);
        float2 scaleFactor = new float2(1.0f - BorderThickness, 1.0f - BorderThickness);

        Dictionary<Polygon, Polygon> scaledPolygons = new Dictionary<Polygon, Polygon>();

        /* Shrink each polygon (we need to do it early to ensure we can triangulate bridges */
        foreach(var node in samples)
        {
            var polygon = nodeToPoly[node];

            /* Scale the polygon down by the given amount */
            var shrunkPolygon = polygon.ScalePolygon(scaleFactor);
            scaledPolygons.Add(polygon, shrunkPolygon);
        }

        /* Triangulate each polygon based on its neighbors */
        foreach(var node in samples)
        {
            var polygon = nodeToPoly[node];

            /* Scale the polygon down by the given amount */
            var shrunkPolygon = scaledPolygons[polygon];

            /* Triangulate the floor of the polygon */
            for (int i = 1; i < shrunkPolygon.vertices.Count - 1; i++)
            {
                /* Triangle fan */
                var a = shrunkPolygon.vertices[0];
                var b = shrunkPolygon.vertices[i];
                var c = shrunkPolygon.vertices[i + 1];

                floorMesher.AddTriangle(a, b, c);
            }

            /* Triangulate the walls of the polygon */
            var originalEdges = polygon.GetEdges();
            var shrunkEdges = shrunkPolygon.GetEdges();
            for(int i = 0; i < shrunkEdges.Count; i++)
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
                if(borderNodes.Count >= 2)
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
                    if(opposition == null)
                    {
                        /* Triangulate the top of the wall, it's a border so we are always responsible */
                        wallMesher.AddQuad(b + wallOffset, a + wallOffset, oa + wallOffset, ob + wallOffset);
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

                            /* Triangulate the floor across the gap */
                            float2 oppA = scaledPolygons[otherPoly].vertices[aMatchIndex];
                            float2 oppB = scaledPolygons[otherPoly].vertices[bMatchIndex];
                            Vector3 oppAV = new Vector3(oppA.x, 0, oppA.y);
                            Vector3 oppBV = new Vector3(oppB.x, 0, oppB.y);
                            wallMesher.AddQuad(b + wallOffset, a + wallOffset, oppAV + wallOffset, oppBV + wallOffset);
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
                        floorMesher.AddQuad(b, a, oppAV, oppBV);

                        /* Triangulate the wall pieces left */
                        //wallMesher.AddQuad(a, halfA, halfA + wallOffset, a + wallOffset);
                        //wallMesher.AddQuad(b, halfB, halfB + wallOffset, b + wallOffset);
                    }
                }
            }

        }

        /* Our meshes are complete. Finish and assign them */
        triangulatedFloorMesh = floorMesher.GenerateMesh();
        triangulatedWallMesh = wallMesher.GenerateMesh();
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
                curr.Neighbors.Remove(endPoint);
                endPoint.Neighbors.Remove(curr);
                endPoint.OpenNeighbors.Add(curr);
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

        /* Each time an edge is shared update the adjacency graph */
        foreach(var site in generatedVoronoi)
        {
            Polygon poly = site.polygon;

            /* Record this polygon into an adjacency node */
            AdjacencyNode node = new AdjacencyNode(new Vector3(site.site.x, 0, site.site.y));
            polyToNode.Add(poly, node);
            nodeToPoly.Add(node, poly);

            /* Catalogue each edge */
            foreach(var edge in poly.GetEdges())
            {
                if (!edgeToNodeBorder.ContainsKey(edge))
                {
                    edgeToNodeBorder.Add(edge, new List<AdjacencyNode>() {node });
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

        /* Create a normal list to store the adjacency nodes into */
        samples = new List<AdjacencyNode>();
        samples.AddRange(nodeToPoly.Keys);

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
