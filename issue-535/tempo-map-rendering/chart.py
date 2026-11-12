# SPDX-License-Identifier: AGPL-3.0-only
# Copyright (c) 2026 Egor Khindikaynen (Nota). See LICENSES/ for license terms.
import csv
import html
import sys
from pathlib import Path

root = Path(sys.argv[1]).resolve()
ids = ['edit-warp-split', 'audio-tails-8', 'instrument-04', 'instrument-01',
       'audio-warp-split', 'instrument-03', 'instrument-17']

def work(side):
    with (root / f'results-{side}.tsv').open() as f:
        return {r[0]: int(r[2]) for r in csv.reader(f, delimiter='\t')}

def timing(side):
    out = {}
    for line in (root / f'timing-{side}.tsv').read_text().splitlines():
        sid, value = line.split()
        out[sid] = min(out.get(sid, float('inf')), float(value))
    return out

def chart(filename, title, subtitle, base, head, limit):
    values = [100 * (head[sid] / base[sid] - 1) for sid in ids]
    lo, hi = min(-limit * 1.2, min(values) * 1.2), max(limit * 1.2, max(values) * 1.2)
    x = lambda v: 260 + (v - lo) / (hi - lo) * 690
    parts = ['<svg xmlns="http://www.w3.org/2000/svg" width="1040" height="600" viewBox="0 0 1040 600">',
             '<rect width="1040" height="600" fill="#fcfcfb"/>',
             '<g font-family="Segoe UI,Arial,sans-serif" fill="#202020">',
             f'<text x="35" y="42" font-size="23" font-weight="600">{html.escape(title)}</text>',
             f'<text x="35" y="72" font-size="14" fill="#52514e">{html.escape(subtitle)}</text>']
    ticks = list(range(int(lo), int(hi) + 1)) if limit == 1 and lo < -1.21 else [-limit, -limit/2, 0, limit/2, limit]
    for tick in ticks:
        color = '#bb3939' if tick == limit else '#deded8'
        parts.append(f'<line x1="{x(tick)}" x2="{x(tick)}" y1="112" y2="480" stroke="{color}"/>')
        parts.append(f'<text x="{x(tick)}" y="505" text-anchor="middle" font-size="13">{tick:+g}%</text>')
    parts.append(f'<text x="{x(limit)}" y="102" text-anchor="middle" fill="#bb3939" font-size="13">Gate +{limit:g}%</text>')
    for i, sid in enumerate(ids):
        y = 130 + i * 50
        value = 100 * (head[sid] / base[sid] - 1)
        left, right = sorted([x(0), x(value)])
        parts.append(f'<text x="240" y="{y+21}" text-anchor="end" font-size="15">{sid}</text>')
        parts.append(f'<rect x="{left}" y="{y}" width="{max(2,right-left)}" height="29" rx="2" fill="#2a78d6"/>')
        label_x = right + 9 if value >= 0 else left - 9
        anchor = 'start' if value >= 0 else 'end'
        parts.append(f'<text x="{label_x}" y="{y+21}" text-anchor="{anchor}" font-size="14">{value:+.2f}%</text>')
    parts += ['<text x="35" y="550" font-size="14">Ubuntu 26.04.1 / GCC 15.2 / Valgrind 3.26 / WSL2 / 24 logical CPUs</text>',
              '<text x="35" y="577" font-size="13" fill="#52514e">Base 39c7857c vs native tree 17a8133c. Original seven regression scenarios shown.</text>',
              '</g></svg>']
    (root / filename).write_text('\n'.join(parts), encoding='utf-8')

chart('instructions-vs-base.svg', 'CPU work with tempo rendering fixes',
      'Lower is better. Same harness and machine for both builds; unchanged instruction gate.',
      work('base'), work('head'), 1)
chart('timing-vs-base.svg', 'Render time with tempo rendering fixes',
      'Lower is better. Fastest of five interleaved rounds; unchanged wall-time gate.',
      timing('base'), timing('head'), 15)
