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
    private Dictionary<float2, AdjacencyNode> siteToNode;

    private Mesh generatedMesh;
    private List<(float2 site, Polygon polygon)> generatedVoronoi;
    private List<float2> circumcenters;

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
        var dual = generatedMesh.GenerateDualGraph(new float2(Boundary.min.x, Boundary.min.z), new float2(Boundary.max.x, Boundary.max.z));
        generatedVoronoi = dual.voronoi;
        circumcenters = dual.circumcenters;

        /* Build up an adjacency graph */
        GenerateAdjacencyGraph(MinOpenWallLength);

        /* Backtrace over the graph to generate a maze */
        DoBacktrace(samples[0]);

        /* We now have a maze layout picked - we now need to generate a mesh to hold this maze */
        //TriangulateMaze();

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

        /* Triangulate each cell */
        foreach (var cell in generatedVoronoi)
        {
            var polygon = cell.polygon;
            AdjacencyNode node = siteToNode[cell.site];

            /* Scale the polygon down by a small amount */
            Polygon shrunkPolygon = null;
            if (BorderThickness > 0.0f)
            {
                shrunkPolygon = polygon.ScalePolygon(scaleFactor);
            }
            else
            {
                shrunkPolygon = polygon;
            }

            /* Get a list of line segments to check for open walls later */
            List<Vector3> openDirs = new List<Vector3>();
            foreach(var neighbor in node.OpenNeighbors)
            {
                openDirs.Add(neighbor.position - node.position);
            }

            /* Triangulate the floor */
            for(int i = 1; i < shrunkPolygon.vertices.Count - 1; i++)
            {
                /* Triangle fan */
                var a = shrunkPolygon.vertices[0];
                var b = shrunkPolygon.vertices[i];
                var c = shrunkPolygon.vertices[i+1];

                floorMesher.AddTriangle(a, b, c);
            }

            /* Triangulate the walls */
            var originalEdges = BorderThickness > 0.0f ? polygon.GetEdges() : null;
            var shrunkEdges = shrunkPolygon.GetEdges();
            for(int e = 0; e < shrunkEdges.Count; e++)
            {
                Edge edge = shrunkEdges[e];
                Edge originalEdge = BorderThickness > 0.0f ? originalEdges[e] : null;

                Vector3 a = new Vector3(edge.a.x, 0, edge.a.y);
                Vector3 b = new Vector3(edge.b.x, 0, edge.b.y);

                /* Check if this edge should be open. if so, don't triangulate it */
                int i;
                for(i = 0; i < openDirs.Count; i++)
                {
                    if(Mathf.Abs(Vector3.Dot((b-a), openDirs[i])) < 0.001f)
                    {
                        /* This wall is open */
                        break;
                    }
                }
                
                /* If the wall is open, remove that direction and don't do anything else */
                if(i < openDirs.Count)
                {
                    openDirs.RemoveAt(i);
                }
                else
                {
                    /* We need to triangulate our inner wall and half of the thickness on top */
                    /* We then use columns to cover up the edge thicknesses */
                    wallMesher.AddQuad(b, a, a + wallOffset, b + wallOffset);   /* Inner wall */

                    /* To add thickness, we just triangulate the quad between the shrunk and original versions */
                    if(BorderThickness > 0.0f)
                    {
                        /* make v3s for original borders */
                        Vector3 oa = new Vector3(originalEdge.a.x, 0, originalEdge.a.y);
                        Vector3 ob = new Vector3(originalEdge.b.x, 0, originalEdge.b.y);

                        /* Top of wall */
                        wallMesher.AddQuad(a + wallOffset, oa + wallOffset, ob + wallOffset, b + wallOffset);

                        /* Sides of wall */
                        wallMesher.AddQuad(a, oa, oa + wallOffset, a + wallOffset);
                        wallMesher.AddQuad(b, ob, ob + wallOffset, b + wallOffset);
                    }

                }
            }
        }

        /* Create the mesh */
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
        Dictionary<Edge, AdjacencyNode> borders = new Dictionary<Edge, AdjacencyNode>();
        Dictionary<Polygon, AdjacencyNode> polyToNode = new Dictionary<Polygon, AdjacencyNode>();
        Dictionary<AdjacencyNode, Polygon> nodeToPoly = new Dictionary<AdjacencyNode, Polygon>();

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
                if (!borders.ContainsKey(edge))
                {
                    borders.Add(edge, node);
                }
                else
                {
                    /* Before we connect this edge, make sure this edge is large enough to consider making open */
                    if(math.distance(edge.a, edge.b) >= minimumEdgeSize)
                    {
                        borders[edge].Neighbors.Add(node);
                        node.Neighbors.Add(borders[edge]);
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


    /// <summary>
    /// Copied from https://forum.unity.com/threads/line-intersection.17384/
    /// </summary>
    /// <param name="line1point1"></param>
    /// <param name="line1point2"></param>
    /// <param name="line2point1"></param>
    /// <param name="line2point2"></param>
    /// <returns></returns>
    static bool FasterLineSegmentIntersection(Vector2 line1point1, Vector2 line1point2, Vector2 line2point1, Vector2 line2point2)
    {

        Vector2 a = line1point2 - line1point1;
        Vector2 b = line2point1 - line2point2;
        Vector2 c = line1point1 - line2point1;

        float alphaNumerator = b.y * c.x - b.x * c.y;
        float betaNumerator = a.x * c.y - a.y * c.x;
        float denominator = a.y * b.x - a.x * b.y;

        if (denominator == 0)
        {
            return false;
        }
        else if (denominator > 0)
        {
            if (alphaNumerator < 0 || alphaNumerator > denominator || betaNumerator < 0 || betaNumerator > denominator)
            {
                return false;
            }
        }
        else if (alphaNumerator > 0 || alphaNumerator < denominator || betaNumerator > 0 || betaNumerator < denominator)
        {
            return false;
        }
        return true;
    }
}
