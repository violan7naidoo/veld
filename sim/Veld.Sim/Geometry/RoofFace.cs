using System.Collections.Generic;
using Godot;

namespace Veld.Sim.Geometry;

/// <summary>A point on a roof face and its height, the inward offset distance from the eaves; the caller scales it by the pitch.</summary>
public readonly record struct RoofVertex(Vector2 Point, float Height);

/// <summary>One roof face: the outline edge it rises from and its ring of points, counter-clockwise seen from above, starting with that edge at height zero.</summary>
public sealed record RoofFace(int Edge, IReadOnlyList<RoofVertex> Ring);
