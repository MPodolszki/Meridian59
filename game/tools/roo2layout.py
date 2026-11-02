"""Erzeugt aus einer Original-.roo-Datei ein grobes Höhenraster (ein Wert pro Grid-Feld) für den
neuen Client. Das ist ein PLATZHALTER: Es übernimmt nur den Grundriss (welches Feld ist Straße,
Dach oder Wand), keine Texturen oder Geometrie. Wird ersetzt, sobald eigene Karten existieren.

Aufruf (im Repo-Root):
    python -I game/tools/roo2layout.py game/server/data/world/tos.json resource/rooms game/client/rooms

Ausgabe je Raum: <roo-name>.json mit
    rows, cols   – Größe in Grid-Feldern
    floor        – Liste von Zeilen (Zeile 1 zuerst), je Feld Bodenhöhe in KOD-Einheiten
                   (64 = 1 Feld), -1 = kein Boden (massive Wand / außerhalb)
    ceiling      – höchste Deckenhöhe im Raum (für Innenwände)
    depth        – nur wenn vorhanden: je Zeile ein Text mit der Wassertiefe je Feld (0–3, SF_DEPTH1–3)
    tags         – nur wenn vorhanden: Sektoren mit ID (vom Server per BP_SECTOR_MOVE bewegt, z. B. Türen):
                   [{"id", "floor", "ceiling", "cells": [[zeile, spalte], ...]}] (Felder 1-basiert)
Formatbeschreibung des .roo-Formats: clientd3d/bspload.c.
"""
import json
import struct
import sys
from pathlib import Path


def read_roo(path):
    d = path.read_bytes()
    if d[:4] != bytes([0x52, 0x4F, 0x4F, 0xB1]):
        raise ValueError(f'{path} ist keine .roo-Datei')
    version, _security, main = struct.unpack_from('<iii', d, 4)
    width, height, node_pos, _wall_pos, _, _side_pos, sector_pos = struct.unpack_from('<iiiiiii', d, main)
    if version >= 13:
        def val(o): return struct.unpack_from('<f', d, o)[0]
    else:
        def val(o): return float(struct.unpack_from('<i', d, o)[0])

    # BSP-Blätter: konvexe Bodenpolygone mit Sektornummer
    p = node_pos
    (count,) = struct.unpack_from('<H', d, p); p += 2
    leaves = []
    for _ in range(count):
        node_type = d[p]; p += 1 + 16          # Typ + Bounding Box
        if node_type == 1:                      # innerer Knoten
            p += 12 + 6
        else:                                   # Blatt
            sector, npts = struct.unpack_from('<HH', d, p); p += 4
            pts = [(val(p + i * 8), val(p + i * 8 + 4)) for i in range(npts)]
            p += npts * 8
            if npts >= 3:
                leaves.append((sector, pts))

    # Sektoren: Boden-/Deckenhöhe
    p = sector_pos
    (count,) = struct.unpack_from('<H', d, p); p += 2
    sectors = []
    for _ in range(count):
        sid, _ft, _ct, _tx, _ty, floor_h, ceil_h = struct.unpack_from('<7H', d, p); p += 14
        p += 1                                  # Licht
        (flags,) = struct.unpack_from('<I', d, p); p += 4
        if version >= 10:
            p += 1                              # Animationsgeschwindigkeit
        plane = None
        for sloped in (0x400, 0x800):           # SF_SLOPED_FLOOR / SF_SLOPED_CEILING
            if flags & sloped:
                if sloped == 0x400:             # Ebene a·x + b·y + c·z + d = 0 (bspload.c LoadSlopeInfo)
                    plane = tuple(val(p + 4 * i) for i in range(4))
                p += 46
        sectors.append((floor_h, ceil_h, flags & 3, sid, plane))   # SF_DEPTH: Wassertiefe; sid: Sektor-ID
    return width >> 10, height >> 10, leaves, sectors


