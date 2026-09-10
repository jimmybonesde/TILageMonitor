#!/usr/bin/env python3
"""Assemble staged base64 chunks into real PNG assets and remove staging files."""
from __future__ import annotations
import base64
from pathlib import Path

ROOT = Path("docs/assets")

def assemble(prefix: str, out_name: str) -> None:
    parts = sorted(ROOT.glob(f"_b64_{prefix}_*.txt"))
    if not parts:
        raise SystemExit(f"no chunks for {prefix}")
    b64 = "".join(p.read_text().strip() for p in parts)
    data = base64.b64decode(b64)
    if data[:8] != b"\x89PNG\r\n\x1a\n":
        raise SystemExit(f"bad PNG signature for {out_name}: {data[:8]!r}")
    out = ROOT / out_name
    out.write_bytes(data)
    print(f"wrote {out} ({len(data)} bytes)")
    for p in parts:
        p.unlink()
        print(f"removed {p}")

assemble("light", "screenshot-light.png")
assemble("dark", "screenshot-dark.png")
print("done")
