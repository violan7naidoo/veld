# Veld

An original open-world frontier game prototype set in South Africa during the Second Boer War
(the game clock reads April 1901). Third-person riding and shooting, a Karoo dorp with a British
garrison, a railway loop with blockhouses and an armoured train, a farm held by outlaws, townsfolk
with dialogue, two missions, a wanted system, day and night, save and load.

It is built to be **edited**. The whole world comes from six JSON files in `data/`, every building
and character is a small recipe of boxes in code, and each system lives in one short C# file.
Nothing here is copied from any commercial game; the art is placeholder geometry you are meant to
replace.

## Requirements

- **Godot 4.7.2 .NET** (the "mono" build). A copy was downloaded to
  `C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\`. Other 4.x .NET builds should work
  if you change the SDK version at the top of `Veld.csproj`.
- **.NET SDK 8 or newer** (9.0 is installed on this machine).

## Run it

1. Start `Godot_v4.7.2-stable_mono_win64.exe`, click **Import**, pick `D:\Violan\VDocuments\Veld\project.godot`.
2. Press **F5** (Run Project). The first run builds the C# assembly.

From a terminal instead:

```
cd D:\Violan\VDocuments\Veld
dotnet build
"C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path .
```

## Controls

| Key | Action |
| --- | --- |
| W A S D | move (relative to the camera) |
| Shift | sprint / gallop |
| Space | jump |
| Mouse | look |
| Right mouse | aim (over the shoulder) |
| Left mouse | shoot (rifle: one shot per click, bolt action) |
| R | reload |
| 1 / 2 | Mauser rifle / Webley revolver |
| X | holster |
| E | talk, mount or dismount, search a body |
| H | whistle for your horse |
| M | full map: scroll to zoom, drag to pan, click to set a waypoint, right-click to clear |
| Q | Dead Eye: slow motion while aiming, drains the pale meter |
| T | skip two hours |
| F12 | screenshot to `%APPDATA%\Godot\app_userdata\Veld\screenshots\` |
| Esc | pause menu (save, load, settings, quit) |

In a conversation, click a choice or press its number.

**Driving (Chatsworth map).** Your own car is parked on the road a few metres ahead of where you
start, and it is marked on the minimap and map. Walk up to any car or minibus taxi with a prompt and
press E to get in.

| Key | In a vehicle |
| --- | --- |
| W / S | accelerate / brake, then reverse |
| A / D | steer |
| Space | handbrake |
| H | horn |
| E | get out |
| Mouse | look around; the camera settles behind the car when you leave the mouse alone |

Headlights come on at night, brake lights when you brake. Every fifth parked car in the suburb can
be taken, and one in four of those is a minibus taxi. Running into people counts as an attack and the
police will come.

**Lost your car?** Press M: it is the white square marked "Your car", and the minimap shows it as a
white square too. Or press **H** anywhere on foot and a mate drops it off beside you for R20, the
modern map's version of whistling for a horse. Any other car with a "Get in" prompt is yours to take.

## What is in the world

- **Driefontein**: a dorp with a Dutch Reformed church, general dealer, Royal Hotel, magistrate's
  court, smithy, chemist, houses, a well, a hitch rail, a railway station and a British garrison camp.
- **Welgevonden**: a farm 650 m south-west along the road, with a farmhouse, labourers' cottage,
  dry-stone kraal and well. The Bosman brothers hold it once the magistrate's mission starts.
- **The line**: a railway loop around the southern half of the map with corrugated-iron blockhouses
  every 420 m, each with two sentries, and an armoured train that runs continuously and will knock
  you down if you stand on the rails.
- **People**: the magistrate (mission giver), Mr. Naidoo the shopkeeper (sells cartridges), Mrs.
  O'Reilly at the hotel (a room heals you and sleeps eight hours), the dominee, the stationmaster, a
  burgher who signed the oath, a Basotho transport rider, townsfolk, and Sergeant Harris with his
  privates (second mission giver).
- **Missions**: *The Bosman Brothers* (£15) and *Dispatch for the Line* (£2).
- **Systems**: factions (civilian, British, Boer, outlaw), reputation, wanted level under martial law
  (soldiers shoot on sight until it decays or the magistrate takes a £5 fine), pre-decimal money
  (£ s d), stamina for you and the horse, day and night with sun, moon, fog and sky colours.

## Project layout

```
project.godot            Godot project (main scene, autoload, physics layers)
Veld.csproj              C# project (Godot.NET.Sdk)
data/
  world.json             map seed and size, roads, railway, vegetation, player and horse spawn
  towns.json             towns and their buildings
  npcs.json              who lives where, patrols, weapons, dialogue ids
  dialogue.json          conversation trees with conditions and actions
  missions.json          missions, steps, rewards, NPCs spawned per mission
  weapons.json           weapon stats
