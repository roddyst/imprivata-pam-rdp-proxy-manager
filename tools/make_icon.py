#!/usr/bin/env python3
"""Generates src/PamRdpProxyManager/Assets/app.ico (+ app.png) without external dependencies.

Motif: rounded accent-colored tile with a monitor and a small shield (proxy/PAM).
"""
import struct
import zlib
from pathlib import Path

SIZES = [16, 24, 32, 48, 64, 128, 256]
ASSETS = Path(__file__).resolve().parent.parent / "src" / "PamRdpProxyManager" / "Assets"
OUT = ASSETS / "app.ico"
PNG_OUT = ASSETS / "app.png"


def rounded_rect(x, y, x0, y0, x1, y1, r):
    cx = min(max(x, x0 + r), x1 - r)
    cy = min(max(y, y0 + r), y1 - r)
    return (x - cx) ** 2 + (y - cy) ** 2 <= r * r and x0 <= x <= x1 and y0 <= y <= y1


def shield(x, y, cx, top, w, h):
    # Simple shield: rectangle top half, triangle bottom half.
    if not (cx - w / 2 <= x <= cx + w / 2 and top <= y <= top + h):
        return False
    mid = top + h * 0.45
    if y <= mid:
        return True
    t = (y - mid) / (top + h - mid)
    return abs(x - cx) <= (w / 2) * (1 - t)


def sample(u, v):
    """u, v in [0,1]; returns RGBA."""
    if not rounded_rect(u, v, 0.03, 0.03, 0.97, 0.97, 0.2):
        return (0, 0, 0, 0)
    # Background gradient (blue -> teal).
    t = (u + v) / 2
    bg = (int(0 + 20 * t), int(95 + 60 * t), int(184 + 20 * t), 255)
    white = (255, 255, 255, 255)
    dark = (12, 46, 92, 255)
    # Monitor frame and screen.
    if rounded_rect(u, v, 0.16, 0.2, 0.84, 0.66, 0.05):
        if rounded_rect(u, v, 0.21, 0.25, 0.79, 0.61, 0.02):
            if shield(u, v, 0.5, 0.31, 0.22, 0.26):
                return white if not shield(u, v, 0.5, 0.345, 0.14, 0.18) else (0, 120, 212, 255)
            return dark
        return white
    # Stand.
    if 0.44 <= u <= 0.56 and 0.66 <= v <= 0.75:
        return white
    if rounded_rect(u, v, 0.32, 0.74, 0.68, 0.8, 0.03):
        return white
    return bg


def render(size):
    ss = 4  # supersampling
    rows = []
    for y in range(size):
        row = bytearray([0])
        for x in range(size):
            acc = [0, 0, 0, 0]
            for sy in range(ss):
                for sx in range(ss):
                    r, g, b, a = sample((x + (sx + 0.5) / ss) / size, (y + (sy + 0.5) / ss) / size)
                    acc[0] += r * a
                    acc[1] += g * a
                    acc[2] += b * a
                    acc[3] += a
            n = ss * ss
            a = acc[3] / n
            if acc[3]:
                row += bytes([int(acc[0] / acc[3]), int(acc[1] / acc[3]), int(acc[2] / acc[3]), int(a)])
            else:
                row += bytes([0, 0, 0, 0])
        rows.append(bytes(row))
    raw = b"".join(rows)

    def chunk(tag, data):
        return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    return (b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(raw, 9)) + chunk(b"IEND", b""))


def main():
    images = [render(s) for s in SIZES]
    header = struct.pack("<HHH", 0, 1, len(images))
    offset = 6 + 16 * len(images)
    entries = b""
    for s, png in zip(SIZES, images):
        dim = 0 if s >= 256 else s
        entries += struct.pack("<BBBBHHII", dim, dim, 0, 0, 1, 32, len(png), offset)
        offset += len(png)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_bytes(header + entries + b"".join(images))
    PNG_OUT.write_bytes(render(128))
    print(f"wrote {OUT} ({OUT.stat().st_size} bytes) and {PNG_OUT}")


if __name__ == "__main__":
    main()
