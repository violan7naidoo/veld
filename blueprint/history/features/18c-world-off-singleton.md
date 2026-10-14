# Feature: Retire the Game singleton, part c: World and audio off the singleton

**From build-plan:** feature 18c
**Status:** complete (2026-10-14)
**Branch:** `feature/world-off-singleton`

## Goal

The world builders, the sky, the weather and the sound node stop reaching for
`Game.Instance`. Each receives what it reads through a `Configure` or `Build`
parameter from the node that creates it, and the set of dead characters moves
from `Game` into `WorldBuilder`, which is the only thing that spawns them. The
13 call sites across `scripts/World/` and `scripts/Audio/` go to zero, the
project count drops from 87 to 74, and two more members leave `Game`: the
`DeadNpcs` set and the `Volume` forwarder. The game plays identically: smoke
run 46 of 46, 107 tests untouched.

## In scope

- `Terrain.Generate` and `FootprintBuilder.Build` take the map folder as a parameter; `WorldBuilder` passes `data.MapFolder`.
- `RoadBuilder.Ribbon` loses its `Game` fallback: a caller passes either a height function or a terrain, and the three internal calls that passed neither now pass the builder's own terrain.
- `WorldBuilder.DeadNpcs` replaces `Game.DeadNpcs`; `SaveGame` reads it through `Main.World` and drops the constructor parameter; the one `Npc` line that adds to the set reaches it through `Game.Instance.World` for now (18e moves it onto a field).
- `DayNight.Configure` takes the graphics quality; `DayNight.World` is a property `Main` sets once the world is built, mirroring the existing `DayNight.Weather`.
- `Weather.Configure` takes the `DayNight` and the `Sfx` it reports to.
- `Sfx.Configure` takes the starting volume.
- `Main` updated for all of the above; its own `Game.Instance` uses stay (18f).
- Forwarder `Volume` deleted from `Game` (its only caller was `Sfx`); the `DeadNpcs` property deleted from `Game`.

## Out of scope

- `Main.cs`, `DevSmoke`, `DevCarStress` and the live adapters (18f).
- `Player`, `CameraRig`, `Npc`, `Horse`, `Vehicle` beyond the one `DeadNpcs` line (18d, 18e).
- The `GraphicsQuality` forwarder on `Game`: `DevSmoke` still reads it, so it stays until 18f.
- Resolving the facade and ortho paths at data-load time instead of in the builders; a later cleanup.
- Unit tests: every change swaps a static read for a parameter inside a Godot node or a mesh helper; the smoke run and screenshots are the evidence.

## Build loop

Build one step at a time, never the whole feature at once.

1. Plan mode lays out the step before any code.
2. The AI implements just that step.
3. It shows the diff (not full files); you read it and understand it.
4. You approve, then choose whether to commit a checkpoint or roll straight on.

## Build steps

- [x] **Step 1 - Map folder and terrain into the builders** - `Terrain.Generate(..., string mapFolder)` with `ApplyOrtho` taking the folder; `FootprintBuilder.Build(world, terrain, mapFolder, plates)` with `MapPath` reading a field; `WorldBuilder` passes `data.MapFolder` to both; `RoadBuilder.Ribbon` reads `terrain!` when no height function is given and the three calls on its lines 41 to 43 pass `_terrain`. *Done when:* `dotnet build` is clean, `grep -c "Game.Instance"` is 0 in `Terrain.cs`, `RoadBuilder.cs` and `FootprintBuilder.cs` and 84 across `scripts/`, the smoke run passes 46 of 46 including "number plates registered" and the "addresses:" check, and the `1-spawn` screenshot from a `--shot` run shows the tarred road with its yellow centre line and kerbs as before.
- [x] **Step 2 - WorldBuilder owns the dead** - `public HashSet<string> DeadNpcs { get; } = new();` on `WorldBuilder`, its two `Contains` calls read it; `SaveGame` loses the `deadNpcs` constructor parameter and reads `Main.World.DeadNpcs` in `Save` and `Load`; `Game` loses the property and the constructor argument; `Npc.cs` line 149 becomes `Game.Instance.World!.DeadNpcs.Add(Data.Id)`. *Done when:* `dotnet build` is clean, `DeadNpcs` no longer appears in `Game.cs`, the count is 0 in `WorldBuilder.cs` and 82 across `scripts/`, and the smoke run passes 46 of 46 including "saved to", "loaded save" and "save restored money and health".
- [x] **Step 3 - DayNight, Weather and Sfx** - `Sfx.Configure(float volume)` applied in `_Ready`; `DayNight.Configure(float startHour, float dayLength, Quality quality)` applied in `_Ready`, plus `public WorldBuilder? World { get; set; }` read where the two shader parameters are set; `Weather.Configure(string biome, int seed, DayNight dayNight, Sfx sfx)` with the dead `dn == null` arm dropped; `Main` passes `game.Settings.Volume` and `game.Settings.GraphicsQuality`, hands `DayNight` and `Sfx` to `Weather`, and sets `DayNight.World = World` after `World.Build`; the `Volume` forwarder deleted from `Game`. *Done when:* `dotnet build` is clean, `Volume` no longer appears in `Game.cs`, the count is 0 across `scripts/World/` and `scripts/Audio/` and 74 across `scripts/`, the smoke run passes 46 of 46 including "fps 60 while moving, quality High", "clock advanced to" and "weather forced to a thunderstorm", and the `4-sunset` and `5-night` screenshots from a `--shot` run show the low sun and the lit lamps respectively.

