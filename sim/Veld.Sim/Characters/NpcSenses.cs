using System;
using Godot;

namespace Veld.Sim.Characters;

/// <summary>What the body reports to its brain in one tick. CanSeePlayer is a delegate so the raycast runs only when the brain asks.</summary>
public readonly record struct NpcSenses(
    Vector3 Position,
    Vector3 Right,
    Vector3 PlayerPosition,
    bool PlayerDead,
    bool PlayerMounted,
    float PlayerSpeed,
    Func<bool> CanSeePlayer,
    float Speed,
    bool MagEmpty,
    bool CanFire);
