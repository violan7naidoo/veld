"""Merge eThekwini Municipality open data into a Veld map.

Used by osm_to_map.py when it is given the file written by ethekwini_fetch.py.
It adds, in game coordinates:

- an address (number, street, suburb) on every building whose centroid falls in
  a zoning parcel, or that stands within 25 m of an address point;
- a better kind for generic buildings from the parcel's zoning (worship,
  school, commercial, industrial, public);
- points of interest with real names for public places (schools, places of
  worship, library, hall, pool, clinics, hospital, shopping centres, municipal
  offices, railway station), skipping any that OpenStreetMap already has;
- the suburb polygons.

Data (c) eThekwini Municipality, Open GIS Data (accuracy not guaranteed).
"""
import re
from collections import Counter

ATTRIBUTION = "Addresses, zoning, suburbs and public places (c) eThekwini Municipality, Open GIS Data."

ZONING_KIND = {
    "WORSHIP": "place_of_worship",
    "EDUCATION": "school",
    "GENERAL BUSINESS 2": "commercial",
    "LIMITED COMMERCIAL": "commercial",
    "GENERAL COMMERCIAL": "commercial",
    "OFFICE": "commercial",
    "LIGHT INDUSTRY": "industrial",
    "GENERAL INDUSTRY": "industrial",
    "FUELLING AND SERVICE STATION": "retail",
    "GOVERNMENT AND MUNICIPAL RESERVATION": "public",
    "INSTITUTIONAL 1": "public",
    "INSTITUTIONAL 2": "public",
    "INSTITUTIONAL 3": "public",
    "CULTURAL AND ENTERTAINMENT": "public",
}

SMALL_WORDS = {"of", "and", "the", "for", "de"}


def title(s):
    """'HARE KRISHNA TEMPLE OF UNDERSTANDING' -> 'Hare Krishna Temple of Understanding'; keeps hyphen parts."""
    out = []
    for i, word in enumerate(str(s).strip().split()):
        lw = word.lower()
        if i > 0 and lw in SMALL_WORDS:
            out.append(lw)
        else:
            out.append("-".join(p[:1].upper() + p[1:].lower() for p in lw.split("-")))
    return " ".join(out)


def fix_case(name):
    """Municipal names come in every case; all-caps names get title case, mixed names only lose capitalised small words."""
    name = " ".join(str(name or "").split())
    if name.isupper():
        return title(name)
    return re.sub(r"(?<=\s)(Of|And|The|For)(?=\s)", lambda mm: mm.group(1).lower(), name)


def street_name(strname, strtype):
    """Municipal name and type to the form people use: 'Road 723', 'Rainbow Circle', 'Arena Park Drive'."""
    name = " ".join(str(strname or "").split())
    kind = " ".join(str(strtype or "").split())
    if not name:
        return ""
    if name.isdigit() and kind.upper() == "ROAD" and len(name) <= 3:
        return f"Road {name}"
    if name.isdigit():
        return ""  # placeholder codes such as "120099 Street" mean the street has no name yet
    return title(f"{name} {kind}".strip())


def school_name(name, schooltype):
    """'Montford P' + 'Primary' -> 'Montford Primary School'."""
    base = " ".join(str(name or "").split())
    if "school" in base.lower() or "college" in base.lower():
        return fix_case(base)
    m = re.match(r"^(.*?)\s+([PSC])$", base)
    if m:
        base = m.group(1)
    suffix = {"Primary": "Primary School", "Secondary": "Secondary School", "Combined": "Combined School"}.get(str(schooltype or "").strip(), "School")
    return f"{base} {suffix}"


def worship_kind(*texts):
    t = " ".join(str(x or "") for x in texts).lower()
    if any(w in t for w in ("mosque", "musjid", "masjid", "jamaat")):
        return "mosque"
    if any(w in t for w in ("temple", "aalayam", "mandir", "gurukulam", "sabha", "ashram", "krishna", "shree", "siva", "shiva")):
        return "temple"
    if any(w in t for w in ("church", "chapel", "ministr", "pentecostal", "catholic", "baptist", "revival", "gospel")):
        return "church"
    return "place_of_worship"


# ------------------------------------------------------------------ geometry

def point_in_ring(p, ring):
    x, z = p
    inside = False
    n = len(ring)
    for i in range(n):
        x1, z1 = ring[i]
        x2, z2 = ring[(i + 1) % n]
        if (z1 > z) != (z2 > z):
            xi = x1 + (z - z1) * (x2 - x1) / (z2 - z1)
            if xi > x:
                inside = not inside
    return inside


def centroid(poly):
    return (sum(p[0] for p in poly) / len(poly), sum(p[1] for p in poly) / len(poly))


