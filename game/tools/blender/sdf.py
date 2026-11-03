"""Weiche Körper für make_creature.py: Teile (Ellipsoide, abgerundete Kegel) als Distanzfelder, weich vereinigt
(polynomielles smooth-min), mit OpenVDB trianguliert. Gewichte und Farben je Eckpunkt aus den nächsten Teilen gemischt,
Schattierung in den Falten (Umgebungsverdeckung aus dem Feld) in die Eckpunktfarben gebacken.

Nur numpy und das openvdb-Modul, das Blender mitbringt.
"""
import math

import numpy as np
import openvdb as vdb

MAX_CELLS = 9_000_000


class Part:
    """Ein weiches Teil im Ruhezustand des Skeletts."""

    def __init__(self, kind, bone, color, rough, emit, **geo):
        self.kind, self.bone, self.color, self.rough, self.emit = kind, bone, color, rough, emit
        if kind == "ell":
            self.c = np.array(geo["c"], float)
            self.Rt = np.array(geo["R"], float).T          # Welt → lokal
            self.r = np.array(geo["r"], float)
            self.size = float(self.r.min())
            self.big = float(self.r.max())
            lo, hi = self.c - self.big, self.c + self.big
        else:
            self.a, self.b = np.array(geo["a"], float), np.array(geo["b"], float)
            self.r1, self.r2 = float(geo["r1"]), float(geo["r2"])
            self.size = 0.5 * (self.r1 + self.r2)
            self.big = max(self.r1, self.r2)
            lo = np.minimum(self.a, self.b) - self.big
            hi = np.maximum(self.a, self.b) + self.big
        self.lo, self.hi = lo, hi
        self.k = geo.get("blend", 0.55) * self.size          # Rundungsradius der Verschmelzung
        self.bump = geo.get("bump")                            # (Art, Höhe, Maßstab) oder None

    def dist(self, P):
        if self.kind == "ell":
            q = (P - self.c) @ self.Rt.T
            k0 = np.linalg.norm(q / self.r, axis=1)
            k1 = np.linalg.norm(q / (self.r * self.r), axis=1)
            return k0 * (k0 - 1.0) / np.maximum(k1, 1e-9)
        return round_cone(P, self.a, self.b, self.r1, self.r2)


def round_cone(P, a, b, r1, r2):
    """Abgerundeter Kegel (Inigo Quilez, sdRoundCone), vektorisiert."""
    ba = b - a
    l2 = float(ba @ ba)
    rr = r1 - r2
    if rr * rr >= l2 * 0.98:            # eine Kugel schluckt die andere
        return np.minimum(np.linalg.norm(P - a, axis=1) - r1, np.linalg.norm(P - b, axis=1) - r2)
    a2 = l2 - rr * rr
    il2 = 1.0 / l2
    pa = P - a
    y = pa @ ba
    z = y - l2
    v = pa * l2 - np.outer(y, ba)
    x2 = (v * v).sum(1)
    y2 = y * y * l2
    z2 = z * z * l2
    k = math.copysign(1.0, rr) * rr * rr * x2
    out = (np.sqrt(np.maximum(x2 * a2 * il2, 0)) + y * rr) * il2 - r1
    m2 = np.sign(y) * a2 * y2 < k
    out = np.where(m2, np.sqrt(x2 + y2) * il2 - r1, out)
    m1 = np.sign(z) * a2 * z2 > k
    out = np.where(m1, np.sqrt(x2 + z2) * il2 - r2, out)
    return out


def smin(a, b, k):
    h = np.maximum(k - np.abs(a - b), 0.0) / k
    return np.minimum(a, b) - h * h * k * 0.25


