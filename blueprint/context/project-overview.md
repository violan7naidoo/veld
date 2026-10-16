# Veld - Project Overview

> An open-world game set in Montford, Chatsworth, Durban, built from real map data: the one-day prototype has become a tested codebase with its simulation split from its rendering, and is now growing towards a phone release and co-op.

> **Generated file.** Distilled by `/overview` from `blueprint/project-plan.md`
> and `blueprint/build-plan.md` on 2026-10-16. Change the plans and re-run
> `/overview`; do not hand-edit this.

## Problem

The prototype rendered the real suburb from day one, but it hung off one
global singleton, routed behaviour through string switches, had no unit tests
and ran only on Windows desktop. The refactor (build plan items 14 to 19) has
delivered on that: the singleton is gone, the game's logic lives in an
engine-free simulation library with 171 unit tests beside the smoke run, and
the save file is a snapshot of that simulation. The job now is to grow the
game towards Android and iOS and, later, multiplayer, without losing that
footing.

## Users

- The owner, as developer and first player.
- People who know Chatsworth and want to explore a recognisable version of it.
- Later: mobile players on Google Play and the App Store.

## Features

Shipped first (1 to 13 and 20), then the refactor MVP (14 to 19), then the
roadmap in build order. The headline feature is the real suburb: real
streets, real outlines, real addresses, real public places.

Shipped in the prototype:

1. **Real-world map pipeline** - OpenStreetMap and SRTM elevation converted to `world.json` and `heightmap.json` by the Python tools.
2. **Generated suburb** - terrain, roads with markings and street lights, houses from outlines, fields, fences, streams, signs, parked cars.
3. **Player on foot** - walk, sprint, jump, aim, shoot, reload, two weapons, slow-motion aiming, stamina.
4. **Driving** - cars and taxis on vehicle physics, enter and exit, chase camera, lights, horn, call the car back for R20.
5. **NPCs and factions** - residents, police and gang with wander, patrol, alert, combat and death.
6. **Dialogue** - JSON conversation trees with conditions and actions.
7. **Missions** - goto, kill_group, talk and return steps, spawns, rewards, HUD marker.
8. **Wanted system** - witnesses, heat levels, decay, admission-of-guilt fine.
9. **Day, night and weather** - sun, moon, fog, lamps, five weather states.
10. **UI** - HUD, minimap, world map with waypoints and location names, pause menu, settings, graphics tiers.
11. **Save and load** - single slot in `user://save.json`.
12. **Per-building overrides** - roof, wall, facade photo or 3D model by OpenStreetMap id.
13. **Smoke test** - `--smoke` plays through the game and exits non-zero on failure.
20. **House numbers, addresses and real places** - every building carries its municipal address (eThekwini open data), plates by the door, HUD address, map numbers and search; schools, places of worship, the library, hall, pool, clinics, hospital, shopping centres, stadia and parks exist with real names.

Shipped, the refactor MVP:

14. **Unit test runner** - `tests/Veld.Tests` (xUnit) for pure logic, wired into the build and documented in `AGENTS.md`.
15. **Game services seam** - the mission, dialogue and wanted systems reached through small ports (economy, notifier, clock, world query, dialogue view) with live adapters in the game and in-memory fakes in tests.
16. **Mission step registry** - step kinds as classes looked up by type name; the JSON unchanged.
17. **Dialogue action and condition registries** - the same for dialogue conditions and actions.
18. **Retire the Game singleton** - every call site replaced by services composed in `Main`; `Game.cs` and the autoload are gone.
19. **Simulation and presentation split** - the `Veld.Sim` library holds the data model and loader, money, factions, the ports, purse, standing, notifier, missions, dialogue and wanted; one `Simulation` owns them with the sim clock and is ticked from `Main` at the physics step; NPC decisions are a brain per character and the player's health, stamina, Dead Eye, weapons, mount and death are a `PlayerState`; Godot nodes are bodies and views; the save is `Simulation.Snapshot` and `Restore`.

Next, the enhancements, in build order:

21. **Roofs for any outline** - hip roofs on L and T shaped houses, not only rectangles.
22. **Landmark models** - stadium, shopping centre, police station, schools and filling stations as real models via overrides.
23. **Animals** - dogs, monkeys and birds that wander and flee.
24. **Android build** - Mobile renderer preset, touch controls, performance tiers, runs on a real phone.
25. **More missions** - six to eight on the launch map, with new step types as needed.
26. **Multiplayer co-op** - server-authoritative, two to four players in one suburb; the simulation library and the snapshot are its prerequisites.

## Data model

Everything is JSON on disk under `data/maps/<map-id>/`, read by the library's
`DataLoader` (`System.Text.Json`, camelCase, comments and trailing commas
allowed, nulls omitted on write) through the `IDataFiles` port into the sealed
data classes of `Veld.Sim`. Positions in data are `[x, z]` metres from the map
centre, x east, z south; height always comes from the terrain.

### WorldData (`world.json`, generated)

- `name`, `era` ("modern"), `biome`, `intro`, `attribution`, `seed`, `sizeMeters` (3100), `cellSize`, `heightmap`
- `playerSpawn` `[x, z]`, `playerSpawnFacing` (degrees), `playerWeapons` (ids), `startMoney` (cents), `startHour`, `dayLengthSeconds`
- `roads[]` - `name`, `kind` (OSM highway), `width`, `surface`, `lit`, `points[]`
- `areas[]` - `kind` (pitch, park, parking, wood, water...), `name`, `sport`, `polygon[]`
- `buildings[]` - `id` (OSM), `kind`, `name`, `levels`, `polygon[]`, `roof`, `use`, **`number`**, **`street`**, **`suburb`**, plus override fields `wall`, `roofKind`, `roofColor`, `facade`, `model`, `modelYaw`, `modelScale`
- `lines[]` - `kind` (fence, wall, stream, river, power_line), `points[]`
- `pois[]` - `kind`, `name`, `position`, `source` ("" for OSM, "ethekwini")
- `suburbs[]` - `name`, `polygon[]`
- `vegetation`, `streetLightSpacing`, `parkedCars`, `railway`, `horseEnabled`

> Load-bearing: `buildings[].number/street/suburb` and `suburbs[]` are read by the HUD, the map search and the smoke test. Keep them.

### Hand-written per map

- `npcs.json` - `id`, `name`, `faction` (civilian, british = police, boer = resident, outlaw = gang), `role`, `position` or `town` + `offset`, `dialogue`, `patrol[]`, `weapon`, `hostile`, `group`, `wanderRadius`
- `dialogue.json` - `{ id: { speaker, start, nodes: { id: { text, choices: [{ text, next, condition, action }] } } } }`; conditions `mission_available:id`, `mission_active:id`, `mission_step:id:n`, `mission_done:id`, `wanted`, `not_wanted`, `money:n`; actions `start_mission:id`, `talk_done`, `give_money:n`, `take_money:n`, `give_ammo:weapon:n`, `heal`, `sleep:h`, `clear_wanted:n`, `end`; each kind is a class in the dialogue registry
- `missions.json` - `id`, `title`, `description`, `giver`, `rewardPence`, `requires[]`, `steps[] { type (goto, kill_group, talk, return), objective, target, position, radius, group }`, `spawns[]` (NpcData); each step type is a class in the mission step registry
- `weapons.json` - `id`, `name`, `kind` (rifle, revolver), `damage`, `magSize`, `rateOfFire`, `reloadTime`, `spreadDegrees`, `hipSpreadDegrees`, `range`, `reserveAmmo`
- `overrides.json` - `{ buildings: { "<osm id>": { levels, kind, number, street, wall, roofKind, roofColor, facade, model, modelYaw, modelScale } } }`
- `data/config.json` - `{ map }`

### The simulation and its snapshot