## Files / areas

- `scripts/World/Terrain.cs`, `RoadBuilder.cs`, `FootprintBuilder.cs`, `WorldBuilder.cs`, `DayNight.cs`, `Weather.cs`
- `scripts/Audio/Sfx.cs`
- `scripts/Core/Main.cs` (configure calls; its own `Game.Instance` uses untouched)
- `scripts/Core/Game.cs` (`DeadNpcs` property and `Volume` forwarder removed, `SaveGame` call shortened)
- `scripts/Core/SaveGame.cs` (constructor and the two reads)
- `scripts/Characters/Npc.cs` (one line)

## Data / contracts

Load-bearing for 18e, which gives `Npc` the `WorldBuilder` it adds its id
to, and for 18f, which calls these from `Main` with objects `Main` then owns.

```csharp
namespace Veld.World;

public partial class Terrain : Node3D
{
    public void Generate(WorldData world, HeightmapData? heightmap, List<Flatten> flats, List<Strip> strips, List<AreaFill> areas, List<Vector2> built, string mapFolder);
}

public partial class FootprintBuilder : Node3D
{
    public void Build(WorldData world, Terrain terrain, string mapFolder, AddressPlates? plates = null);
}

public partial class RoadBuilder : Node3D
{
    /// <summary>Pass a height function or a terrain; the one you give is used.</summary>
    public static int Ribbon(SurfaceTool st, List<Vector2> pts, float width, float lift, float vMetres, bool markings, Func<float, float, float>? height = null, Terrain? terrain = null, float offset = 0f);
}

public partial class WorldBuilder : Node3D
{
    /// <summary>Ids of characters who have died; respected on spawn and on load.</summary>
    public HashSet<string> DeadNpcs { get; }
}

public partial class DayNight : Node3D
{
    public void Configure(float startHour, float dayLength, Quality quality);
    public WorldBuilder? World { get; set; }     // set by Main after the world is built
}

public partial class Weather : Node3D
{
    public void Configure(string biome, int seed, DayNight dayNight, Sfx sfx);
}

namespace Veld.Audio;

public partial class Sfx : Node3D
{
    public void Configure(float volume);         // applied in _Ready
}

namespace Veld.Core;

public sealed class SaveGame
{
    public SaveGame(Purse purse, Reputation reputation, MissionManager missions, WantedSystem wanted, INotifier notifier);
}
```

`Ribbon`'s trailing `offset` parameter already exists; it is shown so the
signature is complete. Every `Configure` is called before `AddChild`, so
`_Ready` can rely on it.

## Testing

The test gate is on, but no step here carries logic a unit test could reach:
each change swaps a static read for a parameter inside a Godot node or the
mesh helper. The suite must stay at 107 green. Evidence per step is the smoke
run with `--shot`:

    "...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --shot=<scratch dir>/veld

Screenshots named in the done-whens are viewed, not just produced. Two lines
are not exercised by this map and ride on the build alone: the ortho path in
`Terrain` (`world.json` has `"ortho": ""`) and `MapPath` in
`FootprintBuilder` unless `overrides.json` names a facade or model;
`/implement` says which applies when it reports step 1.

Manual path: start the game; the road at the spawn has its yellow line and
kerbs; walk until sunset and the lamps come on; press T to skip time and the
sky follows; wait for or force rain and the rain sound rises; save, quit,
load, and any character you killed stays dead.

## Notes for the AI

- Change nothing but the source of each value. Lift each line as it is; `Game.Instance.X` becomes a parameter, a field, or a property, nothing else moves.
- `DayNight.World` stays nullable with its null check because it is assigned after construction, like `DayNight.Weather`. `Weather`'s `DayNight` and `Sfx` arrive in `Configure` and are never null, so the `dn == null` arm goes.
- `Ribbon` keeps its optional parameters so `Dressing.cs` compiles unchanged; only the fallback expression changes.
- `Sfx.Configure` stores the value and `_Ready` applies it, because `AudioServer` is set up by then and `Configure` runs before `AddChild`.
- Delete a member from `Game` only when `grep` shows no caller left; the done-whens name which step deletes which. `GraphicsQuality` stays.
- Count with `grep -rc "Game.Instance" scripts/ | awk -F: '{s+=$2} END {print s}'` and quote the number.
- British spelling in player-facing text, no em dashes, no new `Game.Instance` call sites anywhere.
- No AI attribution in commit messages; ask before committing.
