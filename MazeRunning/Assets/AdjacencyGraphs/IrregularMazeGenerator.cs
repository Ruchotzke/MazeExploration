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
    public Bounds Boundary;
    public float MinDistance = 1.0f;
    public float WallHeight = 0.5f;
    
    [Header("Prefabs")]
    public Waypoint pf_Waypoint;

    [Header("Mesh Rendering")]
    public MeshFilter Filter;
    public MeshRenderer Renderer;
    public Material RenderMaterial;

    [Header("Draw Settings")] 
    public bool DrawBaseGrid = true;
    public bool DrawMazeGrid = true;
    public bool DrawVoronoi = true;

    private List<AdjacencyNode> samples;
    private Dictionary<float2, AdjacencyNode> siteToNode;

    private Mesh generatedMesh;
    private List<(float2 site, Polygon polygon)> generatedVoronoi;

    private UnityEngine.Mesh triangulatedMesh;
    public int Seed;

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

        /* Build up an adjacency graph */
        GenerateAdjacencyGraph(generatedMesh);
        
        /* Backtrace over the graph to generate a maze */
        DoBacktrace(samples[0]);

        /* Create a voronoi diagram to help in mesh construction */
        generatedVoronoi = generatedMesh.GenerateDualGraph(new float2(Boundary.min.x, Boundary.min.z), new float2(Boundary.max.x, Boundary.max.z));

        /* We now have a maze layout picked - we now need to generate a mesh to hold this maze */
        TriangulateMaze();

        /* Apply this new mesh to the mesh renderer */
        Filter.mesh = triangulatedMesh;
        Renderer.material = RenderMaterial;
    }
    
    /// <summary>
    /// Triangulate a mesh from its voronoi polygons.
    /// </summary>
    /// <returns></returns>
    private void TriangulateMaze()
    {
        Mesher mesher = new Mesher();
        Vector3 wallOffset = new Vector3(0f, WallHeight, 0f);

        /* Triangulate each cell */
        foreach(var cell in generatedVoronoi)
        {
            var polygon = cell.polygon;
            AdjacencyNode node = siteToNode[cell.site];

            /* Get a list of line segments to check for open walls later */
            List<Vector3> openDirs = new List<Vector3>();
            foreach(var neighbor in node.OpenNeighbors)
            {
                openDirs.Add(neighbor.position - node.position);
            }

            /* Triangulate the floor */
            for(int i = 1; i < polygon.vertices.Count - 1; i++)
            {
                /* Triangle fan */
                var a = polygon.vertices[0];
                var b = polygon.vertices[i];
                var c = polygon.vertices[i+1];

                mesher.AddTriangle(a, b, c);
            }

            /* Triangulate the walls */
            foreach(var edge in polygon.GetEdges())
            {
                Vector3 a = new Vector3(edge.a.x, 0, edge.a.y);
                Vector3 b = new Vector3(edge.b.x, 0, edge.b.y);

                /* Check if this edge should be open. if so, don't triangulate it */
                int i;
                for(i = 0; i < openDirs.Count; i++)
                {
                    if(Mathf.Abs(Vector3.Dot((b-a), openDirs[i])) < 0.01f)
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
                    mesher.AddQuad(b, a, a + wallOffset, b + wallOffset);
                }
            }
        }

        /* Create the mesh */
        triangulatedMesh = mesher.GenerateMesh();
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
            //Utilities.Utilities.Shuffle(curr.Neighbors);                                                                                          //random walk
            curr.Neighbors.Sort((a, b) => Vector3.Distance(curr.position, a.position).CompareTo(Vector3.Distance(curr.position, b.position)));      //closest neighbor
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
    /// Generate an adjacency graph from the triangulation provided.
    /// </summary>
    void GenerateAdjacencyGraph(Mesh triangulation)
    {
        samples = new List<AdjacencyNode>();
        siteToNode = new Dictionary<float2, AdjacencyNode>();
        Dictionary<float2, AdjacencyNode> nodeLookup = new Dictionary<float2, AdjacencyNode>();
        foreach (var triangle in triangulation.Triangles)
        {
            /* Foreach edge, build the adjacency list of the nodes */
            foreach (var edge in triangle.GetEdges())
            {
                /* If these are new adjacency nodes, generate them */
                if (!nodeLookup.ContainsKey(edge.a))
                {
                    AdjacencyNode node = new AdjacencyNode(new Vector3(edge.a.x, 0, edge.a.y));
                    siteToNode.Add(edge.a, node);
                    samples.Add(node);
                    nodeLookup.Add(edge.a, node);
                }
                if (!nodeLookup.ContainsKey(edge.b))
                {
                    AdjacencyNode node = new AdjacencyNode(new Vector3(edge.b.x, 0, edge.b.y));
                    siteToNode.Add(edge.b, node);
                    samples.Add(node);
                    nodeLookup.Add(edge.b, node);
                }
                
                /* We can now update our adjacency lists */
                AdjacencyNode a = nodeLookup[edge.a];
                AdjacencyNode b = nodeLookup[edge.b];
        
                if (!a.Neighbors.Contains(b)) a.Neighbors.Add(b);
                if (!b.Neighbors.Contains(a)) b.Neighbors.Add(a);
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