class Field:
    """Distanzfeld einer Gruppe auf einem Gitter."""

    def __init__(self, parts, vox):
        lo = np.min([p.lo for p in parts], axis=0)
        hi = np.max([p.hi for p in parts], axis=0)
        pad = max(p.k for p in parts) + 4 * vox
        lo, hi = lo - pad, hi + pad
        cells = np.prod((hi - lo) / vox)
        if cells > MAX_CELLS:
            vox *= (cells / MAX_CELLS) ** (1 / 3)
        self.vox, self.lo = vox, lo
        self.shape = tuple(int(n) for n in np.ceil((hi - lo) / vox) + 1)
        self.pad = 4 * vox
        F = np.full(self.shape, self.pad, np.float32)
        for p in parts:
            m = p.k + self.pad
            i0 = np.maximum(np.floor((p.lo - m - lo) / vox).astype(int), 0)
            i1 = np.minimum(np.ceil((p.hi + m - lo) / vox).astype(int) + 1, self.shape)
            axes = [lo[d] + np.arange(i0[d], i1[d]) * vox for d in range(3)]
            G = np.stack(np.meshgrid(*axes, indexing="ij"), -1).reshape(-1, 3)
            d = p.dist(G)
            if p.bump:
                d = d - p.bump[1] * relief(G, p.bump[0], p.bump[2])
            d = d.reshape(tuple(i1 - i0)).astype(np.float32)
            blk = F[i0[0]:i1[0], i0[1]:i1[1], i0[2]:i1[2]]
            F[i0[0]:i1[0], i0[1]:i1[1], i0[2]:i1[2]] = smin(blk, d, max(p.k, 1e-5))
        # Boden: nichts unter z = 0 (flache Saumkanten bei Roben)
        z = lo[2] + np.arange(self.shape[2]) * vox
        F = np.maximum(F, (-z)[None, None, :].astype(np.float32))
        self.F = F

    def polygons(self):
        g = vdb.FloatGrid(self.pad)
        g.copyFromArray(self.F)
        pts, tris, quads = g.convertToPolygons(isovalue=0.0, adaptivity=0.0)
        return self.lo + np.asarray(pts, float) * self.vox, np.asarray(tris), np.asarray(quads)

    def sample(self, P):
        """Trilinear; außerhalb des Gitters: weit draußen."""
        u = (P - self.lo) / self.vox
        i = np.floor(u).astype(int)
        f = u - i
        n = np.array(self.shape)
        ok = np.all((i >= 0) & (i < n - 1), axis=1)
        i = np.clip(i, 0, n - 2)
        out = np.zeros(len(P))
        for dx in (0, 1):
            for dy in (0, 1):
                for dz in (0, 1):
                    w = (f[:, 0] if dx else 1 - f[:, 0]) * (f[:, 1] if dy else 1 - f[:, 1]) * (f[:, 2] if dz else 1 - f[:, 2])
                    out += w * self.F[i[:, 0] + dx, i[:, 1] + dy, i[:, 2] + dz]
        return np.where(ok, out, self.pad)


def noise(P, scale, seed=0):
    """Glattes Pseudo-Rauschen (Summe schräger Sinuswellen), Werte etwa −1 … 1."""
    rng = np.random.default_rng(seed)
    out = np.zeros(len(P))
    for octave, amp in ((1, 0.6), (2.3, 0.3), (5.1, 0.15)):
        for _ in range(4):
            d = rng.normal(size=3)
            d /= np.linalg.norm(d)
            out += amp * np.sin(P @ d * (2 * math.pi * octave / scale) + rng.uniform(0, 6.3))
    return out / 2.1


