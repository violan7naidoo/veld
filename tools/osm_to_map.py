"""Convert raw OpenStreetMap (Overpass JSON) + a Terrarium elevation grid into a Veld map folder.

    python tools/osm_to_map.py tools/source/montford_osm.json tools/source/montford_elevation.json data/maps/chatsworth-unit7

Writes map.json (roads, areas, buildings, lines, points of interest, world settings) and heightmap.json.
Game coordinates: x = metres east of the map centre, z = metres south of it (Godot's -Z is north).
Map data (c) OpenStreetMap contributors, ODbL 1.0. Elevation: NASA SRTM via AWS Terrain Tiles.
"""
import json, math, os, sys
from collections import Counter

osm_path, elev_path, out_dir = sys.argv[1], sys.argv[2], sys.argv[3]
os.makedirs(out_dir, exist_ok=True)
osm = json.load(open(osm_path, encoding="utf-8"))
elev = json.load(open(elev_path, encoding="utf-8"))

south, west, north, east = elev["south"], elev["west"], elev["north"], elev["east"]
lat0, lon0 = (south + north) / 2, (west + east) / 2
m_lat, m_lon = elev["m_per_deg_lat"], elev["m_per_deg_lon"]
width_m, height_m = (east - west) * m_lon, (north - south) * m_lat
size = math.ceil(max(width_m, height_m) / 100) * 100 + 100  # square map, a little margin
half = size / 2


def xz(lat, lon):
    return [round((lon - lon0) * m_lon, 1), round(-(lat - lat0) * m_lat, 1)]


def inside(p, margin=0):
    return abs(p[0]) < half - margin and abs(p[1]) < half - margin


# ------------------------------------------------------------------ heightmap
grid = elev["heights"]
rows, cols, step = elev["rows"], elev["cols"], elev["step_m"]


def blur(g):
    out = [[0.0] * cols for _ in range(rows)]
    for j in range(rows):
        for i in range(cols):
            s, n = 0.0, 0
            for dj in (-1, 0, 1):
                for di in (-1, 0, 1):
                    jj, ii = j + dj, i + di
                    if 0 <= jj < rows and 0 <= ii < cols:
                        s += g[jj][ii]; n += 1
            out[j][i] = s / n
    return out


for _ in range(2):
    grid = blur(grid)
flat = [v for r in grid for v in r]
mean = sum(flat) / len(flat)
heights = [round(v - mean, 2) for v in flat]
origin_x = -(cols - 1) * step / 2
origin_z = -(rows - 1) * step / 2
json.dump({"cols": cols, "rows": rows, "stepM": step, "originX": origin_x, "originZ": origin_z, "seaLevelOffset": round(mean, 1), "heights": heights},
          open(os.path.join(out_dir, "heightmap.json"), "w"))
print(f"heightmap {cols}x{rows} @ {step} m, relief {min(heights):.0f}..{max(heights):.0f} m (mean {mean:.0f} m above sea level removed)")

# ------------------------------------------------------------------ features
ROAD_WIDTH = {"motorway": 12, "trunk": 10, "primary": 9, "secondary": 8, "tertiary": 7, "tertiary_link": 6, "unclassified": 6, "residential": 6,
              "living_street": 5, "service": 3.6, "track": 3, "footway": 1.6, "path": 1.4, "steps": 1.6, "cycleway": 2, "pedestrian": 4}
roads, areas, buildings, lines, pois = [], [], [], [], []
kinds = Counter()


def ring_of(el):
    """Closed way geometry as a list of [x,z], or None."""
    g = el.get("geometry")
    if not g or len(g) < 4:
        return None
    pts = [xz(p["lat"], p["lon"]) for p in g]
    if pts[0] != pts[-1]:
        pts.append(pts[0])
    return pts[:-1]


def rings_of_relation(el):
    outs = []
    for m in el.get("members", []):
        if m.get("role") == "outer" and m.get("geometry"):
            pts = [xz(p["lat"], p["lon"]) for p in m["geometry"]]
            if len(pts) >= 3 and pts[0] == pts[-1]:
                outs.append(pts[:-1])
    return outs


def area_kind(t):
    if t.get("leisure") in ("pitch", "park", "stadium", "track", "sports_centre", "playground", "garden", "swimming_pool"):
        return t["leisure"]
    if t.get("landuse") in ("grass", "meadow", "farmland", "education", "industrial", "commercial", "retail", "cemetery", "construction", "brownfield", "greenfield", "recreation_ground", "forest", "residential"):
        return t["landuse"]
    if t.get("natural") in ("wood", "scrub", "grassland", "water", "wetland", "heath", "bare_rock"):
        return t["natural"]
    if t.get("amenity") in ("parking", "school"):
        return t["amenity"]
    if t.get("man_made") == "reservoir_covered":
        return "reservoir"
    return None