The `Simulation` owns the whole game state: `Clock` (hour, day, day length),
`Purse` (pence), `Reputation` (standing), `Missions`, `Wanted` (heat and
level), `Dialogue`, `Player` (`PlayerState`: health, stamina, Dead Eye, the
weapons as `WeaponState`s with magazine and reserve, mount, dead or alive)
and `Characters` (one `NpcBrain` per NPC: faction, health, provoked, state,
dead). `Simulation.Snapshot()` returns a `SimSnapshot` and `Restore()` puts it
back; `user://save.json` is that snapshot, written and read by `SaveGame`,
which also moves the player's body.

- `SimSnapshot` - `version` (1), `pence`, `reputation`, `playerPosition` `[x, y, z]`, `hour`, `day`, `deadNpcs[]`, `completedMissions[]`, `activeMission`, `activeStep`, `wantedLevel`, `health`, `weapon`, `ammo { "<weapon>.mag": n, "<weapon>.reserve": n }`

> Load-bearing: these fourteen keys are the save file on disk and are pinned by a test; item 26 serialises the same snapshot between server and clients.

- `user://settings.cfg` - mouse sensitivity, invert, volume, graphics quality (game-side, not in the snapshot)

Money is an integer: cents on the modern map, pence (240 to the pound) on a
1901-era map; `Money.Format` renders either.

## Tech stack

- **Godot 4.7.2 .NET** - engine; Forward+ renderer on desktop, Mobile renderer planned for Android
- **C# on .NET 8** (`Godot.NET.Sdk/4.7.2`, nullable, latest language) - all game code, one system per file under `scripts/`
- **`Veld.Sim`** (`sim/Veld.Sim/`, a plain net8.0 class library referencing `GodotSharp` only for the maths structs) - the simulation: data model and loader, money, factions, the ports, the `Simulation` with its clock, purse, standing, notifier, missions, dialogue, wanted, NPC brains and player state, and the snapshot; no engine API, kept out by a grep fence
- **xUnit on .NET 8** (`tests/Veld.Tests`, referencing the library alone) - 171 behaviour tests over the simulation through its ports, with in-memory fakes
- **System.Text.Json** - every data file and the save
- **Python 3, standard library** - the map tools under `tools/` (OpenStreetMap fetch, elevation, eThekwini fetch, converter)
- **Git on GitHub** (`violan7naidoo/veld`)
- No server, no database, no third-party packages beyond Godot's own

Constraints: no C# web export in Godot 4.7; Android and iOS C# export are
experimental; iOS needs a Mac; the Mobile renderer lacks SDFGI, SSAO, SSIL and
volumetric fog, which the Low quality tier already turns off.

## Monetization

> TODO: not decided. Nothing in the code assumes a model.

## UI/UX

Third-person over-the-shoulder camera. Minimal HUD: health, stamina, money,
compass with objective marker, minimap with the house, road and suburb name,
interaction prompt. Dialogue box with numbered choices. Full map on M with
labels, house numbers at close zoom, and an address search. Pause menu with
save, load, settings and graphics quality. Box-and-cylinder placeholder models
to be replaced.

Content rules: characters are invented; public places keep real names; shops
get generic signs; missions are small-town stories; the area's history is
acknowledged, not performed.

## Deployment

- Now: Windows desktop from the editor or `play.cmd`; no export preset yet.
- Planned: Android first (Google Play needs a 12-tester, 14-day closed test for a new personal account), then iOS (Mac and Apple Developer Program).
- No server, no env vars, no cron. The app must show the OpenStreetMap and SRTM attribution.

## Open questions

- Target age rating (shooting and police chases suggest Teen / PEGI 16).
- Monetization model.
- Android before iOS: assumed, not confirmed.
- Order of enhancements 21 to 26: the build plan's order is a suggestion; Android was named a priority.
- `project-plan.md` §3 still lists the SOLID refactor and the addresses under "next"; the build plan has items 14 to 20 shipped. Move them to the shipped list so the plans agree.
- eThekwini address data (build plan item 20) appears in no section of `project-plan.md`: add it to §4 as a source, to §5 beside the Python tools, and to §8's attribution line, and confirm its licence terms before a commercial release.
- `project-plan.md` §4 lists `user://save.json` by field; it is now the simulation's snapshot (item 19e). The fields are the same; say so there.
