# Veld - Project Overview

> An open-world game set in Montford, Chatsworth, Durban, built from real map data, on its way from a one-day prototype to a clean, testable codebase and a phone release.

> **Generated file.** Distilled by `/overview` from `blueprint/project-plan.md`
> and `blueprint/build-plan.md` on 2026-10-14. Change the plans and re-run
> `/overview`; do not hand-edit this.

## Problem

The prototype works and already renders the real suburb with its real addresses,
but it hangs off one global singleton, routes behaviour through string
switches, has no unit tests and runs only on Windows desktop. The project's job
is to turn it into a clean, testable, scalable codebase without breaking the
game, then grow it towards Android and iOS and, later, multiplayer.

## Users

- The owner, as developer and first player.
- People who know Chatsworth and want to explore a recognisable version of it.
- Later: mobile players on Google Play and the App Store.

## Features

Shipped (1 to 13 and 20), then the roadmap in build order. The headline feature
is the real suburb: real streets, real outlines, real addresses, real public
places.

1. **Real-world map pipeline** - OpenStreetMap, SRTM elevation and eThekwini municipal data converted to `world.json` and `heightmap.json`.
2. **Generated suburb** - terrain, roads, houses from outlines, fields, fences, streams, signs, parked cars.
3. **Player on foot** - walk, sprint, jump, aim, shoot, reload, two weapons, slow-motion aiming, stamina.
4. **Driving** - cars and taxis on vehicle physics, enter and exit, chase camera, lights, horn, recall.
5. **NPCs and factions** - residents, police and gang with wander, patrol, alert, combat and death.
6. **Dialogue** - JSON conversation trees with conditions and actions.
7. **Missions** - goto, kill_group, talk and return steps, spawns, rewards, HUD marker.
8. **Wanted system** - witnesses, heat levels, decay, admission-of-guilt fine.
9. **Day, night and weather** - sun, moon, fog, lamps, five weather states.
10. **UI** - HUD, minimap, world map with waypoints and location names, pause menu, settings, graphics tiers.
11. **Save and load** - single slot.
12. **Per-building overrides** - roof, wall, facade photo, 3D model, number and street by OpenStreetMap id.
13. **Smoke test** - `--smoke` plays through the game and exits non-zero on failure.
20. **House numbers, addresses and real places** - municipal address on every building, plates by the door, HUD address, map numbers and search, named public places.

Next, the MVP of the refactor:

14. **Unit test runner** - xUnit project for pure logic; turns the test gate on.
15. **Game services seam** - clock, economy and notifier interfaces out of `Game`, injected into the mission, dialogue and wanted systems.
16. **Mission step registry** - step types as classes looked up by name.
17. **Dialogue action and condition registries** - the same for dialogue.
18. **Retire the Game singleton** - call sites replaced by services composed in `Main`.
19. **Simulation and presentation split** - `Veld.Sim` library at a fixed tick; Godot nodes render.

Then enhancements: 21 roofs for any outline, 22 landmark models, 23 animals,
24 Android build, 25 more missions, 26 multiplayer co-op.

## Data model

Everything is JSON on disk under `data/maps/<map-id>/`, read by
`DataLoader` (`System.Text.Json`, camelCase, comments allowed) into the sealed
classes in `scripts/Core/Data.cs`. Positions are `[x, z]` metres from the map
centre, x east, z south; height comes from the terrain.

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
- `dialogue.json` - `{ id: { speaker, start, nodes: { id: { text, choices: [{ text, next, condition, action }] } } } }`; conditions `mission_available:id`, `mission_active:id`, `mission_step:id:n`, `mission_done:id`, `wanted`, `not_wanted`, `money:n`; actions `start_mission:id`, `talk_done`, `give_money:n`, `take_money:n`, `give_ammo:weapon:n`, `heal`, `sleep:h`, `clear_wanted:n`, `end`
- `missions.json` - `id`, `title`, `description`, `giver`, `rewardPence`, `requires[]`, `steps[] { type (goto, kill_group, talk, return), objective, target, position, radius, group }`, `spawns[]` (NpcData)
- `weapons.json` - `id`, `name`, `kind` (rifle, revolver), `damage`, `magSize`, `rateOfFire`, `reloadTime`, `spreadDegrees`, `hipSpreadDegrees`, `range`, `reserveAmmo`
- `overrides.json` - `{ buildings: { "<osm id>": { levels, kind, number, street, wall, roofKind, roofColor, facade, model, modelYaw, modelScale } } }`
- `data/config.json` - `{ map }`

### Player state

- `user://save.json` - `version`, `pence`, `reputation`, `playerPosition`, `hour`, `day`, `deadNpcs[]`, `completedMissions[]`, `activeMission`, `activeStep`, `wantedLevel`, `health`, `weapon`, `ammo{}`
- `user://settings.cfg` - mouse sensitivity, invert, volume, graphics quality

Money is an integer: cents on the modern map, pence (240 to the pound) on a
1901-era map; `Money.Format` renders either.

## Tech stack

- **Godot 4.7.2 .NET** - engine; Forward+ renderer on desktop, Mobile renderer planned for Android
- **C# on .NET 8** (`Godot.NET.Sdk/4.7.2`, nullable, latest language) - all game code, one system per file under `scripts/`
- **System.Text.Json** - every data file
- **Python 3, standard library** - the map tools under `tools/` (OpenStreetMap fetch, elevation, eThekwini fetch, converter)
- **Git on GitHub** (`violan7naidoo/veld`)
- No server, no database, no third-party packages beyond Godot's own

Constraints: no C# web export in Godot 4.7; Android and iOS C# export are
experimental; iOS needs a Mac.

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
- Planned: Android first (Google Play needs a 12-tester, 14-day closed test for a new personal account), then iOS.
- No env vars, no services. The app must show the OpenStreetMap, SRTM and eThekwini attribution.

## Open questions

- Target age rating (shooting and police chases suggest Teen / PEGI 16).
- Monetization model.
- Android before iOS: assumed, not confirmed.
- Order of enhancements 21 to 26 after the refactor.
- eThekwini data carries only a liability disclaimer, no licence name; confirm terms before a commercial release.