for el in osm["elements"]:
    t = el.get("tags", {})
    if el["type"] == "way" and "highway" in t:
        kind = t["highway"]
        pts = [xz(p["lat"], p["lon"]) for p in el.get("geometry", [])]
        pts = [p for p in pts if inside(p, 5)]
        if len(pts) < 2:
            continue
        roads.append({"id": el["id"], "name": t.get("name", ""), "kind": kind, "width": ROAD_WIDTH.get(kind, 4),
                      "surface": t.get("surface", "asphalt" if kind not in ("track", "path", "footway", "steps") else "ground"),
                      "lit": t.get("lit", "") == "yes", "points": pts})
        kinds["road:" + kind] += 1
    elif el["type"] in ("way", "relation") and "building" in t:
        rings = [ring_of(el)] if el["type"] == "way" else rings_of_relation(el)
        for ring in rings:
            if not ring or len(ring) < 3 or not any(inside(p) for p in ring):
                continue
            levels = t.get("building:levels")
            try:
                levels = int(float(levels)) if levels else 0
            except ValueError:
                levels = 0
            buildings.append({"id": el["id"], "kind": t["building"], "name": t.get("name", ""), "levels": levels, "polygon": ring,
                              "roof": t.get("roof:shape", ""), "roofColor": t.get("roof:colour", ""), "wall": t.get("building:material", ""), "use": t.get("amenity", t.get("shop", ""))})
            kinds["building:" + t["building"]] += 1
    elif el["type"] == "way" and area_kind(t):
        ring = ring_of(el)
        if ring and any(inside(p) for p in ring):
            areas.append({"id": el["id"], "kind": area_kind(t), "name": t.get("name", ""), "sport": t.get("sport", ""), "polygon": ring})
            kinds["area:" + area_kind(t)] += 1
    elif el["type"] == "way" and (t.get("waterway") or t.get("barrier") or t.get("power") == "line" or t.get("railway")):
        kind = t.get("waterway") or t.get("barrier") or ("power_line" if t.get("power") == "line" else t.get("railway"))
        pts = [xz(p["lat"], p["lon"]) for p in el.get("geometry", [])]
        pts = [p for p in pts if inside(p, 2)]
        if len(pts) >= 2:
            lines.append({"kind": kind, "name": t.get("name", ""), "points": pts})
            kinds["line:" + kind] += 1
    elif el["type"] == "node" and ("amenity" in t or "shop" in t or t.get("highway") == "bus_stop" or t.get("natural") == "tree" or t.get("place")):
        kind = t.get("shop") and "shop:" + t["shop"] or t.get("amenity") or t.get("highway") or t.get("natural") or "place:" + t.get("place", "")
        p = xz(el["lat"], el["lon"])
        if inside(p):
            pois.append({"kind": kind, "name": t.get("name", ""), "position": p})
            kinds["poi:" + kind] += 1

# ------------------------------------------------------------------ spawn: on 721 Road (the longest numbered road) if present, else the map centre
spawn, facing = [0, 0], 0
candidates = [r for r in roads if r["name"] == "721 Road"] or [r for r in roads if r["kind"] == "residential"]
if candidates:
    longest = max(candidates, key=lambda r: len(r["points"]))
    mid = longest["points"][len(longest["points"]) // 2]
    nxt = longest["points"][min(len(longest["points"]) - 1, len(longest["points"]) // 2 + 1)]
    spawn = mid
    facing = math.degrees(math.atan2(-(nxt[0] - mid[0]), -(nxt[1] - mid[1])))
print(f"spawn at {spawn} facing {facing:.0f} deg")

world = {
    "name": "Montford, Chatsworth (Unit 7)",
    "era": "modern",
    "biome": "subtropical",
    "intro": "Montford, Chatsworth, Durban. The present day. Press E to talk, Esc for the menu.",
    "origin": {"lat": lat0, "lon": lon0, "mPerDegLat": m_lat, "mPerDegLon": m_lon},
    "attribution": "Map data (c) OpenStreetMap contributors (ODbL). Elevation: NASA SRTM via AWS Terrain Tiles.",
    "seed": 7,
    "sizeMeters": size,
    "cellSize": 5,
    "heightScale": 1,
    "heightmap": "heightmap.json",
    "ortho": "",
    "orthoBounds": {"south": south, "west": west, "north": north, "east": east},
    "orthoMix": 0.85,
    "playerSpawn": spawn,
    "playerSpawnFacing": round(facing, 1),
    "playerWeapons": ["pistol", "hunting_rifle"],
    "startHour": 7.5,
    "dayLengthSeconds": 900,
    "horseEnabled": False,
    "horseSpawnTown": "",
    "horseSpawnOffset": [4, 2],
    "horseName": "Vos",
    "vegetation": {"trees": 2200, "rocks": 250, "grass": 160000, "shrubs": 5000, "mounds": 0},
    "startMoney": 25000,
    "streetLightSpacing": 45,
    "parkedCars": 420,
    "roads": roads,
    "areas": areas,
    "buildings": buildings,
    "lines": lines,
    "pois": pois,
    "railway": {"points": [], "loop": False, "blockhouseSpacing": 0, "trainSpeed": 0, "wagons": 0},
}
json.dump(world, open(os.path.join(out_dir, "world.json"), "w", encoding="utf-8"), ensure_ascii=False)
print(f"map {size} m square; roads {len(roads)}, areas {len(areas)}, buildings {len(buildings)}, lines {len(lines)}, pois {len(pois)}")
for k, v in sorted(kinds.items()):
    print(f"  {v:6d} {k}")
total_road_m = 0.0
for r in roads:
    for a, b in zip(r["points"], r["points"][1:]):
        total_road_m += math.dist(a, b)
print(f"total road length {total_road_m / 1000:.1f} km")
