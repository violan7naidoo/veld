# Feature: Roofs for any outline

**From build-plan:** feature 21
**Status:** complete (2026-10-18)
**Branch:** `feature/skeleton-roofs`

## Goal

Houses whose outline is not a rectangle get a proper hipped roof instead of
the flat grey slab they wear today: the L, T, U and notched shapes that are
1,130 of the map's 14,405 buildings. The roof is the straight skeleton of the
outline at the same pitch, eave overhang and tile scale as the rectangle hip,
so a rectangle's roof is unchanged and an L's roof is two wings meeting in a
valley, as the real houses do. The geometry is pure and tested in the
library; `FootprintBuilder` only turns faces into mesh. Outlines too odd to
roof keep the flat roof, and a roof that cannot be computed falls back to it
rather than failing the build.

## In scope

- `sim/Veld.Sim/Geometry/`, namespace `Veld.Sim.Geometry`: `Polygons` (`SignedArea`, `Centroid`, `OrientedRect`) moved out of `FootprintBuilder` with the oriented rectangle as a record struct rather than a tuple; `RoofOutline` (`Simplify`, `Offset`, `IsRoofable`); `StraightSkeleton.Compute`, which turns a simple polygon into roof faces with a height at every vertex.
- `FootprintBuilder`: a `SkeletonRoof` path for outlines that are not rectangular but are roofable, between the rectangle hip and the flat fallback; the same rise rule, overhang, tile scale, darkened underside, collision faces and satellite dish as the rectangle hip; two static counters, roofs built and roofs that fell back.
- `DevSmoke`: one check that at least 400 non-rectangular outlines got a hip roof with no fallbacks, and a seventh screenshot at 34 Road 720 (an L-shaped house 53 m from the spawn) when `--shot` is given.
- `Dressing` and `AddressIndex` follow the moved helpers.
- `coding-standards.md`: the layout line names `Geometry/`; the seams bullet says pure geometry lives in the library and the world builders assemble meshes.

## Out of scope

- Any change to rectangular roofs: the existing `HipRoof` path and its 0.84 area test stay exactly as they are, so 12,172 houses render as before.
- Gables, mixed pitches, dormers, roofs on big or extension buildings, roofs on curved or spiky outlines. The gate below says which outlines qualify; the rest stay flat.
- Landmark models (item 22) and per-building roof overrides beyond the existing `roofKind` and `roofColor`.
- Roof textures, lighting or the rendering of eaves.
- Changing the world data or the converter.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Polygon helpers into the library** - `Polygons.SignedArea`, `Centroid` and `OrientedRect` (returning the `OrientedRect` record struct) in `sim/Veld.Sim/Geometry/`, with `tests/Veld.Tests/Sim/Geometry/PolygonTests.cs` written red first; `FootprintBuilder`, `Dressing` and `AddressIndex` call them there and read the struct's named fields. *Done when:* `dotnet build` clean, `dotnet test tests/Veld.Tests` green at 171 plus the four polygon tests, the Sim fence prints nothing, `grep -rnE "static .*(SignedArea|Centroid|OrientedRect)\(" scripts/` finds nothing, and the smoke run passes 46 of 46.
- [x] **Step 2 - Preparing an outline** - `RoofOutline.Simplify`, `Offset` and `IsRoofable` with `RoofOutlineTests.cs` written red first. Nothing in the game changes. *Done when:* build clean, tests green at the step 1 count plus six, the fence prints nothing, no change under `scripts/`.
- [x] **Step 3 - The straight skeleton** - `StraightSkeleton.Compute` with `StraightSkeletonTests.cs` written red first. Nothing in the game changes. *Done when:* build clean, tests green at the step 2 count plus six, the fence prints nothing, no change under `scripts/`.
- [x] **Step 4 - The roof on the house** - `FootprintBuilder.SkeletonRoof`, the gate between the rectangle hip and the flat fallback, the counters, the smoke check and the seventh screenshot. *Done when:* build clean, tests green, `git diff main -- scripts/World/FootprintBuilder.cs` shows no change inside `HipRoof`, the smoke run passes 47 of 47 including "hip roofs on N non-rectangular outlines, 0 fell back" with N at least 400, and a run with `--shot=<dir>/veld` writes `veld-7-roof.png` showing the house at 34 Road 720 under a hipped roof with a valley.
- [x] **Step 5 - Standards** - the layout line and the seams bullet. *Done when:* `grep -n "Geometry" blueprint/context/coding-standards.md` shows both lines; build, tests and smoke unchanged from step 4 (no code in this step).

