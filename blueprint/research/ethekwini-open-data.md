# eThekwini Municipality open GIS data for the Chatsworth map

## Question

Where can the game get real house numbers, addresses and public places for
Montford (Chatsworth Unit 7), and under what terms?

## Findings

- OpenStreetMap holds 4 `addr:housenumber` tags in the whole Montford box
  (Overpass query on 2026-10-14, `nwr["addr:housenumber"](-29.9304,30.8549,-29.9082,30.8853)`),
  so it cannot be the address source.
- eThekwini Municipality publishes its GIS layers as open data on ArcGIS Online
  under the account `opendata_ethekwini`, hosted at
  `https://services3.arcgis.com/HO0zfySJshlD6Twu/arcgis/rest/services/<Layer>/FeatureServer/0`
  and catalogued at https://gis-ethekwini.opendata.arcgis.com. 81 items were
  listed on 2026-10-14 (`https://www.arcgis.com/sharing/rest/search?q=owner:opendata_ethekwini`).
- `Street_Address`: "The Address Points dataset is a point feature class that
  is continually maintained by Corporate GIS within the Property Management
  System, in accordance with the City's Street Naming and Addressing policy."
  Fields `STRNUM`, `STRNAME`, `STRTYPE`, `SUBURB`, `DISTRICT`, `PLAN_UNIT`,
  `WARDNO`. 11,260 points in the Montford box with a 300 m margin, 3,281 of
  them in the Montford suburb. Numbered roads are stored as `STRNAME=723`,
  `STRTYPE=ROAD`; locals and the municipality write "Road 723".
- `Zoning`: the cadastre. 9,017 parcel polygons in the box, 9,010 with a street
  number, plus `ZONING` (SPECIAL RESIDENTIAL 180 and 400 for houses; WORSHIP,
  EDUCATION, GENERAL BUSINESS 2, LIGHT INDUSTRY, FUELLING AND SERVICE STATION
  and others for the rest).
- `Suburbs`: 11 polygons in the box (Montford, Risecliff, Arena Park,
  Croftdene, Moorton, Westcliff, Woodhurst, Welbedagt East, Chatsworth Town
  Centre, Umlazi-H).
- Facilities with real names in the box: 23 schools (`Educational_Institutions`,
  names abbreviated like "Montford P"), 36 places of worship (`Places_of_Worship`,
  with `TYPE` Temple / Mosque / Church), 1 fixed clinic, 1 hospital (R K Khan),
  4 shopping centres, 2 stadia, 1 regional park, 1 SAPS station, 1 fire station,
  1 library (Montford), 1 community hall (Montford Community Hall), 1 pool
  (Arena Park), 6 municipal offices, 1 railway station (Chatsglen).
- `Building_Footprints` covers only 849 buildings in the box (2016
  photogrammetry, mostly industrial), so OpenStreetMap stays the footprint source.
- Licence: the portal describes itself as an open and free platform for
  searching and downloading GIS datasets. The item metadata carries no licence
  name, only this text: "The accuracy of the data is not guaranteed nor is it
  implied. The eThekwini Municipality accepts no liability for any claims
  brought against it or its employees as a result of the use of supplied
  data." Layer copyright text is "eThekwini Municipality".
- OpenAddresses lists a 546 MB countrywide South African CSV uploaded by a
  user from a Google Drive link (`sources/za/countrywide.json`), with no stated
  provenance, so it was not used.

## Answer

Use the municipal `Street_Address` points and `Zoning` parcels for addresses
(parcel containing the building centroid first, nearest address point second),
`Suburbs` for suburb names, and the facility layers for named public places.
Attribute "eThekwini Municipality, Open GIS Data" wherever OpenStreetMap is
attributed. Fetch with `tools/ethekwini_fetch.py`; merge with
`tools/ethekwini_merge.py` through `osm_to_map.py`.

## Open gaps

- No explicit licence name. Before a commercial store release, confirm the
  terms with eThekwini Corporate GIS (the portal only states the liability
  disclaimer).
- Address points mark property entrances, not buildings; a few buildings on
  corner or shared plots may pick up a neighbour's number.
