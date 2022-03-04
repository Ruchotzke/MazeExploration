using System.Collections;
using System.Collections.Generic;
using Delaunay.Geometry;
using NUnit.Framework;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.TestTools;

public class GeometryTests
{
    [Test]
    public void ConstructTriangle()
    {
        Triangle triangle = new Triangle(
            new float2(0.0f, 1.0f),
            new float2(0.866f, -0.5f),
            new float2(-0.866f, -0.5f));

        Assert.AreEqual(0.0f, triangle.a.x);
        Assert.AreEqual(0.866f, triangle.b.x);
        Assert.AreEqual(-0.866f, triangle.c.x);
    }

    [Test]
    public void ConstructTriangleWinding()
    {
        Triangle triangle = new Triangle(
            new float2(0.0f, 1.0f),
            new float2(-0.866f, -0.5f),
            new float2(0.866f, -0.5f));

        Assert.AreEqual(0.0f, triangle.a.x);
        Assert.AreEqual(0.866f, triangle.b.x);
        Assert.AreEqual(-0.866f, triangle.c.x);
    }

    [Test]
    public void ConstructTriangleColinear()
    {
        Triangle triangle1 = new Triangle(
            new float2(0.0f, 1.0f),
            new float2(0.0f, 2.0f),
            new float2(0.0f, 3.0f));
        
        Triangle triangle2 = new Triangle(
            new float2(0.0f, 1.0f),
            new float2(0.0f, 1.0f),
            new float2(0.0f, 1.0f));

        Assert.NotNull(triangle1);
        Assert.NotNull(triangle2);
    }

    [Test]
    public void ConstructCircle()
    {
        Circle circle = new Circle(float2.zero, 1.0f);

        Assert.AreEqual(float2.zero, circle.center);
        Assert.AreEqual(1.0f, circle.radius);
    }

    [Test]
    public void GetCircumcircleEquilateral()
    {
        Triangle triangle = new Triangle(
            new float2(0.0f, 1.0f),
            new float2(0.866f, -0.5f),
            new float2(-0.866f, -0.5f));

        Circle circumcircle = triangle.GetCircumcircle();
        
        Assert.That(circumcircle.center.x, Is.EqualTo(0.0f).Within(5).Percent);
        // Assert.That(circumcircle.center.y, Is.EqualTo(0.0f).Within(5).Percent);

        Assert.That(circumcircle.radius, Is.EqualTo(1.0f).Within(0.5).Percent);
    }
    
    [Test]
    public void GetCircumcircleEquilateralOffCenter()
    {
        Triangle triangle = new Triangle(
            new float2(0.0f, 1.0f) + new float2(1.0f, 2.0f),
            new float2(0.866f, -0.5f) + new float2(1.0f, 2.0f),
            new float2(-0.866f, -0.5f) + new float2(1.0f, 2.0f));

        Circle circumcircle = triangle.GetCircumcircle();

        Assert.That(circumcircle.center.x, Is.EqualTo(1.0f).Within(0.5).Percent);
        Assert.That(circumcircle.center.y, Is.EqualTo(2.0f).Within(0.5).Percent);
        
        Assert.That(circumcircle.radius, Is.EqualTo(1.0f).Within(0.5).Percent);
    }

    [Test]
    public void GetCircumcircleColinear()
    {
        Triangle triangle = new Triangle(
            new float2(0.0f, 1.0f),
            new float2(0.0f, 2.0f),
            new float2(0.0f, 3.0f));

        Assert.IsNull(triangle.GetCircumcircle());
    }

    [Test]
    public void TestInteriorofCircleTrue()
    {
        Circle c = new Circle(float2.zero, 1.0f);

        Assert.IsTrue(c.PointLiesInside(float2.zero));
        Assert.IsTrue(c.PointLiesInside(new float2(0.5f, 0.5f)));
        Assert.IsTrue(c.PointLiesInside(new float2(-0.5f, -0.1f)));
    }
    
    [Test]
    public void TestInteriorofCircleFalse()
    {
        Circle c = new Circle(new float2(55.0f, 2.3f), 1.0f);

        Assert.IsFalse(c.PointLiesInside(float2.zero));
        Assert.IsFalse(c.PointLiesInside(new float2(0.5f, 0.5f)));
        Assert.IsFalse(c.PointLiesInside(new float2(-0.5f, -0.1f)));
    }
    
    [Test]
    public void TestInteriorofCircleEdge()
    {
        Circle c = new Circle(float2.zero, 1.0f);

        Assert.IsTrue(c.PointLiesInside(new float2(1.0f, 0.0f)));
        Assert.IsTrue(c.PointLiesInside(new float2(0.0f, 1f)));
    }
}