## Files / areas

- `sim/Veld.Sim/Geometry/Polygons.cs`, `OrientedRect.cs`, `RoofOutline.cs`, `StraightSkeleton.cs`, `RoofFace.cs` (new)
- `scripts/World/FootprintBuilder.cs`, `scripts/World/Dressing.cs`, `scripts/World/AddressIndex.cs`, `scripts/Core/DevSmoke.cs`
- `tests/Veld.Tests/Sim/Geometry/PolygonTests.cs`, `RoofOutlineTests.cs`, `StraightSkeletonTests.cs` (new)
- `blueprint/context/coding-standards.md`

## Data / contracts

Load-bearing for item 22, which will place models on the same prepared
outlines, and for any later roof style.

```csharp
namespace Veld.Sim.Geometry;

/// <summary>Bounding rectangle aligned with a polygon's longest edge.</summary>
public readonly record struct OrientedRect(Vector2 Center, Vector2 Axis, float HalfLong, float HalfShort);

public static class Polygons
{
    public static float SignedArea(IReadOnlyList<Vector2> p);        // positive when counter-clockwise
    public static Vector2 Centroid(IReadOnlyList<Vector2> p);        // the mean of the vertices, as today
    public static OrientedRect? OrientedRect(IReadOnlyList<Vector2> p);   // null when the longest edge is under 0.1 m
}

/// <summary>An outline made fit to roof: short edges and straight-through vertices dropped, the eave overhang added, and the gate.</summary>
public static class RoofOutline
{
    public static List<Vector2> Simplify(IReadOnlyList<Vector2> p, float minEdge = 0.5f, float straightDegrees = 3f);
    public static List<Vector2> Offset(IReadOnlyList<Vector2> p, float distance);   // every edge moved outward, adjacent edge lines re-intersected; input counter-clockwise
    public static bool IsRoofable(IReadOnlyList<Vector2> p);
    // 4 to 12 vertices after Simplify, every edge at least 1.2 m, and either convex or every corner within 12 degrees of 90 or 270
}

/// <summary>One roof face: the polygon edge it rises from and its ring of points with their height, the inward offset distance.</summary>
public sealed record RoofFace(int Edge, IReadOnlyList<RoofVertex> Ring);
public readonly record struct RoofVertex(Vector2 Point, float Height);

public static class StraightSkeleton
{
    /// <summary>The roof faces of a simple counter-clockwise polygon, one per edge, each ring counter-clockwise seen from above; empty when the input is degenerate.</summary>
    public static IReadOnlyList<RoofFace> Compute(IReadOnlyList<Vector2> polygon);
}
```

The height in a `RoofVertex` is a distance, not a metre of rise: the caller
multiplies by the pitch. `FootprintBuilder` uses the rectangle hip's rule,
`rise = clamp(maxHeight * 0.5, 0.8, 3.5)` and `slope = rise / maxHeight`
where `maxHeight` is the largest height in the faces, so a rectangle fed
through the skeleton comes out identical to `HipRoof`. The eave overhang is
`Offset(outline, 0.45)` before the skeleton, as the hip extends its
rectangle by 0.45 on every side. UVs run along the face's base edge in
metres and up the slope at one tile per 0.4 m, as the hip does. The
underside is the offset polygon facing down in the darkened tint. Every
face is triangulated in plan with `Geometry2D.TriangulatePolygon`, the
vertices lifted by their height; faces can be non-convex.

The gate in `AddBuilding`, in order: the model override, then `big`,
`extension`, `carport`, `ruin` and `roofKind == "flat"` keep today's
paths; a rectangular outline keeps `HipRoof`; otherwise `IsRoofable`
over both the simplified outline and its eave-widened form chooses the
skeleton roof, so a shape the offset would ruin never reaches it; anything
else is flat. A skeleton that returns no faces, or throws, counts as a fallback and
the house gets the flat roof. Both counters are static on
`FootprintBuilder`, like `ModelsPlaced`.

## Testing

The test gate is on. Steps 1 to 3 are pure logic and ship their tests red
first at the library seams; step 4 is mesh assembly and rides on the smoke
check, the counters and the screenshot.

