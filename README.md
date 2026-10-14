# Veld

An open-world game set in Montford, Unit 7 of Chatsworth, Durban, in the present day. Third-person
on foot and behind the wheel, in a suburb built from real map data: every street, 14,000 building
outlines, the sports fields, Chatsworth Stadium, the cricket oval, the schools, the shopping centre,
the police station, streams, fences and walls, on real hills from satellite elevation data. Residents
with dialogue, missions, police and a wanted system, day and night, weather, save and load.

It is built to be **edited**. The whole world comes from JSON files in `data/maps/chatsworth-unit7/`,
every character is a small recipe of boxes in code, and each system lives in one short C# file. The art
is placeholder geometry you are meant to replace.

Characters and missions are invented. Streets, fields, schools, the stadium, the police station and the
shopping centre are real public places and keep their names. Shops get generic trade signs, not real
business names.

## Requirements

- **Godot 4.7.2 .NET** (the "mono" build). A copy was downloaded to
  `C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\`. Other 4.x .NET builds should work
  if you change the SDK version at the top of `Veld.csproj`.
- **.NET SDK 8 or newer** (9.0 is installed on this machine).

## Run it

Double-click `play.cmd`. It builds the C# code and starts the game without the editor.

From the editor instead:

1. Start `Godot_v4.7.2-stable_mono_win64.exe`, click **Import**, pick `D:\Violan\VDocuments\Veld\project.godot`.
2. Press **F5** (Run Project). The first run builds the C# assembly.

From a terminal:

```
cd D:\Violan\VDocuments\Veld
dotnet build
"C:\Users\Damian work\Downloads\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64.exe" --path .
```

## Controls

| Key | On foot |
| --- | --- |
| W A S D | move (relative to the camera) |
| Shift | sprint |
| Space | jump |
| Mouse | look |
| Right mouse | aim (over the shoulder) |
| Left mouse | shoot (rifle: one shot per click; pistol: fires while held) |
| R | reload |
| 1 / 2 | 9 mm pistol / hunting rifle |
| X | holster |
| E | talk, get into a car, search a body |
| H | have your car brought round to you (R20) |
| M | full map: scroll to zoom, drag to pan, click to set a waypoint, right-click to clear |
| Q | slow motion while aiming; drains the pale meter |
| T | skip two hours |
| F12 | screenshot to `%APPDATA%\Godot\app_userdata\Veld\screenshots\` |
| Esc | pause menu (save, load, settings, quit) |

In a conversation, click a choice or press its number.

**Driving.** Your own car is parked on the road a few metres ahead of where you start, and it is marked
on the minimap and map. Walk up to any car or minibus taxi with a prompt and press E to get in.

| Key | In a vehicle |
| --- | --- |
| W / S | accelerate / brake, then reverse |
| A / D | steer |
| Space | handbrake |
| H | horn |
| E | get out |
| Mouse | look around; the camera settles behind the car when you leave the mouse alone |

Headlights come on at night, brake lights when you brake. Every fifth parked car in the suburb can be
taken, and one in four of those is a minibus taxi. Running into people counts as an attack and the
police will come.

**Lost your car?** Press M: it is the white square marked "Your car", and the minimap shows it as a
white square too. Or press **H** anywhere on foot and a mate drops it off beside you for R20. Any other
car with a "Get in" prompt is yours to take.

## What is in the world

- **The streets**: Road 701 to Road 753 and the rest, 130 named roads of tar with markings, street
  lights, pavements and kerbs, 420 parked cars, and precast boundary walls with gate pillars in front of
  the houses. The HUD names the house, road and suburb you are standing at.
- **Addresses**: every house and building carries its real municipal address, with the number on a
  plate beside the front door. See [Addresses and real places](#addresses-and-real-places).
- **Places**: Chatsworth Stadium, Chatsworth Cricket Oval, Gandhi Memorial Park, the shopping centre,
  Chatsworth Police Station, the magistrates court, the fire station, the post office, nine schools
  (Brindhavan Secondary, Arena Park Secondary, Meadowlands Technical High, and the Sunnyvale, Dawnridge,
  Belvedere, Beacon Ridge, Seven Hills and Ays Memorial primaries), filling stations, superettes, salons,
  spares shops and the animal clinic. Sports fields get goal posts, a cricket strip or basketball hoops.
- **People**: Aunty Devi at the superette (first mission giver; her bunny chow heals you), Kyle playing
  football, Mrs Naicker your neighbour on Road 721, Sipho the taxi driver at the rank, Mr Ndlovu the
  security guard at the shopping centre, Coach Naidoo at the stadium (second mission giver), Thandeka at
  the basketball court, Mr Govender the teacher at Brindhavan Secondary, Uncle Raj on his bench in
  Gandhi Memorial Park, and Constable Pillay, Sergeant Mthembu and Constable Singh of the SAPS.
- **Missions**: *Aunty Devi's Parcel* (R150) and *The Stolen Kit* (R800).
- **Systems**: factions (residents, police, gang), reputation, wanted level (armed officers shoot on
  sight until it decays, or any officer will take an R500 admission-of-guilt fine), rands and cents,
  stamina, day and night with sun, moon, fog and sky colours, five weather states from clear to
  thunderstorm with wet roads and wind in the grass, and driving on real vehicle physics.
- **Start**: 07:30 on Road 721 outside number 42, your house, with R250, a pistol, a hunting rifle and
  your car parked just ahead.

## Addresses and real places

Every building has the street number, street and suburb the municipality gives it: 14,169 of the
14,405 buildings on the map, and every one inside Montford. The number hangs on a plate beside the
front door, the HUD reads "42 Road 721, Montford" when you stand at a house and "Road 721, Montford"
on the road, the full map shows numbers when you zoom right in, and its search box takes "42 Road
721" (or just "42 721") and drops a waypoint on the house. Chatsworth's numbered roads are written the
way the municipality and residents write them, "Road 721".

Public places carry their real names: the schools, the temples, mosques and churches, Montford
Library, Montford Community Hall, the Arena Park pool, the clinic, R K Khan Hospital, the shopping
centres, the police and fire stations. Private shops still get generic signs.

The data comes from eThekwini Municipality's open GIS portal (address points, zoning parcels, suburb
boundaries and facility layers), fetched by `tools/ethekwini_fetch.py` into
`tools/source/montford_ethekwini.json` and merged by the map converter. OpenStreetMap carries only
four house numbers for the whole suburb, which is why the municipal data is needed. To refresh it:

```
python tools/ethekwini_fetch.py tools/source/montford_elevation.json tools/source/montford_ethekwini.json
python tools/osm_to_map.py tools/source/montford_osm.json tools/source/montford_elevation.json data/maps/chatsworth-unit7 tools/source/montford_ethekwini.json
```

To correct one house, put `"number"` and `"street"` on its entry in `overrides.json`.

## Project layout

```
project.godot            Godot project (main scene, autoload, physics layers)
Veld.csproj              C# project (Godot.NET.Sdk)
play.cmd                 build and run without the editor
open-editor.cmd          open the project in the Godot editor
data/
  config.json            which map folder to load
  maps/chatsworth-unit7/
    world.json           roads, areas, buildings with addresses, lines, points of interest, suburbs and settings (generated)
    heightmap.json       elevation grid (generated)
    npcs.json            who lives where, patrols, weapons, dialogue ids
    dialogue.json        conversation trees with conditions and actions
    missions.json        missions, steps, rewards, NPCs spawned per mission
    weapons.json         weapon stats
    towns.json           optional authored props (empty: everything comes from the map data)
    overrides.json       per-building roof, wall, facade photo or 3D model, keyed by OpenStreetMap id
    models/              3D models referenced by overrides.json
