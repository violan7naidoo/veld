using System.Collections.Generic;
using Godot;
using Veld.Sim.Geometry;
using Xunit;

namespace Veld.Tests.Sim.Geometry;

public class PolygonTests
{
    private static List<Vector2> Rect(float w, float h) => new() { new(0, 0), new(w, 0), new(w, h), new(0, h) };

    [Fact]
    public void SignedArea_is_the_area_counter_clockwise_and_negative_clockwise()
    {
        var rect = Rect(3f, 2f);

        Assert.Equal(6f, Polygons.SignedArea(rect), 3);
        rect.Reverse();
        Assert.Equal(-6f, Polygons.SignedArea(rect), 3);
    }

    [Fact]
    public void Centroid_is_the_mean_of_the_vertices()
    {
        Assert.Equal(new Vector2(0.5f, 0.5f), Polygons.Centroid(Rect(1f, 1f)));
    }

    [Fact]
    public void OrientedRect_follows_the_longest_edge()
    {
        float turn = Mathf.DegToRad(30f);
        var rect = new List<Vector2>();
        foreach (var p in Rect(4f, 2f)) rect.Add(p.Rotated(turn));
        var longEdge = new Vector2(1f, 0f).Rotated(turn);

        var r = Polygons.OrientedRect(rect);

        Assert.NotNull(r);
        Assert.Equal(1f, Mathf.Abs(r.Value.Axis.Dot(longEdge)), 3);
        Assert.Equal(2f, r.Value.HalfLong, 3);
        Assert.Equal(1f, r.Value.HalfShort, 3);
        Assert.Equal(new Vector2(2f, 1f).Rotated(turn).X, r.Value.Center.X, 3);
        Assert.Equal(new Vector2(2f, 1f).Rotated(turn).Y, r.Value.Center.Y, 3);
    }

    [Fact]
    public void OrientedRect_is_null_when_the_longest_edge_is_under_a_tenth_of_a_metre()
    {
        Assert.Null(Polygons.OrientedRect(Rect(0.05f, 0.05f)));
    }
}
