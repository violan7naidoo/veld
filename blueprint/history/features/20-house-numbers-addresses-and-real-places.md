# Feature: House numbers, addresses and real places

**From build-plan:** feature 20
**Status:** complete (2026-10-14)
**Branch:** `feature/addresses`

## Goal

Every house and building on the Chatsworth map carries its real municipal
address, shown on a plate by the door, on the HUD when you stand at it, and on
the map, and the real public places (schools, temples, mosques, churches, the
library, the community hall, the pool, clinics, the hospital, shopping centres,
stadia, parks, police, fire) exist with their real names. The source is
eThekwini Municipality's open GIS data, which OpenStreetMap lacks for this
suburb (4 house numbers in 14,000 buildings).

## In scope

- A fetch tool for the eThekwini layers and the fetched source data under `tools/source/`.
- Converter changes: parcel polygons give each building its street number, street and suburb; zoning gives generic buildings a better kind; facility layers become named points of interest; suburb polygons are written out.
- Game: number plates beside front doors, address and suburb on the HUD, house numbers and an address search on the full map, labels for the new place kinds.
- Smoke test checks and documentation, including attribution.

## Out of scope

- Real building models for the landmarks (build plan item 22).
- Shops keep generic signs; only public places get real names (content rules).
- Parcel boundaries as fences or walls in the 3D world.

## Build steps

- [x] **Step 1 - Fetch tool and source data** - `tools/ethekwini_fetch.py` downloads address points, zoning parcels, suburbs and facility layers for the elevation file's box into `tools/source/montford_ethekwini.json`. *Done when:* the file exists with 11,000+ address points and 9,000+ parcels.
- [x] **Step 2 - Converter** - `tools/osm_to_map.py` takes the eThekwini file as an optional fourth argument. Each building gets `number`, `street`, `suburb` from the parcel containing its centroid, else the nearest address point within 25 m. Generic `building=yes` footprints inside WORSHIP, educational, business, industrial or service-station parcels get a matching `kind`. Facilities become `pois` with real names and a `source` of `ethekwini`; suburbs become a `suburbs` list of named polygons. *Done when:* the converter reports 90 percent or more of buildings inside the Montford suburb with an address and `world.json` holds the new fields.
- [x] **Step 3 - Schema** - `FootprintData.Number`, `Street`, `Suburb`; `WorldData.Suburbs` of `SuburbData { Name, Polygon }`; `PoiData.Source`; `FootprintOverride.Number`, `Street`. *Done when:* `dotnet build` is clean and the game loads the new `world.json`.
- [x] **Step 4 - Address plates** - `scripts/World/AddressPlates.cs`: `FootprintBuilder` records a plate (position beside the door, facing out, text) per addressed house; the node streams `Label3D` plates for the chunks within 120 m of the player. *Done when:* walking Road 721 shows a number on every house front.
- [x] **Step 5 - HUD address** - `MapImage.LocationName` uses suburb polygons for the suburb and, when the player is within a house's front radius, prefixes the house address: "88 Road 723, Montford". *Done when:* the HUD reads the spawn house's address at spawn and the road name on the road.
- [x] **Step 6 - Map** - `WorldMap` draws house numbers at building centroids when zoomed close, and a search box ("88 723" or "88 Road 723") that sets the waypoint and centres the map. *Done when:* searching the spawn address puts the waypoint on that house.
- [x] **Step 7 - Real places** - `Dressing.PoiLabel` and `MapImage` label the new kinds with their real names (schools with full names, places of worship, library, hall, pool, clinics, hospital, shopping centres, stadia, parks). *Done when:* Montford Community Hall, Montford Library, Al Ameen Musjid and Montford Primary School are visible in the world and on the map.
- [x] **Step 8 - Smoke test and docs** - `DevSmoke` checks address coverage, the spawn address on the HUD and the plates node; README, editing guide and attribution updated. *Done when:* the smoke run passes with the new checks and the README explains addresses and credits eThekwini.

## Files / areas

- `tools/ethekwini_fetch.py` (new), `tools/osm_to_map.py`, `tools/source/montford_ethekwini.json` (new)
- `data/maps/chatsworth-unit7/world.json` (regenerated)
- `scripts/Core/Data.cs`, `scripts/World/FootprintBuilder.cs`, `scripts/World/AddressPlates.cs` (new), `scripts/World/MapImage.cs`, `scripts/World/Dressing.cs`, `scripts/UI/WorldMap.cs`, `scripts/Core/DevSmoke.cs`
- `README.md`, `docs/editing-guide.md`

## Data / contracts

- Building: `"number": "88"`, `"street": "Road 723"`, `"suburb": "Montford"`. Numbers are strings because the municipality uses suffixes such as `309T`.
- Street display form: numbered Chatsworth roads are `Road 723` (the municipal and local form), other streets are title case with their type, `Rainbow Circle`.
- `"suburbs": [{ "name": "Montford", "polygon": [[x, z], ...] }]`
- POI: `{ "kind": "school" | "place_of_worship" | "library" | "community_centre" | "swimming_pool" | "clinic" | "hospital" | "shopping_centre" | "stadium" | "park" | "police" | "fire_station" | "railway_station", "name": "...", "position": [x, z], "source": "ethekwini" }`

## Testing

- No unit test runner yet; evidence is `dotnet build`, the smoke run, and screenshots.
- Converter: its printed coverage report is the check for Step 2.
- Smoke: new PASS lines for address coverage, spawn address, plates node.

## Research

- `blueprint/research/ethekwini-open-data.md` - what the municipality publishes, what it covers in Montford, and the licence terms.

## Notes for the AI

- Keep the OSM building outlines; the municipal address is attached to them, not replaced.
- Content rules: public places keep real names; private shops stay generic.
- No new `Game.Instance` call sites; the plates node receives what it needs from `WorldBuilder`.
- Attribute eThekwini Municipality wherever OpenStreetMap is attributed.