scenes/                  four tiny scenes (Main, Player, Npc, Horse); everything else is built in code
scripts/
  Core/      Game.cs (autoload: money, reputation, save/load)  Main.cs (builds the scene)
             Data.cs (the JSON schema)  InputSetup.cs (keys)  Layers.cs  DevSmoke.cs (automated test)
  World/     WorldBuilder.cs  Terrain.cs  RoadBuilder.cs (roads, markings, street lights, parked cars)
             FootprintBuilder.cs (houses from outlines)  Dressing.cs (fields, fences, streams, signs)
             AddressPlates.cs (number plates by the doors)  AddressIndex.cs (which house is this, where is 42 Road 721)
             Vegetation.cs  DayNight.cs  Weather.cs  MapImage.cs (map and location names)
             Tex.cs (generated textures)  Props.cs (box/cylinder/sign helpers and the colour palette)
             TownBuilder.cs, Railway.cs (authored props; not used by this map)
  Characters/ Player.cs  Npc.cs  HumanoidModel.cs  CameraRig.cs  Faction.cs  Horse.cs (not used by this map)
  Gameplay/  Vehicle.cs (cars and taxis)  Weapon.cs (hitscan + effects)  MissionManager.cs
             DialogueSystem.cs  WantedSystem.cs  Interactable.cs
  UI/        Hud.cs  Minimap.cs  WorldMap.cs  DialogueBox.cs  PauseMenu.cs
  Audio/     Sfx.cs (procedural placeholder sounds)