scenes/                  four tiny scenes (Main, Player, Horse, Npc); everything else is built in code
scripts/
  Core/      Game.cs (autoload: money, reputation, save/load)  Main.cs (builds the scene)
             Data.cs (the JSON schema)  InputSetup.cs (keys)  Layers.cs  DevSmoke.cs (automated test)
  World/     Terrain.cs  Vegetation.cs  TownBuilder.cs  Railway.cs (track + Train)  DayNight.cs
             WorldBuilder.cs  Props.cs (box/cylinder/sign helpers and the colour palette)
  Characters/ Player.cs  Horse.cs  Npc.cs  HumanoidModel.cs  CameraRig.cs  Faction.cs
  Gameplay/  Weapon.cs (hitscan + effects)  MissionManager.cs  DialogueSystem.cs  WantedSystem.cs  Interactable.cs
  UI/        Hud.cs  DialogueBox.cs  PauseMenu.cs
  Audio/     Sfx.cs (procedural placeholder sounds)
docs/
  design.md              setting, tone and historical notes
  editing-guide.md       recipes: add a town, an NPC, a mission, a building type, swap models, etc.
```

## Editing

Start with `docs/editing-guide.md`. The short version:

- **Change the world without touching code**: edit the JSON in `data/`. Positions are metres from the
  map centre (`[x, z]`, the map is 1600 m across). Comments are allowed in the files.
- **Change how something behaves**: each system is one file, listed above. Search for the string you
  saw on screen and you will land in the right place.
- **Replace the blocky models**: `HumanoidModel.cs`, `Horse.BuildModel()` and `TownBuilder.cs` are the
  only places that build geometry for people, horses and buildings. Drop a `.glb` into the project and
  instantiate it there instead; the rest of the code only calls `Configure`, `Animate`, `SetAiming`,
  `SetDead` and `ShowWeapon`. Free CC0 sets that fit: Kenney, Quaternius, Kay Lousberg (KayKit).
- **Replace the sounds**: `Sfx.cs` generates its sounds. Put `.wav`/`.ogg` files under `res://audio`
  and load them in `_Ready` instead.

## Maps

Two maps ship with the game. Pick one in `data/config.json` (`"map": "chatsworth-unit7"` or
`"driefontein-1901"`), or start with `-- --map=driefontein-1901` on the command line.

**chatsworth-unit7** is Montford, Unit 7 of Chatsworth, Durban, in the present day, built from real
map data: every street (701 Road to 753 Road and the rest), 14,000 building outlines, the sports
fields, Chatsworth Stadium, the cricket oval, the schools, the shopping centre, the police station,
streams, fences and walls, on real hills from satellite elevation data. Modern dressing: tar roads
with markings, street lights, parked cars, lit windows at night, residents in t-shirts, police,
rands. Characters and missions are invented; shop signs use generic trade names, not real business
names, and public places keep their real names.

**driefontein-1901** is the original Boer War map: a procedural Karoo with an invented dorp, farm,
railway and garrison.

Map data © OpenStreetMap contributors, ODbL 1.0 (openstreetmap.org/copyright). Elevation: NASA SRTM
via AWS Terrain Tiles. Google Maps imagery and data cannot be used in a game, which is why the map is
built from OpenStreetMap; it carries the same real road layout.

### How close to the real houses can it get?

