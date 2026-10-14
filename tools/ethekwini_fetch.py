"""Fetch eThekwini Municipality open GIS data for a map's bounding box.

    python tools/ethekwini_fetch.py tools/source/montford_elevation.json tools/source/montford_ethekwini.json

The municipality publishes its GIS layers on ArcGIS Online under the account
opendata_ethekwini (https://gis-ethekwini.opendata.arcgis.com). This pulls the
layers the map converter uses: street address points, zoning parcels (the
cadastre, with the address of each erf), suburb polygons, and the public
facilities (schools, places of worship, clinics, halls, libraries, pools,
shopping centres, stadia, parks, police, fire, hospital, railway stations).

The bounding box comes from the elevation file written by terrarium.py
(south, west, north, east) plus a margin. Data (c) eThekwini Municipality; the
portal states that accuracy is not guaranteed.
"""
import json, math, os, sys, time, urllib.parse, urllib.request

BASE = "https://services3.arcgis.com/HO0zfySJshlD6Twu/arcgis/rest/services"
UA = "VeldPrototype/1.0 (game map research; contact: local dev)"
ATTRIBUTION = "Addresses, parcels, suburbs and facilities (c) eThekwini Municipality, Open GIS Data"

# key -> ArcGIS layer name. Every layer is read with all its fields.
LAYERS = {
    "addresses": "Street_Address",
    "parcels": "Zoning",
    "suburbs": "Suburbs",
    "schools": "Educational_Institutions",
    "worship": "Places_of_Worship",
    "clinics": "Fixed_Clinics",
    "health_centres": "Community_Health_Centres",
    "hospitals": "Hospitals",
    "shopping_centres": "Shopping_Centres",
    "stadia": "Existing_Stadia",
    "parks": "Regional_Parks",
    "police": "SAPS_Stations",
    "fire": "Fire_Stations",
    "libraries": "Libraries",
    "halls": "Community_Halls",
    "pools": "Swimming_Pools",
    "municipal_offices": "Municipal_Offices",
    "welfare": "Social_Welfare_Organizations",
    "railway_stations": "Railway_Stations",
    "taxi_ranks": "Bus_Taxi_Ranks",
    "heritage": "Culture_Historical",
}


def get_json(url, attempts=3):
    last = None
    for _ in range(attempts):
        try:
            req = urllib.request.Request(url, headers={"User-Agent": UA})
            with urllib.request.urlopen(req, timeout=180) as r:
                return json.load(r)
        except Exception as e:  # noqa: BLE001 - retry any network failure
            last = e
            time.sleep(3)
    raise last


def fetch_layer(name, envelope):
    """Every feature of one layer that intersects the envelope, in WGS84, paged."""
    info = get_json(f"{BASE}/{name}/FeatureServer/0?f=json")
    features, offset = [], 0
    while True:
        params = {
            "where": "1=1", "geometry": json.dumps(envelope), "geometryType": "esriGeometryEnvelope",
            "inSR": 4326, "spatialRel": "esriSpatialRelIntersects", "outFields": "*", "outSR": 4326,
            "f": "json", "resultOffset": offset, "resultRecordCount": 2000,
        }
        page = get_json(f"{BASE}/{name}/FeatureServer/0/query?{urllib.parse.urlencode(params)}")
        if "error" in page:
            raise RuntimeError(f"{name}: {page['error']}")
        batch = page.get("features", [])
        features += batch
        if not page.get("exceededTransferLimit") or not batch:
            break
        offset += len(batch)
        time.sleep(0.5)
    return {
        "layer": name,
        "geometryType": info.get("geometryType"),
        "copyright": info.get("copyrightText", ""),
        "features": features,
    }


def main():
    elev_path, out_path = sys.argv[1], sys.argv[2]
    elev = json.load(open(elev_path, encoding="utf-8"))
    margin_m = float(sys.argv[3]) if len(sys.argv) > 3 else 300.0
    m_lat = elev.get("m_per_deg_lat", 111320.0)
    m_lon = elev.get("m_per_deg_lon", 111320.0 * math.cos(math.radians((elev["south"] + elev["north"]) / 2)))
    envelope = {
        "xmin": elev["west"] - margin_m / m_lon, "ymin": elev["south"] - margin_m / m_lat,
        "xmax": elev["east"] + margin_m / m_lon, "ymax": elev["north"] + margin_m / m_lat,
        "spatialReference": {"wkid": 4326},
    }
    out = {"source": BASE, "attribution": ATTRIBUTION, "fetched": time.strftime("%Y-%m-%d"), "envelope": envelope, "layers": {}}
    for key, name in LAYERS.items():
        t0 = time.time()
        try:
            layer = fetch_layer(name, envelope)
        except Exception as e:  # noqa: BLE001 - one broken layer must not sink the rest
            print(f"{key:18s} {name:30s} FAILED: {e}", file=sys.stderr)
            continue
        out["layers"][key] = layer
        print(f"{key:18s} {name:30s} {len(layer['features']):6d} features  {time.time() - t0:4.0f}s")
    os.makedirs(os.path.dirname(os.path.abspath(out_path)), exist_ok=True)
    with open(out_path, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False)
    print(f"wrote {out_path} ({os.path.getsize(out_path) / 1e6:.1f} MB)")


if __name__ == "__main__":
    main()