tools/                   Python pipeline that turns OpenStreetMap, elevation and eThekwini municipal data into a map folder
docs/
  design.md              setting, tone and what to handle with care
  editing-guide.md       recipes: add a person, a conversation, a mission, a building type, swap models, etc.
  real-houses.md         making the suburb look like the real one: aerial photo, facade photos, 3D models
  rdr2-systems.md        catalogue of open-world systems and which ones exist here
```

## Editing

Start with `docs/editing-guide.md`. The short version:

- **Change the world without touching code**: edit the JSON in `data/maps/chatsworth-unit7/`. Positions
  are metres from the map centre (`[x, z]`, x east, z south; the map is 3100 m across). Comments are
  allowed in the files. Walk somewhere in the game and press F12: the screenshot's name carries the
  position.
- **Change how something behaves**: each system is one file, listed above. Search for the string you
  saw on screen and you will land in the right place.
- **Change one house**: add its OpenStreetMap id to `overrides.json` with `"levels"`, `"wall"`,
  `"roofKind"`, `"roofColor"`, a `"facade"` photo or a `"model"`. The id is in `world.json` or on
  openstreetmap.org when you click the building.
- **Replace the blocky models**: `HumanoidModel.cs`, `Vehicle.cs` and `FootprintBuilder.cs` are the only
  places that build geometry for people, cars and houses. Drop a `.glb` into the project and instantiate
  it there instead; the rest of the code only calls `Configure`, `Animate`, `SetAiming`, `SetDead` and
  `ShowWeapon`. Free CC0 sets that fit: Kenney, Quaternius, Kay Lousberg (KayKit).
- **Replace the sounds**: `Sfx.cs` generates its sounds. Put `.wav`/`.ogg` files under `res://audio`
  and load them in `_Ready` instead.

## The map data

The suburb is converted from OpenStreetMap and NASA SRTM elevation by the scripts in `tools/`. Map data
© OpenStreetMap contributors, ODbL 1.0 (openstreetmap.org/copyright). Elevation: NASA SRTM via AWS
Terrain Tiles. Addresses, zoning, suburbs and public places © eThekwini Municipality, Open GIS Data
(gis-ethekwini.opendata.arcgis.com; accuracy not guaranteed). Google Maps imagery and data cannot be
used in a game, which is why the map is built from OpenStreetMap; it carries the same real road layout.

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
  `"levels": 2` or `"kind": "house"` to one, or an entry in `overrides.json`, and it is rebuilt that way.
  OpenStreetMap itself accepts `building:levels`, `roof:shape` and `roof:colour` tags, and re-running
  the converter picks them up.
- **Model your own street.** For the few houses that matter most, a model made in Blender from photos
  can replace the generated box: point to it from `overrides.json`. Try it with the included
  `models/example_house.tscn`.
- What is not possible: using Google's Street View or 3D buildings in a game, which their terms forbid.

