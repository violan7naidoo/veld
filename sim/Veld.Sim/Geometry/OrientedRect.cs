using Godot;

namespace Veld.Sim.Geometry;

/// <summary>Bounding rectangle aligned with a polygon's longest edge: centre, unit axis along the long side, and the two half extents.</summary>
public readonly record struct OrientedRect(Vector2 Center, Vector2 Axis, float HalfLong, float HalfShort);
