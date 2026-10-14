# Making Chatsworth look like Chatsworth: the real-houses guide

The map data gives the game every street and every building outline in Unit 7, placed where the real
ones are. It does not say what any building looks like. This guide is about closing that gap, from
"the ground is the real ground" to "that is my house". Each level plugs into something the game
already supports; nothing here needs new code.

## What cannot be used

Google Maps, Street View, Google Earth 3D buildings, Bing and Apple imagery, Esri basemaps: their terms
forbid extracting imagery or 3D data for use in a game. The same goes for Rockstar's assets. Using
them would also make the game impossible to share. Everything below is either open data or something
you capture yourself.

## Level 1: the real ground from above (an afternoon)

**Source.** The South African National Geo-spatial Information office (NGI, part of the Department of
Agriculture, Land Reform and Rural Development) publishes colour aerial photography of the whole
country at 25 cm per pixel, including Durban. It is distributed through the CDNGI Geospatial Portal
(search for "CDNGI geoportal"). The licence allows use with attribution; confirm the current terms on
the portal before you ship anything.

**Steps.**
1. Download the orthophoto tiles that cover Unit 7 (the map's box is south -29.9304, west 30.8549,
   north -29.9082, east 30.8853; the files are georeferenced TIFFs).
2. In QGIS (free), load the tiles, set the project CRS to EPSG:4326, and export a single PNG clipped to
   exactly that box: Project > Import/Export > Export Map to Image, extent = the box, 4096 or 8192 px wide.
   Save it as `data/maps/chatsworth-unit7/ortho.png`.
3. In `world.json` set `"ortho": "ortho.png"` (the bounds are already there under `orthoBounds`).
4. Run the game. Every road, driveway, lawn and roof is now the real colour and shape from above. Raise or
   lower `"orthoMix"` to blend with the procedural ground detail.

**Then the roofs.** `python tools/roof_from_ortho.py data/maps/chatsworth-unit7 data/maps/chatsworth-unit7/ortho.png -29.9304 30.8549 -29.9082 30.8853`
reads the roof colour of every one of the 14,000 buildings off the photo and writes `overrides.json`
with a colour and a guess at tile / iron / flat for each. From then on each house carries its own real
roof colour. Hand edits in `overrides.json` are kept when the tool is re-run.

To prove the alignment before you have the real photo, `python tools/make_test_ortho.py data/maps/chatsworth-unit7`
renders a fake one from the map data; set `"ortho": "ortho_test.png"` and the drawn roads must sit
exactly under the 3D roads.

## Level 2: the real walls from the street (an afternoon per block)

Walk the street with a phone. For each house, one photo of the front, as straight-on as you can,
whole house in frame, in flat light (overcast is best). Crop each photo to the wall: left edge of the
house to the right edge, ground line to the roof line. Save as `data/maps/chatsworth-unit7/facades/<osm id>.jpg`.

Find a house's OpenStreetMap id: open the full map in the game (M), or look it up at openstreetmap.org
by clicking the building, or read it from `world.json` (every building has `"id"`). Then in
`overrides.json`:

```json
{ "buildings": { "175563190": { "facade": "facades/175563190.jpg", "wall": "brick", "roofKind": "tile", "roofColor": "#6e3b2c", "levels": 1 } } }
```

The photo replaces that house's street-facing wall; the other walls, roof and boundary wall are still
generated but can be matched with the colour fields. Windows, doors, burglar bars and paint are then
real. This is the biggest visual step per hour of effort.

## Level 3: real 3D houses (an hour per house, or a drone day per suburb)

**Phone photogrammetry.** Walk around a house taking 40 to 100 overlapping photos (every 10 degrees,
two heights, include the roof from the far pavement). Feed them to Meshroom (free, AliceVision),
RealityScan (free on phones) or Polycam. Export a GLB with a texture, open it in Blender, delete the
ground and neighbours, set the origin to the centre of the floor at ground level, +X east, scale in
metres, export GLB. Put it in `data/maps/chatsworth-unit7/models/` and add to `overrides.json`:

```json
"175563190": { "model": "models/175563190.glb", "modelYaw": 0, "modelScale": 1 }
```

The game places it on the footprint, on the ground, with collision, and skips its generated box.
Try it today with the included `models/example_house.tscn`.

**Drone survey.** For the whole unit, this is what the professionals do: a drone with a mapping app
flies a grid with oblique photos, and photogrammetry software builds one textured mesh of the entire
suburb, exactly like the 3D view in a map app. Practicalities in South Africa: hobby flights under
SACAA rules keep the drone in sight, under 120 m, away from roads, people and private property without
permission; anything commercial needs an RPAS licence and an operator certificate. A surveyed mesh is
huge; cut it into 150 m chunks in Blender with a decimated LOD, and load each chunk as a model override
or replace the terrain and buildings for that area.

## Level 4: the rest of what makes it feel real

- Street name signs, bus shelters, taxi rank, the mosque and temples (OpenStreetMap has `place_of_worship`;
  model them like Level 3 landmarks).
- Trees in the right gardens: OpenStreetMap `natural=tree` points (there are a few) or your own list.
- People: the current placeholders can be replaced with rigged models (see the editing guide).
- Sound: record ambience in the street (birds, taxis, the mosque, the stadium) and loop it.

## Suggested order

1. Level 1 with the NGI orthophoto, plus the roof tool. One afternoon, whole suburb.
2. Level 2 for 721 Road and the streets you care about.
3. Level 3 for your own house, the superette, the stadium and the shopping centre.
4. A drone survey if the project grows beyond a hobby.
