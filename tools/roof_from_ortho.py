"""Read each building's roof colour and kind off an aerial photo and write them to overrides.json.

    python tools/roof_from_ortho.py data/maps/chatsworth-unit7 ortho.png south west north east

The PNG must be north-up, 8-bit, non-interlaced, covering exactly the given lat/lon box (as NGI
orthophotos are when exported from QGIS). For each building footprint the script averages the
pixels inside the outline, guesses tile / iron / flat from brightness and saturation, and writes
    { "buildings": { "<osm id>": { "roofColor": "#rrggbb", "roofKind": "tile|iron|flat" } } }
merging into any existing overrides.json (your hand edits win).
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngread import read_png

map_dir, png, south, west, north, east = sys.argv[1], sys.argv[2], *map(float, sys.argv[3:7])
world = json.load(open(os.path.join(map_dir, "world.json"), encoding="utf-8"))
o = world["origin"]
w, h, ch, rows = read_png(png)
print(f"image {w}x{h}, {ch} channels")


def px(x, z):
    """Game metres -> pixel."""
    lon = o["lon"] + x / o["mPerDegLon"]
    lat = o["lat"] - z / o["mPerDegLat"]
    return int((lon - west) / (east - west) * w), int((north - lat) / (north - south) * h)


def inside(p, poly):
    r, n = False, len(poly)
    for i in range(n):
        a, b = poly[i], poly[(i + 1) % n]
        if (a[1] > p[1]) != (b[1] > p[1]) and p[0] < (b[0] - a[0]) * (p[1] - a[1]) / (b[1] - a[1]) + a[0]:
            r = not r
    return r


over_path = os.path.join(map_dir, "overrides.json")
over = json.load(open(over_path, encoding="utf-8")) if os.path.exists(over_path) else {"buildings": {}}
over.setdefault("buildings", {})
done = 0
for b in world["buildings"]:
    poly = b["polygon"]
    xs = [p[0] for p in poly]; zs = [p[1] for p in poly]
    x0, y0 = px(min(xs), min(zs)); x1, y1 = px(max(xs), max(zs))
    if x1 < 0 or y1 < 0 or x0 >= w or y0 >= h:
        continue
    acc = [0, 0, 0]; n = 0
    for py in range(max(0, min(y0, y1)), min(h - 1, max(y0, y1)) + 1):
        for pxx in range(max(0, min(x0, x1)), min(w - 1, max(x0, x1)) + 1):
            # pixel centre back to metres
            lon = west + (pxx + 0.5) / w * (east - west)
            lat = north - (py + 0.5) / h * (north - south)
            gx = (lon - o["lon"]) * o["mPerDegLon"]; gz = -(lat - o["lat"]) * o["mPerDegLat"]
            if not inside((gx, gz), poly):
                continue
            r = rows[py]
            acc[0] += r[pxx * ch]; acc[1] += r[pxx * ch + 1]; acc[2] += r[pxx * ch + 2]; n += 1
    if n < 3:
        continue
    r, g, bl = [v / n / 255 for v in acc]
    mx, mn = max(r, g, bl), min(r, g, bl)
    sat = 0 if mx == 0 else (mx - mn) / mx
    bright = (r + g + bl) / 3
    if sat < 0.12 and bright > 0.55: kind = "iron"      # light grey / silver sheets
    elif sat < 0.12: kind = "flat"                      # dark grey concrete
    elif r > g and r > bl: kind = "tile"                 # terracotta / brown tiles
    else: kind = "tile"
    key = str(b.get("id", ""))
    if not key:
        continue
    entry = over["buildings"].setdefault(key, {})
    entry.setdefault("roofColor", "#%02x%02x%02x" % (int(r * 255), int(g * 255), int(bl * 255)))
    entry.setdefault("roofKind", kind)
    done += 1
json.dump(over, open(over_path, "w", encoding="utf-8"), indent=1)
print(f"wrote roof colour and kind for {done} buildings to {over_path}")