class Grid:
    """Buckets shapes by bounding box so point lookups only test nearby shapes."""

    def __init__(self, cell):
        self.cell = cell
        self.cells = {}

    def _key(self, x, z):
        return (int(x // self.cell), int(z // self.cell))

    def add_box(self, idx, minx, minz, maxx, maxz):
        k0, k1 = self._key(minx, minz), self._key(maxx, maxz)
        for cx in range(k0[0], k1[0] + 1):
            for cz in range(k0[1], k1[1] + 1):
                self.cells.setdefault((cx, cz), []).append(idx)

    def add_point(self, idx, x, z):
        self.cells.setdefault(self._key(x, z), []).append(idx)

    def near(self, x, z, reach=1):
        k = self._key(x, z)
        for cx in range(k[0] - reach, k[0] + reach + 1):
            for cz in range(k[1] - reach, k[1] + reach + 1):
                for idx in self.cells.get((cx, cz), ()):
                    yield idx


# ------------------------------------------------------------------ the merge

def apply(eth, xz, inside, buildings, areas, pois):
    """Mutates buildings and pois; returns (suburbs, attribution, stats)."""
    layers = eth["layers"]
    stats = Counter()

    def rings_xz(feature):
        out = []
        for ring in feature.get("geometry", {}).get("rings", []):
            pts = [xz(lat, lon) for lon, lat in ring]
            if len(pts) >= 3:
                out.append(pts)
        return out

    def point_xz(feature):
        g = feature.get("geometry", {})
        return xz(g["y"], g["x"]) if "x" in g else None

    # parcels: one entry per ring, indexed by bounding box
    parcels, pgrid = [], Grid(60.0)
    for f in layers.get("parcels", {}).get("features", []):
        a = f["attributes"]
        for ring in rings_xz(f):
            xs = [p[0] for p in ring]
            zs = [p[1] for p in ring]
            idx = len(parcels)
            parcels.append((ring, a))
            pgrid.add_box(idx, min(xs), min(zs), max(xs), max(zs))

    # address points, indexed by position
    points, agrid = [], Grid(25.0)
    for f in layers.get("addresses", {}).get("features", []):
        p = point_xz(f)
        if p is None:
            continue
        agrid.add_point(len(points), p[0], p[1])
        points.append((p, f["attributes"]))

    # places of worship, for naming the kind of a worship-zoned building
    worship_pts = []
    for f in layers.get("worship", {}).get("features", []):
        p = point_xz(f)
        if p is not None:
            worship_pts.append((p, worship_kind(f["attributes"].get("TYPE"), f["attributes"].get("NAME"))))

    # suburbs
    suburbs = []
    for f in layers.get("suburbs", {}).get("features", []):
        rings = rings_xz(f)
        if not rings:
            continue
        ring = max(rings, key=len)
        suburbs.append({"name": title(f["attributes"].get("SUBURB", "")), "polygon": [[round(x, 1), round(z, 1)] for x, z in ring]})
    montford = next((s["polygon"] for s in suburbs if s["name"] == "Montford"), None)

    def parcel_at(p):
        for idx in pgrid.near(p[0], p[1], 0):
            ring, a = parcels[idx]
            if point_in_ring(p, ring):
                return a
        return None

    def address_point_near(p, max_dist):
        best, best_d = None, max_dist * max_dist
        for idx in agrid.near(p[0], p[1], 1):
            q, a = points[idx]
            d = (q[0] - p[0]) ** 2 + (q[1] - p[1]) ** 2
            if d < best_d:
                best, best_d = a, d
        return best

    # --- buildings: address and kind
    in_montford = with_address_montford = 0
    for b in buildings:
        c = centroid(b["polygon"])
        a = parcel_at(c)
        source = "parcel"
        if a is None or not str(a.get("STRNUM") or "").strip():
            a = address_point_near(c, 25.0)
            source = "point"
        street = street_name(a.get("STRNAME"), a.get("STRTYPE")) if a is not None else ""
        if a is not None and str(a.get("STRNUM") or "").strip() and street:
            b["number"] = str(a["STRNUM"]).strip()
            b["street"] = street
            b["suburb"] = title(a.get("SUBURB") or "")
            stats["address:" + source] += 1
        else:
            stats["address:none"] += 1
        zoning = (parcel_at(c) or {}).get("ZONING")
        if b.get("kind") == "yes" and zoning in ZONING_KIND:
            kind = ZONING_KIND[zoning]
            if kind == "place_of_worship":
                near = [(k, (q[0] - c[0]) ** 2 + (q[1] - c[1]) ** 2) for q, k in worship_pts]
                near = [k for k, d in sorted(near, key=lambda t: t[1]) if d < 80 * 80]
                kind = near[0] if near else kind
            b["kind"] = kind
            stats["kind:" + kind] += 1
        if montford and point_in_ring(c, montford):
            in_montford += 1
            if b.get("number"):
                with_address_montford += 1

    # --- points of interest: real public places, skipping what OSM already names
    named_spots = []  # (kind group, xz) for things already in the map
    for p in pois:
        if p.get("name"):
            named_spots.append((p["kind"], tuple(p["position"])))
    for a in areas:
        if a.get("name") and a["kind"] in ("school", "education", "stadium", "park", "pitch", "sports_centre"):
            named_spots.append((a["kind"], centroid(a["polygon"])))

    def taken(kinds, p, radius):
        r2 = radius * radius
        for kind, q in named_spots:
            if kind in kinds and (q[0] - p[0]) ** 2 + (q[1] - p[1]) ** 2 < r2:
                return True
        return False

    def add_poi(kind, name, p, dedupe_kinds, radius):
        name = fix_case(name)
        if not name or not inside(p) or taken(dedupe_kinds, p, radius):
            stats["poi:skipped"] += 1
            return
        pois.append({"kind": kind, "name": name, "position": [round(p[0], 1), round(p[1], 1)], "source": "ethekwini"})
        named_spots.append((kind, p))
        stats["poi:" + kind] += 1

    for f in layers.get("schools", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("school", school_name(f["attributes"].get("NAME"), f["attributes"].get("SCHOOLTYPE")), p, ("school", "education"), 160.0)
    for f in layers.get("worship", {}).get("features", []):
        p = point_xz(f)
        a = f["attributes"]
        if p:
            add_poi(worship_kind(a.get("TYPE"), a.get("NAME")), a.get("NAME"), p, ("mosque", "temple", "church", "place_of_worship"), 40.0)
    for f in layers.get("heritage", {}).get("features", []):
        p = point_xz(f)
        a = f["attributes"]
        if p:
            sub = str(a.get("SUB_CATEGO") or "").lower()
            kind = worship_kind(sub, a.get("NAME")) if sub in ("temple", "church", "mosque") else "community_centre"
            add_poi(kind, title(a.get("NAME")), p, ("mosque", "temple", "church", "place_of_worship", "community_centre"), 160.0)
    for f in layers.get("clinics", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("clinic", f["attributes"].get("NAME", "") + " Clinic", p, ("clinic", "doctors"), 60.0)
    for f in layers.get("hospitals", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("hospital", f["attributes"].get("NAME"), p, ("hospital",), 150.0)
    for f in layers.get("shopping_centres", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("shopping_centre", f["attributes"].get("Shopping_C"), p, ("shopping_centre", "shop:mall", "shop:supermarket"), 60.0)
    for f in layers.get("libraries", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("library", title(f["attributes"].get("LIBRARY", "")) + " Library", p, ("library",), 60.0)
    for f in layers.get("halls", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("community_centre", title(f["attributes"].get("NAME", "")), p, ("community_centre",), 60.0)
    for f in layers.get("pools", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("swimming_pool", f["attributes"].get("Name", "") + " Pool", p, ("swimming_pool",), 60.0)
    for f in layers.get("police", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("police", f["attributes"].get("NAME", "") + " Police Station", p, ("police",), 200.0)
    for f in layers.get("fire", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("fire_station", f["attributes"].get("NAME"), p, ("fire_station",), 200.0)
    for f in layers.get("railway_stations", {}).get("features", []):
        p = point_xz(f)
        if p:
            add_poi("railway_station", title(f["attributes"].get("RAILWAY_ST", "")), p, ("railway_station", "station"), 100.0)
    for f in layers.get("municipal_offices", {}).get("features", []):
        p = point_xz(f)
        a = f["attributes"]
        office_type = " ".join(str(a.get("TYPE") or "").split())
        if p and office_type.lower() != "parks":
            name = title(a.get("DEPOTNAME", ""))
            if office_type and office_type.lower() not in name.lower():
                name += f" ({office_type})"
            add_poi("townhall", name, p, ("townhall",), 20.0)

    stats["buildings_in_montford"] = in_montford
    stats["addressed_in_montford"] = with_address_montford
    return suburbs, ATTRIBUTION, stats


def report(stats):
    total = sum(v for k, v in stats.items() if k.startswith("address:"))
    print(f"ethekwini: {stats['address:parcel']} addresses from parcels, {stats['address:point']} from address points, {stats['address:none']} buildings without ({total} buildings)")
    if stats["buildings_in_montford"]:
        pct = 100.0 * stats["addressed_in_montford"] / stats["buildings_in_montford"]
        print(f"ethekwini: Montford coverage {stats['addressed_in_montford']}/{stats['buildings_in_montford']} buildings = {pct:.1f}%")
    kinds = {k[5:]: v for k, v in stats.items() if k.startswith("kind:")}
    print(f"ethekwini: building kinds from zoning {dict(sorted(kinds.items()))}")
    poi = {k[4:]: v for k, v in stats.items() if k.startswith("poi:")}
    print(f"ethekwini: points of interest added {dict(sorted(poi.items()))}")