def inside(x, y, poly):
    hit = False
    for i in range(len(poly)):
        x1, y1 = poly[i]
        x2, y2 = poly[(i + 1) % len(poly)]
        if (y1 > y) != (y2 > y) and x < (x2 - x1) * (y - y1) / (y2 - y1) + x1:
            hit = not hit
    return hit


def sample(leaves, sectors, x, y):
    """Bodenhöhe (KOD-Einheiten, -1 = kein Boden) und Sektor an einem Punkt (Client-Feineinheiten)."""
    # Blätter verweisen 1-basiert auf Sektoren (0 = keiner), wie bspload.c RoomSwizzle.
    sector = next((s for s, poly in leaves if inside(x, y, poly)), None)
    if not sector or sector > len(sectors):
        return -1, None
    f, c, d, sid, plane = sectors[sector - 1]
    if plane and plane[2]:
        # Geneigter Boden: Höhe aus der Ebene (Client-Feineinheiten, 16 je KOD-Einheit)
        f = round(-(plane[0] * x + plane[1] * y + plane[3]) / plane[2] / 16)
    return f, sectors[sector - 1]


def layout(path):
    cols, rows, leaves, sectors = read_roo(path)
    floor, depth, tags = [], [], {}
    max_ceiling = 0
    for row in range(rows):
        line, wet = [], ''
        for col in range(cols):
            # Fünf Punkte je Feld (Mitte und vier innere Viertelpunkte); die häufigste Höhe gilt, bei Gleichstand die
            # der Mitte. So verfälschen schmale Sektoren (Rinnen, Kanten), die zufällig die Mitte treffen, das Feld nicht.
            samples = [sample(leaves, sectors, col * 1024 + 512 + dx, row * 1024 + 512 + dy)
                       for dx, dy in ((0, 0), (-256, -256), (256, -256), (-256, 256), (256, 256))]
            heights = [h for h, _ in samples]
            best = max(heights, key=lambda h: (heights.count(h), h == heights[0]))
            line.append(best)
            center = samples[0][1]
            chosen = next(sec for h, sec in samples if h == best)
            wet += str(chosen[2]) if chosen else '0'
            if center:
                f, c, d, sid, plane = center
                max_ceiling = max(max_ceiling, c)
                if sid:   # Sektor-IDs (Türen) nach der Feldmitte, damit schmale Türsektoren erhalten bleiben
                    tags.setdefault(sid, {'id': sid, 'floor': f, 'ceiling': c, 'cells': []})['cells'].append([row + 1, col + 1])
        floor.append(line)
        depth.append(wet)
    data = {'source': path.name, 'rows': rows, 'cols': cols, 'ceiling': max_ceiling}
    if any(set(w) - {'0'} for w in depth):
        data['depth'] = depth
    if tags:
        data['tags'] = [tags[k] for k in sorted(tags)]
    data['floor'] = floor
    return data


def main(world_json, roo_dir, out_dir):
    out = Path(out_dir)
    out.mkdir(parents=True, exist_ok=True)
    rooms = json.loads(Path(world_json).read_text(encoding='utf-8'))['rooms']
    for roo in sorted({r['roo'] for r in rooms}):
        src = Path(roo_dir) / roo
        if not src.exists():
            print(f'fehlt: {src}')
            continue
        data = layout(src)
        target = out / (Path(roo).stem.lower() + '.json')   # Client und Server suchen in Kleinschreibung
        # Eine Zeile pro Rasterzeile hält die Datei lesbar und diff-freundlich.
        body = ',\n    '.join(json.dumps(line, separators=(',', ':')) for line in data.pop('floor'))
        head = json.dumps(data, ensure_ascii=False)[:-1]
        target.write_text(f'{head}, "floor": [\n    {body}\n]}}\n', encoding='utf-8')
        print(f'{target}: {data["rows"]}x{data["cols"]}')


if __name__ == '__main__':
    main(*sys.argv[1:4])