def relief(P, kind, scale):
    """Oberflächenrelief 0 … 1 (1 = erhaben): Warzen, Schuppen, Fell, Falten, Rinde."""
    if kind == "warts":
        return np.clip(noise(P, scale, 11) * 1.6 - 0.5, 0, 1) ** 2
    if kind == "scales":
        rng = np.random.default_rng(5)
        acc = np.zeros(len(P))
        for _ in range(3):
            d = rng.normal(size=3)
            d /= np.linalg.norm(d)
            acc += np.abs(np.sin(P @ d * math.pi / scale + noise(P, scale * 4, 2)))
        return np.clip(acc / 3, 0, 1) ** 0.5
    if kind == "fur":
        n = noise(P * np.array([1, 1, 0.35]), scale, 7) + 0.5 * noise(P, scale * 0.45, 8)
        return np.clip(0.5 + n * 0.8, 0, 1)
    if kind == "bark":
        n = noise(P * np.array([1, 1, 0.12]), scale, 9)
        return np.clip(1 - np.abs(n) * 2.2, 0, 1)
    return np.clip(0.5 + noise(P, scale, 4) * 0.7, 0, 1)            # Falten, Knubbel


def pattern(P, pat):
    """Maske 0 … 1 für Fellzeichnung: Flecken, Streifen (quer zur Längsachse), Sprenkel."""
    kind, scale = pat["kind"], pat["scale"]
    if kind == "bands":
        return np.clip(np.sin(P[:, 2] * 2 * math.pi / scale + P[:, 0] * 3 + noise(P, scale * 3, 22) * 1.5) * 2.5, 0, 1)
    if kind == "stripes":
        return np.clip(np.sin(P[:, 1] * 2 * math.pi / scale + noise(P, scale * 2, 21) * 2.5) * 3, 0, 1)
    if kind == "spots":
        return np.clip(noise(P, scale, 23) * 3 - 1.0, 0, 1)
    return np.clip(noise(P, scale, 25) * 2 + 0.2, 0, 1)


def shade(field, parts, V, N, size, bone_index, pat=None):
    """Gewichte (je Eckpunkt bis 3 Knochen), Farbe und vorherrschendes Teil für die Eckpunkte V mit Normalen N."""
    D = np.stack([p.dist(V) for p in parts], 1)                      # V × P
    dmin = D.min(1, keepdims=True)
    s = np.array([max(p.k * 0.6, field.vox * 2) for p in parts])
    W = np.exp(-(D - dmin) / s)
    nb = max(bone_index.values()) + 1
    B = np.zeros((len(V), nb))
    for j, p in enumerate(parts):
        B[:, bone_index[p.bone]] += W[:, j]
    top = np.argsort(-B, 1)[:, :min(3, nb)]
    Bt = np.take_along_axis(B, top, 1)
    Bt /= Bt.sum(1, keepdims=True)
    Wc = np.exp(-(D - dmin) / (s * 0.3))
    Wc /= Wc.sum(1, keepdims=True)
    col = Wc @ np.array([p.color for p in parts])
    if pat:
        m = pattern(V, pat) * pat.get("amount", 1.0)
        col = col * (1 - m[:, None]) + np.array(pat["color"]) * m[:, None]
    # Relief auch in der Farbe: Vertiefungen dunkler
    rel = np.zeros(len(V))
    wsum = np.zeros(len(V))
    for j, p in enumerate(parts):
        if p.bump:
            rel += Wc[:, j] * (relief(V, p.bump[0], p.bump[2]) - 0.5)
            wsum += Wc[:, j]
    # Umgebungsverdeckung aus dem Feld (Falten, Achseln, Gelenke dunkler)
    occ, sca, hs = np.zeros(len(V)), 1.0, 0.0
    for i in range(5):
        h = size * (0.004 + 0.035 * i / 4)
        occ += (h - np.minimum(field.sample(V + N * h), h)) * sca
        hs += h * sca
        sca *= 0.85
    ao = np.clip(1 - 1.8 * occ / hs, 0, 1)
    shade = ((0.35 + 0.65 * ao) * (1 + 0.12 * noise(V, size * 0.18, 3)) * (1 + 0.12 * noise(V, size * 0.04, 6))
             * (0.92 + 0.1 * N[:, 2]) * (1 + 0.45 * rel))
    col = np.clip(col * shade[:, None], 0, 1)
    return top, Bt, col, D.argmin(1)
