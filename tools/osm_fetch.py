"""Fetch OpenStreetMap data for Unit 7 (Montford), Chatsworth, Durban.

Step 1: find every street named "Road 7xx" (Chatsworth numbers its roads by unit) to get the unit's extent.
Step 2: download all roads, buildings, land use, leisure areas, water, amenities in that extent plus a margin.
Writes raw JSON next to this script. Data (c) OpenStreetMap contributors, ODbL.
"""
import json, sys, time, urllib.parse, urllib.request, os

OUT = os.path.dirname(os.path.abspath(__file__))
UA = "VeldPrototype/1.0 (game map research; contact: local dev)"
MIRRORS = ["https://z.overpass-api.de/api/interpreter", "https://lz4.overpass-api.de/api/interpreter", "https://overpass-api.de/api/interpreter"]


def overpass(query):
    data = urllib.parse.urlencode({"data": query}).encode()
    last = None
    for m in MIRRORS:
        for attempt in range(2):
            try:
                req = urllib.request.Request(m, data=data, headers={"User-Agent": UA})
                with urllib.request.urlopen(req, timeout=180) as r:
                    return json.load(r)
            except Exception as e:
                last = e
                print("  retry", m, e, file=sys.stderr)
                time.sleep(3)
    raise last


# ---- step 1: extent of the Road 7xx streets
box = "-29.95,30.84,-29.89,30.91"  # generous box around Chatsworth
q1 = f'[out:json][timeout:120];way["highway"]["name"~"^7[0-9][0-9] Road$"]({box});out geom;'
roads7 = overpass(q1)
names = sorted({e["tags"]["name"] for e in roads7["elements"]})
lats = [p["lat"] for e in roads7["elements"] for p in e.get("geometry", [])]
lons = [p["lon"] for e in roads7["elements"] for p in e.get("geometry", [])]
print(f"Road 7xx ways: {len(roads7['elements'])}, distinct names: {len(names)}")
print("names:", ", ".join(names))
if not lats:
    sys.exit("no Road 7xx streets found")
s, n, w, e = min(lats), max(lats), min(lons), max(lons)
print(f"extent of Unit 7 streets: lat {s:.5f}..{n:.5f}  lon {w:.5f}..{e:.5f}")
# metres per degree at this latitude
import math
m_lat = 111320.0
m_lon = 111320.0 * math.cos(math.radians((s + n) / 2))
print(f"size: {(n - s) * m_lat:.0f} m north-south, {(e - w) * m_lon:.0f} m east-west")

# ---- step 2: everything in the extent plus a margin
margin_m = 250
ms, mn = s - margin_m / m_lat, n + margin_m / m_lat
mw, me = w - margin_m / m_lon, e + margin_m / m_lon
bbox = f"{ms:.6f},{mw:.6f},{mn:.6f},{me:.6f}"
q2 = f"""[out:json][timeout:180];
(
  way["highway"]({bbox});
  way["building"]({bbox});
  relation["building"]({bbox});
  way["landuse"]({bbox});
  way["leisure"]({bbox});
  way["natural"]({bbox});
  way["waterway"]({bbox});
  way["amenity"]({bbox});
  way["barrier"]({bbox});
  way["man_made"]({bbox});
  way["power"]({bbox});
  node["amenity"]({bbox});
  node["shop"]({bbox});
  node["place"]({bbox});
  node["highway"="bus_stop"]({bbox});
  node["natural"="tree"]({bbox});
);
out geom;"""
t0 = time.time()
data = overpass(q2)
print(f"downloaded {len(data['elements'])} elements in {time.time() - t0:.0f}s")
data["meta"] = {"unit7_extent": {"south": s, "north": n, "west": w, "east": e}, "bbox": [ms, mw, mn, me], "margin_m": margin_m,
                "m_per_deg_lat": m_lat, "m_per_deg_lon": m_lon}
with open(os.path.join(OUT, "montford_osm.json"), "w", encoding="utf-8") as f:
    json.dump(data, f)

from collections import Counter
kinds = Counter()
for el in data["elements"]:
    t = el.get("tags", {})
    for k in ("highway", "building", "landuse", "leisure", "natural", "waterway", "amenity", "shop", "barrier", "man_made", "power", "place"):
        if k in t:
            kinds[f"{k}={t[k]}"] += 1
            break
for k, v in kinds.most_common(60):
    print(f"{v:5d}  {k}")
named = [el["tags"]["name"] for el in data["elements"] if "name" in el.get("tags", {}) and el["tags"].get("highway") is None]
print("named places/features:", len(named))
print("; ".join(sorted(set(named))[:120]))