Map data only has the outline of each building, so the game cannot know what a particular house
looks like. What it does instead is build each outline in the style of the area: face brick or
pastel plaster, concrete tile, terracotta or corrugated iron hipped roofs, flat-roofed double-storey
extensions, a front door on the street side, burglar bars, precast boundary walls with gate pillars,
satellite dishes, pavements and kerbs. The choice per house is random but fixed (the same house always
gets the same look), so a street looks like a street, not like the real one brick for brick.

To get closer:

- **Photograph the real walls.** A straight-on photo of a brick wall, a plastered wall, a roof and a
  front door, cropped to tile, dropped into a `textures` folder and loaded in `Tex.cs` in place of the
  generated ones, changes the whole suburb at once. That is the single biggest step and takes an hour.
- **Tell the game about specific houses.** `world.json` keeps every building's outline; add
  `"levels": 2` or `"kind": "house"` to one and it is rebuilt that way. OpenStreetMap itself accepts
  `building:levels`, `roof:shape` and `roof:colour` tags, and re-running the converter picks them up.
- **Model your own street.** For the few houses that matter most, a model made in Blender from photos
  can replace the generated box: match it by position in `FootprintBuilder.AddBuilding`.
- What is not possible: using Google's Street View or 3D buildings in a game, which their terms forbid.

The full step-by-step plan, with the data sources, the licences to check, and the file formats the
game reads (`ortho.png` aerial photo, `overrides.json` per-house roof/wall/facade/model), is in
[docs/real-houses.md](docs/real-houses.md).

### Make a map of any other place

```
python tools/osm_fetch.py            # edit the bbox / street-name pattern at the top first
python tools/terrarium.py tiles S W N E out.json
python tools/osm_to_map.py tools/source/<place>_osm.json tools/source/<place>_elevation.json data/maps/<id>
```

Then write `npcs.json`, `dialogue.json`, `missions.json`, `weapons.json` in the new folder (copy the
Chatsworth ones as a start) and set the map in `data/config.json`. `docs/editing-guide.md` has the details.
The quality of a real map depends on how well the area is mapped in OpenStreetMap; building heights
and roof shapes are guessed from the footprint size unless the data has them.

## Graphics

Everything you see is generated at startup; there are no image files in the project.

- **Textures** are made by `scripts/World/Tex.cs` (soil, rock, plaster, corrugated iron, planks,
  sandstone blocks, grass blades, leaves, stars, clouds). Each is a small function; change the
  numbers or swap the body for `GD.Load<Texture2D>("res://textures/yours.png")`.
- **Shaders** live in `shaders/`: `terrain.gdshader` (slope-based soil/rock detail over the vertex
  tint), `grass.gdshader` (swaying tufts), `leaf.gdshader` (canopies and shrubs), `clouds.gdshader`.
- **Sky, sun, moon, fog, global illumination, ambient occlusion, volumetric light, clouds, stars and
  the night lamps** are all in `scripts/World/DayNight.cs`.
- **Materials for buildings** come from `Props.Plaster / Corrugated / Planks / StoneBlocks / Glass`
  in `scripts/World/Props.cs`; `Props.Lantern` makes a lamp that comes on at night.
- **Quality**: Esc → Graphics quality. *Low* turns off global illumination and shortens shadows,
  *Medium* adds SDFGI and ambient occlusion, *High* adds volumetric light, moon shadows and TAA.
  The choice is saved in `user://settings.cfg` with mouse and volume settings.

## Checking that nothing broke

```
dotnet build
"...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --shot=C:/temp/veld
```

`--smoke` walks, shoots, mounts, gallops, talks to the magistrate, runs the whole Bosman mission,
triggers the wanted system, saves, loads and toggles the pause menu, printing `PASS`/`FAIL` lines, and
exits non-zero on any failure. `--shot=prefix` also saves four screenshots along the way.

## Known limits (this is a prototype)

- Placeholder geometry and procedural sounds; no animation rig, no facial anything.
- No navigation mesh: NPCs walk straight at their target and sidestep when stuck.
- One save slot (`user://save.json`). NPC positions are not saved, only who is dead.
- The train has no schedule; it loops forever at a fixed speed.
- No horse theft, no hunting, no shops beyond cartridges and a hotel room. All of these have an
  obvious place to go: a new dialogue action, a new NPC role, or a new mission step type.
