using System.Collections;
using System.Collections.Generic;
using Delaunay;
using Delaunay.Geometry;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEditor.VersionControl;
using UnityEngine;
using UnityEngine.TestTools;

public class ClippingTests
{
    public float2 min = float2.zero;
    public float2 max = new float2(10f, 10f);

    [Test]
    public void TestInBounds()
    {
        Edge t1 = new Edge(new float2(1f, 1f), new float2(3f, 3f));
        Edge t2 = new Edge(new float2(9f, 9f), new float2(3f, 5f));
        Edge t3 = new Edge(new float2(1f, 2f), new float2(1f, 8f));
        Edge t4 = new Edge(new float2(1f, 2f), new float2(8f, 2f));

        var r1 = Clipping.ClipEdge(t1, min, max);
        var r2 = Clipping.ClipEdge(t2, min, max);
        var r3 = Clipping.ClipEdge(t3, min, max);
        var r4 = Clipping.ClipEdge(t4, min, max);

        Assert.AreEqual(true, r1.isVisible);
        Assert.AreEqual(false, r1.editedEdge);
        Assert.AreEqual(true, r2.isVisible);
        Assert.AreEqual(false, r2.editedEdge);
        Assert.AreEqual(true, r3.isVisible);
        Assert.AreEqual(false, r3.editedEdge);
        Assert.AreEqual(true, r4.isVisible);
        Assert.AreEqual(false, r4.editedEdge);
    }

    [Test]
    public void TestSinglePoint()
    {
        Edge single = new Edge(new float2(5, 5), new float2(5, 5));
        var result = Clipping.ClipEdge(single, min, max);
        
        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(false, result.editedEdge);
    }

    [Test]
    public void TestLrHorizontalClipR()
    {
        Edge lr = new Edge(new float2(1f, 1f), new float2(11f, 1f));

        var result = Clipping.ClipEdge(lr, min, max);

        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(true, result.editedEdge);
        Assert.AreEqual(new float2(10f, 1f), lr.b);
    }
    
    [Test]
    public void TestLRHorizontalClipL()
    {
        Edge lr = new Edge(new float2(-1f, 1f), new float2(9f, 1f));

        var result = Clipping.ClipEdge(lr, min, max);

        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(true, result.editedEdge);
        Assert.AreEqual(new float2(0f, 1f), lr.a);
    }
    
    [Test]
    public void TestLRHorizontalClipLR()
    {
        Edge lr = new Edge(new float2(-1f, 1f), new float2(11f, 1f));

        var result = Clipping.ClipEdge(lr, min, max);

        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(true, result.editedEdge);
        Assert.AreEqual(new float2(0f, 1f), lr.a);
        Assert.AreEqual(new float2(10f, 1f), lr.b);
    }
    
    [Test]
    public void TestBtHorizontalClipT()
    {
        Edge bt = new Edge(new float2(1f, 1f), new float2(1f, 13f));

        var result = Clipping.ClipEdge(bt, min, max);

        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(true, result.editedEdge);
        Assert.AreEqual(new float2(1f, 10f), bt.b);
    }
    
    [Test]
    public void TestBtHorizontalClipB()
    {
        Edge bt = new Edge(new float2(1f, -1f), new float2(1f, 1f));

        var result = Clipping.ClipEdge(bt, min, max);

        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(true, result.editedEdge);
        Assert.AreEqual(new float2(1f, 0f), bt.a);
    }
    
    [Test]
    public void TestBtHorizontalClipBT()
    {
        Edge bt = new Edge(new float2(1f, -1f), new float2(1f, 100f));

        var result = Clipping.ClipEdge(bt, min, max);

        Assert.AreEqual(true, result.isVisible);
        Assert.AreEqual(true, result.editedEdge);
        Assert.AreEqual(new float2(1f, 0f), bt.a);
        Assert.AreEqual(new float2(1f, 10f), bt.b);
    }

    [Test]
    public void TestDiagonalClipNonCorner()
    {
        Edge a = new Edge(new float2(-1f, 2f), new float2(13f, 7f));
        Edge b = new Edge(new float2(-1f, 8f), new float2(13f, 2f));
        Edge c = new Edge(new float2(2f, -1f), new float2(8f, 11f));
        Edge d = new Edge(new float2(8f, -1f), new float2(2f, 11f));

        var ra = Clipping.ClipEdge(a, min, max);
        var rb = Clipping.ClipEdge(b, min, max);
        var rc = Clipping.ClipEdge(c, min, max);
        var rd = Clipping.ClipEdge(d, min, max);

        Assert.AreEqual(true, ra.isVisible);
        Assert.AreEqual(true, ra.editedEdge);
        Assert.AreEqual(true, rb.isVisible);
        Assert.AreEqual(true, rb.editedEdge);
        Assert.AreEqual(true, rc.isVisible);
        Assert.AreEqual(true, rc.editedEdge);
        Assert.AreEqual(true, rd.isVisible);
        Assert.AreEqual(true, rd.editedEdge);
    }

    [Test]
    public void TestDiagonalClipCorner()
    {
        Edge corner = new Edge(new float2(-1f, -1f), new float2(11f, 11f));
        var result = Clipping.ClipEdge(corner, min, max);

        Assert.IsTrue(result.isVisible);
        Assert.IsTrue(result.editedEdge);
        Assert.AreEqual(min, corner.a);
        Assert.AreEqual(max, corner.b);
        
        corner = new Edge(new float2(11f, 11f), new float2(-1f, -1f));
        result = Clipping.ClipEdge(corner, min, max);

        Assert.IsTrue(result.isVisible);
        Assert.IsTrue(result.editedEdge);
        Assert.AreEqual(min, corner.b);
        Assert.AreEqual(max, corner.a);
    }

    [Test]
    public void TestRightLeft()
    {
        Edge rl = new Edge(new float2(11f, 5f), new float2(-50f, 5f));
        var result = Clipping.ClipEdge(rl, min, max);

        Assert.IsTrue(result.isVisible);
        Assert.IsTrue(result.editedEdge);
        Assert.AreEqual(new float2(10f, 5f), rl.a);
        Assert.AreEqual(new float2(0f, 5f), rl.b);
    }
    
    [Test]
    public void TestTopBot()
    {
        Edge tb = new Edge(new float2(5f, 11f), new float2(5f, -30f));
        var result = Clipping.ClipEdge(tb, min, max);

        Assert.IsTrue(result.isVisible);
        Assert.IsTrue(result.editedEdge);
        Assert.AreEqual(new float2(5f, 10f), tb.a);
        Assert.AreEqual(new float2(5f, 0f), tb.b);
    }
}
