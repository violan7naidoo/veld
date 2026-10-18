using System.Collections.Generic;
using Godot;
using Veld.Sim.Geometry;
using Xunit;

namespace Veld.Tests.Sim.Geometry;

/// <summary>Outlines are counter-clockwise rings of [x, z] metres, as the map data gives them.</summary>
public class RoofOutlineTests
{
    /// <summary>The L made of [0,4]x[0,10] and [0,10]x[0,4].</summary>
    public static readonly List<Vector2> L = new() { new(0, 0), new(10, 0), new(10, 4), new(4, 4), new(4, 10), new(0, 10) };

    [Fact]
    public void Simplify_drops_a_vertex_on_a_straight_edge_and_a_short_jog()
    {
        var straight = new List<Vector2> { new(0, 0), new(2, 0), new(4, 0), new(4, 2), new(0, 2) };
        var jog = new List<Vector2> { new(0, 0), new(4, 0), new(4, 2), new(2, 2), new(2, 2.3f), new(0, 2.3f) };

        Assert.Equal(4, RoofOutline.Simplify(straight).Count);
        Assert.Equal(4, RoofOutline.Simplify(jog).Count);
    }

    [Fact]
    public void Offset_moves_every_edge_outward_by_the_distance()
    {
        var rect = new List<Vector2> { new(0, 0), new(4, 0), new(4, 2), new(0, 2) };

        var wide = RoofOutline.Offset(rect, 0.45f);

        var expected = new[] { new Vector2(-0.45f, -0.45f), new Vector2(4.45f, -0.45f), new Vector2(4.45f, 2.45f), new Vector2(-0.45f, 2.45f) };
        Assert.Equal(4, wide.Count);
        for (int i = 0; i < 4; i++)
        {
            Assert.Equal(expected[i].X, wide[i].X, 3);
            Assert.Equal(expected[i].Y, wide[i].Y, 3);
        }
    }

    [Fact]
    public void An_L_shaped_house_is_roofable()
    {
        Assert.True(RoofOutline.IsRoofable(L));
    }

    [Fact]
    public void A_dart_with_a_corner_far_from_square_is_not_roofable()
    {
        var dart = new List<Vector2> { new(0, 0), new(10, 0), new(10, 6), new(5, 1) };

        Assert.False(RoofOutline.IsRoofable(dart));
    }

    [Fact]
    public void A_fourteen_vertex_comb_is_not_roofable()
    {
        var comb = new List<Vector2>
        {
            new(0, 0), new(14, 0), new(14, 2), new(12, 2), new(12, 4), new(10, 4), new(10, 2),
            new(8, 2), new(8, 4), new(6, 4), new(6, 2), new(4, 2), new(4, 4), new(0, 4),
        };

        Assert.False(RoofOutline.IsRoofable(comb));
    }

    [Fact]
    public void A_convex_pentagon_is_roofable()
    {
        var pentagon = new List<Vector2>();
        for (int k = 0; k < 5; k++) pentagon.Add(new Vector2(5f, 0f).Rotated(Mathf.DegToRad(72f * k)));

        Assert.True(RoofOutline.IsRoofable(pentagon));
    }
}
