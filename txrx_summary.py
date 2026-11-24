#!/usr/bin/env python3
"""Merge txrx_loop.py reports (out/txrx_loop168_<tag>.md) into one table. usage: txrx_summary.py TAG:LABEL ..."""
import os, sys
HERE = os.path.dirname(os.path.abspath(__file__))
rows = ["| set | case | IDs on RX radio | tone (RX PCM) | SNR | firmware meter | result |", "|---|---|---|---|---|---|---|"]
n = ok = 0
for spec in sys.argv[1:]:
    tag, _, label = spec.partition(":")
    p = os.path.join(HERE, "out", "txrx_loop168%s.md" % tag)
    if not os.path.exists(p):
        rows.append("| %s | (missing %s) | | | | | |" % (label, os.path.basename(p))); continue
    for l in open(p):
        f = [x.strip() for x in l.split("|")]
        if len(f) < 17 or f[1] in ("case", "") or f[1].startswith("---"):
            continue
        n += 1; ok += f[15] == "PASS"
        tone = f[10] + " Hz" if f[10] not in ("", "None") else "–"
        snr = f[11] + " dB" if f[11] not in ("", "None") else "–"
        rows.append("| %s | %s | %s | %s | %s | %s | %s (%s) |" % (label, f[1].replace(tag, ""), f[13] or "–", tone, snr, f[12] or "–", f[15], f[14]))
print("\n".join(rows))
print("\n%d/%d PASS" % (ok, n))
