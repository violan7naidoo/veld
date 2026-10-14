"""Render a crude fake aerial photo from world.json (green ground, grey roads, coloured roofs, blue water).

    python tools/make_test_ortho.py data/maps/chatsworth-unit7 2048

It exists to prove the aerial-photo draping lines up with the 3D roads before you spend time on a
real orthophoto. Writes ortho_test.png next to world.json; set "ortho": "ortho_test.png" to see it.
"""
import json, math, os, sys
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from pngread import write_png

map_dir, size = sys.argv[1], int(sys.argv[2]) if len(sys.argv) > 2 else 2048
w = json.load(open(os.path.join(map_dir, "world.json"), encoding="utf-8"))
o, b = w["origin"], w["orthoBounds"]
x0 = (b["west"] - o["lon"]) * o["mPerDegLon"]; x1 = (b["east"] - o["lon"]) * o["mPerDegLon"]
z0 = -(b["north"] - o["lat"]) * o["mPerDegLat"]; z1 = -(b["south"] - o["lat"]) * o["mPerDegLat"]
W = size; H = int(size * (z1 - z0) / (x1 - x0))
mpp = (x1 - x0) / W
img = [bytearray([92, 128, 62] * W) for _ in range(H)]


def put(x, y, c):
    if 0 <= x < W and 0 <= y < H:
        r = img[y]; r[x * 3] = c[0]; r[x * 3 + 1] = c[1]; r[x * 3 + 2] = c[2]


def px(p):
    return (p[0] - x0) / mpp, (p[1] - z0) / mpp


def disc(cx, cy, rad, c):
    for y in range(int(cy - rad), int(cy + rad) + 1):
        for x in range(int(cx - rad), int(cx + rad) + 1):
            if (x + 0.5 - cx) ** 2 + (y + 0.5 - cy) ** 2 <= rad * rad: put(x, y, c)


def line(a, b, width, c):
    ax, ay = px(a); bx, by = px(b)
    n = max(1, int(math.hypot(bx - ax, by - ay) / 0.5))
    for i in range(n + 1):
        t = i / n
        disc(ax + (bx - ax) * t, ay + (by - ay) * t, max(0.5, width / mpp / 2), c)


def inside(p, poly):
    r, n = False, len(poly)
    for i in range(n):
        a, bb = poly[i], poly[(i + 1) % n]
        if (a[1] > p[1]) != (bb[1] > p[1]) and p[0] < (bb[0] - a[0]) * (p[1] - a[1]) / (bb[1] - a[1]) + a[0]:
            r = not r
    return r


for l in w["lines"]:
    if l["kind"] in ("stream", "river"):
        for a, bb in zip(l["points"], l["points"][1:]): line(a, bb, 3, (70, 110, 150))
for r in w["roads"]:
    col = (60, 60, 62) if r["kind"] in ("residential", "tertiary", "unclassified", "tertiary_link") else (120, 115, 105)
    for a, bb in zip(r["points"], r["points"][1:]): line(a, bb, r["width"], col)
for i, bld in enumerate(w["buildings"]):
    poly = bld["polygon"]
    xs = [px(p)[0] for p in poly]; ys = [px(p)[1] for p in poly]
    c = [(150, 70, 50), (120, 120, 125), (170, 170, 175), (95, 60, 45)][i % 4]
    for y in range(int(min(ys)), int(max(ys)) + 1):
        for x in range(int(min(xs)), int(max(xs)) + 1):
            if inside(((x + 0.5) * mpp + x0, (y + 0.5) * mpp + z0), poly): put(x, y, c)
out = os.path.join(map_dir, "ortho_test.png")
write_png(out, W, H, img)
print(f"wrote {out} ({W}x{H}, {mpp:.2f} m/px)")
