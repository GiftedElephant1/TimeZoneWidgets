"""Regenerates the Regions table in src/ZoneCatalog.cs from tools/cities.txt.

    python tools/gen-catalog.py

Lines in cities.txt: City|Code|Country|Windows time zone ID|Latitude|Longitude, under [Region] headings.
"""
import io, os, re, sys

root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
src = os.path.join(root, "src", "ZoneCatalog.cs")
data = os.path.join(root, "tools", "cities.txt")

regions = []
codes = {}
for raw in io.open(data, encoding="utf-8"):
    line = raw.strip()
    if not line or line.startswith("#"):
        continue
    if line.startswith("["):
        regions.append((line.strip("[]"), []))
        continue
    parts = [p.strip() for p in line.split("|")]
    if len(parts) != 6:
        raise SystemExit("bad line: " + line)
    city, code, country, tzid, lat, lon = parts
    if len(code) != 3:
        raise SystemExit("code must be 3 letters: " + line)
    if code in codes:
        raise SystemExit("duplicate code %s (%s and %s)" % (code, codes[code], city))
    codes[code] = city
    float(lat); float(lon)
    regions[-1][1].append((city, code, country, tzid, lat, lon))

def cs(s):
    return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'

out = []
out.append("        public static readonly ZoneRegion[] Regions = new ZoneRegion[]")
out.append("        {")
for ri, (name, zones) in enumerate(regions):
    out.append("            new ZoneRegion(%s," % cs(name))
    for zi, (city, code, country, tzid, lat, lon) in enumerate(zones):
        last = zi == len(zones) - 1
        out.append("                new ZoneEntry(%s, %s, %s, %s, %s, %s)%s" % (
            cs(city), cs(code), cs(country), cs(tzid), lat, lon, ")" + ("" if ri == len(regions) - 1 else ",") if last else ","))
out.append("        };")
block = "\n".join(out)

text = io.open(src, encoding="utf-8").read()
pattern = re.compile(r"        public static readonly ZoneRegion\[\] Regions = new ZoneRegion\[\]\n        \{\n.*?\n        \};", re.S)
if not pattern.search(text):
    raise SystemExit("Regions block not found in ZoneCatalog.cs")
text = pattern.sub(lambda m: block, text, count=1)
io.open(src, "w", encoding="utf-8", newline="").write(text)
total = sum(len(z) for _, z in regions)
print("wrote %d regions, %d cities" % (len(regions), total))
