"""Decode Terrarium elevation PNG tiles (AWS Terrain Tiles, zoom 14) into a lat/lon elevation grid.
Pure python (zlib only). Usage: terrarium.py <tiles_dir> <south> <west> <north> <east> <out.json>
Elevation (m) = (R * 256 + G + B / 256) - 32768.
"""
import json, math, os, struct, sys, zlib

tiles_dir, south, west, north, east, out = sys.argv[1], float(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]), float(sys.argv[5]), sys.argv[6]
Z = 14
N = 2 ** Z


def decode_png(path):
    data = open(path, "rb").read()
    assert data[:8] == b"\x89PNG\r\n\x1a\n", path
    pos, idat, w, h, ct, bd = 8, b"", 0, 0, 0, 0
    while pos < len(data):
        ln = struct.unpack(">I", data[pos:pos + 4])[0]
        typ = data[pos + 4:pos + 8]
        body = data[pos + 8:pos + 8 + ln]
        if typ == b"IHDR":
            w, h, bd, ct = struct.unpack(">IIBB", body[:10])
            assert bd == 8, f"bit depth {bd}"
        elif typ == b"IDAT":
            idat += body
        pos += 12 + ln
    raw = zlib.decompress(idat)
    ch = {2: 3, 6: 4, 0: 1, 4: 2}[ct]
    stride = w * ch
    rows, prev = [], bytearray(stride)
    p = 0
    for y in range(h):
        f = raw[p]; p += 1
        line = bytearray(raw[p:p + stride]); p += stride
        for i in range(stride):
            a = line[i - ch] if i >= ch else 0
            b = prev[i]
            c = prev[i - ch] if i >= ch else 0
            if f == 1: line[i] = (line[i] + a) & 255
            elif f == 2: line[i] = (line[i] + b) & 255
            elif f == 3: line[i] = (line[i] + (a + b) // 2) & 255
            elif f == 4:
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if pa <= pb and pa <= pc else (b if pb <= pc else c)
                line[i] = (line[i] + pr) & 255
        rows.append(line)
        prev = line
    return w, h, ch, rows


def tile_xy(lat, lon):
    x = (lon + 180.0) / 360.0 * N
    lr = math.radians(lat)
    y = (1.0 - math.log(math.tan(lr) + 1.0 / math.cos(lr)) / math.pi) / 2.0 * N
    return x, y


def elev_at(lat, lon, cache):
    x, y = tile_xy(lat, lon)
    tx, ty = int(x), int(y)
    px, py = int((x - tx) * 256), int((y - ty) * 256)
    key = (tx, ty)
    if key not in cache:
        path = os.path.join(tiles_dir, f"{tx}_{ty}.png")
        cache[key] = decode_png(path) if os.path.exists(path) else None
    t = cache[key]
    if t is None:
        return None
    w, h, ch, rows = t
    r, g, b = rows[py][px * ch], rows[py][px * ch + 1], rows[py][px * ch + 2]
    return (r * 256 + g + b / 256.0) - 32768.0


# sample a regular grid in metres (SRTM is ~30 m; sample every 10 m and let the game smooth it)
step_m = 10.0
m_lat = 111320.0
m_lon = 111320.0 * math.cos(math.radians((south + north) / 2))
rows_n = int((north - south) * m_lat / step_m) + 1
cols_n = int((east - west) * m_lon / step_m) + 1
cache = {}
grid = []
missing = 0
for j in range(rows_n):
    lat = north - j * step_m / m_lat  # row 0 = north edge
    row = []
    for i in range(cols_n):
        lon = west + i * step_m / m_lon
        e = elev_at(lat, lon, cache)
        if e is None:
            missing += 1; e = 0.0
        row.append(round(e, 1))
    grid.append(row)
flat = [v for r in grid for v in r]
print(f"grid {cols_n} x {rows_n} at {step_m} m, elevation {min(flat):.0f}..{max(flat):.0f} m, missing {missing}")
json.dump({"south": south, "west": west, "north": north, "east": east, "step_m": step_m, "cols": cols_n, "rows": rows_n,
           "m_per_deg_lat": m_lat, "m_per_deg_lon": m_lon, "heights": grid}, open(out, "w"))
print("wrote", out)