- `PolygonTests`, four facts: the signed area of a counter-clockwise 3 by 2 rectangle is 6 and negative clockwise; the centroid of the unit square's corners is (0.5, 0.5); the oriented rectangle of a 4 by 2 rectangle rotated 30 degrees has its axis along the long edge and halves 2 and 1 (within 0.001); a polygon whose longest edge is under 0.1 m gives null.
- `RoofOutlineTests`, six facts: `Simplify` drops a vertex on a straight edge and a 0.3 m jog; `Offset` of the rectangle (0,0)-(4,2) by 0.45 is (-0.45,-0.45)-(4.45,2.45); the L made of [0,4]x[0,10] and [0,10]x[0,4] is roofable; a triangle with a 30 degree corner is not; a 14-vertex staircase is not; a convex pentagon is.
- `StraightSkeletonTests`, six facts: the rectangle (0,0)-(4,2) gives four faces, the ridge from (1,1) to (3,1) at height 1, the long faces four-sided and the ends three-sided; the square (0,0)-(2,2) gives four triangles meeting at (1,1) at height 1; the L above gives six faces with its highest point at height 2 and the reflex corner (4,4) joined to (2,2) at height 2; the T made of [0,10]x[6,10] and [3,7]x[0,10] gives eight faces; every ring starts at height 0 on its own edge; fewer than three points or zero area gives an empty list.

**Test seams**

- Public interface under test: `Polygons`, `RoofOutline` and `StraightSkeleton` in `Veld.Sim.Geometry`.
- Behaviour the tests prove: an outline is cleaned, widened by the eave and turned into roof faces whose heights are the inward distances, with the rectangle, square, L and T coming out as a roofer would draw them.
- External boundary, local substitute, or mock: none; pure functions over `Vector2`.
- Red command and expected failing signal: `dotnet test tests/Veld.Tests`; CS0234 on `Veld.Sim.Geometry` in step 1, CS0246 on `RoofOutline` in step 2 and on `StraightSkeleton` in step 3.
- Full Verify command after green: none declared; `dotnet build`, `dotnet test tests/Veld.Tests`, the Sim fence from `AGENTS.md`, then the smoke run.

The smoke proves the integration by count: with the map's 615 six-vertex
and 515 eight-vertex outlines, at least 400 must come through the gate and
the skeleton with no fallback. The seventh screenshot is for your eyes: the
house at 34 Road 720 (OpenStreetMap id 1124955746, centre (-92, 316)) seen
from the road, under two wings and a valley.

Manual path: `play.cmd`; walk 50 m from the spawn to 34 Road 720 and look
at the roof, then along Road 720 and Road 721 where several L-shaped houses
stand; compare with any rectangular neighbour, whose roof must look exactly
as before.

## Notes for the AI

- `StraightSkeleton` is the wavefront algorithm: every edge moves inward at unit speed, vertices slide along their angle bisectors, and the two events are an edge collapsing to a point and a reflex vertex hitting an opposite edge (a split). Process events in height order from a priority queue; each event closes faces or splits the active polygon. Keep it to simple polygons, no holes. Numerical guards: treat lengths under 0.001 and times within 0.0005 as equal.
- Rings of a face are the points the face's edge swept: its two input vertices at height 0 and the skeleton nodes above, in counter-clockwise order seen from above, so the game can triangulate them in plan.
- `Simplify` first, then `Offset`, then `IsRoofable` on both; the offset polygon is what the skeleton gets. The skeleton works in coordinates local to the outline's centroid, because a float a kilometre from the map centre cannot hold the millimetre tolerances.
- Do not touch `HipRoof` or the 0.84 rectangle test; the done-when diffs them against main.
- Collision: add every roof triangle to `chunk.Faces` as `HipRoof` does; the trimesh collider is built from that list.
- The smoke's seventh screenshot: place the player on the road in front of 34 Road 720 facing the house, like the sunset shot sets the yaw, and call `Shot("7-roof")`.
- No `Node`, `GD` or Godot geometry classes in the library; `Vector2` and `Mathf` only. The fence after every touch of `sim/`.
- Godot does not rebuild the C# project from the command line; build before every run.
- British spelling in player-facing text, no em dashes.
- No AI attribution in commit messages; ask before committing.
