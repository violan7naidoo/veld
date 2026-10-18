using System.Collections.Generic;
using System.Linq;
using Godot;
using Veld.Sim.Geometry;
using Xunit;

namespace Veld.Tests.Sim.Geometry;

/// <summary>Outlines are counter-clockwise rings of [x, z] metres; heights are inward offset distances, not metres of rise.</summary>
public class StraightSkeletonTests
{
    private static List<Vector2> Rect(float w, float h) => new() { new(0, 0), new(w, 0), new(w, h), new(0, h) };

    /// <summary>The T made of [0,10]x[6,10] and [3,7]x[0,10].</summary>
    private static readonly List<Vector2> T = new() { new(3, 0), new(7, 0), new(7, 6), new(10, 6), new(10, 10), new(0, 10), new(0, 6), new(3, 6) };

    private static bool Has(RoofFace face, float x, float y, float height) =>
        face.Ring.Any(v => v.Point.DistanceTo(new Vector2(x, y)) < 0.001f && Mathf.Abs(v.Height - height) < 0.001f);

    private static float Highest(IReadOnlyList<RoofFace> faces) => faces.Max(f => f.Ring.Max(v => v.Height));

    [Fact]
    public void A_rectangle_has_four_faces_with_a_ridge_between_the_long_ones()
    {
        var faces = StraightSkeleton.Compute(Rect(4f, 2f));

        Assert.Equal(4, faces.Count);
        var bottom = faces.Single(f => f.Edge == 0);
        var rightEnd = faces.Single(f => f.Edge == 1);
        Assert.Equal(4, bottom.Ring.Count);
        Assert.True(Has(bottom, 1f, 1f, 1f));
        Assert.True(Has(bottom, 3f, 1f, 1f));
        Assert.Equal(3, rightEnd.Ring.Count);
        Assert.True(Has(rightEnd, 3f, 1f, 1f));
        Assert.Equal(1f, Highest(faces), 3);
    }

    [Fact]
    public void A_square_has_four_triangles_meeting_at_the_centre()
    {
        var faces = StraightSkeleton.Compute(Rect(2f, 2f));

        Assert.Equal(4, faces.Count);
        Assert.All(faces, f => Assert.Equal(3, f.Ring.Count));
        Assert.All(faces, f => Assert.True(Has(f, 1f, 1f, 1f)));
    }

    [Fact]
    public void An_L_has_six_faces_with_a_valley_from_the_inner_corner_to_the_ridges()
    {
        var faces = StraightSkeleton.Compute(RoofOutlineTests.L);

        Assert.Equal(6, faces.Count);
        Assert.Equal(2f, Highest(faces), 3);
        var innerHorizontal = faces.Single(f => f.Edge == 2);
        Assert.True(Has(innerHorizontal, 4f, 4f, 0f));
        Assert.True(Has(innerHorizontal, 2f, 2f, 2f));
        Assert.True(Has(innerHorizontal, 8f, 2f, 2f));
        var left = faces.Single(f => f.Edge == 5);
        Assert.True(Has(left, 2f, 2f, 2f));
        Assert.True(Has(left, 2f, 8f, 2f));
    }

    [Fact]
    public void A_T_has_eight_faces()
    {
        var faces = StraightSkeleton.Compute(T);

        Assert.Equal(8, faces.Count);
        Assert.Equal(2f, Highest(faces), 3);
        Assert.All(faces, f => Assert.True(f.Ring.Count >= 3));
    }

    [Fact]
    public void Every_ring_starts_with_its_own_edge_at_height_zero()
    {
        var poly = RoofOutlineTests.L;

        var faces = StraightSkeleton.Compute(poly);

        foreach (var face in faces)
        {
            Assert.Equal(poly[face.Edge], face.Ring[0].Point);
            Assert.Equal(poly[(face.Edge + 1) % poly.Count], face.Ring[1].Point);
            Assert.Equal(0f, face.Ring[0].Height);
            Assert.Equal(0f, face.Ring[1].Height);
        }
    }

    [Fact]
    public void Degenerate_input_gives_no_faces()
    {
        Assert.Empty(StraightSkeleton.Compute(new List<Vector2> { new(0, 0), new(1, 0) }));
        Assert.Empty(StraightSkeleton.Compute(new List<Vector2> { new(0, 0), new(1, 0), new(2, 0) }));
    }
}