The full step-by-step plan, with the data sources, the licences to check, and the file formats the
game reads (`ortho.png` aerial photo, `overrides.json` per-house roof/wall/facade/model), is in
[docs/real-houses.md](docs/real-houses.md).

### Regenerate or extend the map

```
python tools/osm_to_map.py tools/source/montford_osm.json tools/source/montford_elevation.json data/maps/chatsworth-unit7 tools/source/montford_ethekwini.json
```

To map a different suburb, edit the bounding box and street-name pattern at the top of
`tools/osm_fetch.py`, run it, fetch elevation with `tools/terrarium.py`, fetch the municipal layers
with `tools/ethekwini_fetch.py` (anywhere inside eThekwini), convert with `osm_to_map.py` into a new
folder under `data/maps/`, copy the character files across as a start, and point `data/config.json`
at the new folder. `docs/editing-guide.md` has the details. The quality of a map
depends on how well the area is mapped in OpenStreetMap; building heights and roof shapes are guessed
from the footprint size unless the data has them.

## Graphics

Everything you see is generated at startup; there are no texture image files in the project.

- **Textures** are made by `scripts/World/Tex.cs` (soil, rock, tar, house wall, face brick, roof tiles,
  concrete, precast panels, corrugated iron, a front door, grass blades, leaves, stars, clouds). Each is
  a small function; change the numbers or swap the body for `GD.Load<Texture2D>("res://textures/yours.png")`.
- **Shaders** live in `shaders/`: `terrain.gdshader` (slope-based detail over the vertex tint),
  `grass.gdshader` (swaying tufts that bend around you), `leaf.gdshader` (canopies and shrubs),
  `clouds.gdshader`.
- **Sky, sun, moon, fog, global illumination, ambient occlusion, volumetric light, clouds, stars, street
  lights and lit windows** are all in `scripts/World/DayNight.cs`. Rain, wet roads, thunder and wind are
  in `scripts/World/Weather.cs`.
- **Materials for buildings** come from `FootprintBuilder.cs` and `Props.cs`; `Props.Lantern` makes a
  lamp that comes on at night and `Props.Glass` a window that glows.
- **Quality**: Esc → Graphics quality. *Low* turns off global illumination and shortens shadows,
  *Medium* adds SDFGI and ambient occlusion, *High* adds volumetric light, moon shadows and TAA.
  The choice is saved in `user://settings.cfg` with mouse and volume settings.

## Checking that nothing broke

```
dotnet build
dotnet test tests/Veld.Tests
"...\Godot_v4.7.2-stable_mono_win64_console.exe" --path . -- --smoke --shot=C:/temp/veld
```

`dotnet test` runs the xUnit tests under `tests/Veld.Tests` against the game's pure logic (money
formatting, faction names). They reference the game assembly but never start the engine, so anything
that needs a scene is covered by the smoke run below instead.

`--smoke` walks, shoots, switches weapons, gets into the car and drives it, calls it back with H, talks
to Aunty Devi, runs the parcel mission from start to finish, provokes the police to trigger the wanted
system, saves, loads, toggles the pause menu and the map, advances the clock to sunset and forces a
thunderstorm, printing `PASS`/`FAIL` lines and exiting non-zero on any failure. `--shot=prefix` also
saves screenshots along the way. `--quality=0|1|2` picks the graphics tier for the run.

`-- --carstress=40` is the car physics check: it gets into forty parked cars around the suburb, waits
a second in each with no input, gets out and in again, and fails any car that flips, lifts, sinks
into the ground or bolts. Run it after touching the vehicle code.

## Known limits (this is a prototype)

- Placeholder geometry and procedural sounds; no animation rig, no facial anything.
- Every house is a guess from its outline. See `docs/real-houses.md` for how to make them real.
- No navigation mesh: NPCs walk straight at their target and sidestep when stuck.
- One save slot (`user://save.json`). NPC and car positions are not saved, only who is dead.
- Shops are signs; the only trade is Aunty Devi's food. No animals yet.
- Two missions. New ones are a JSON entry each; new step types are a case in `MissionManager`.
