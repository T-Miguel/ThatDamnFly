"""Generates app/Tests.Domain/body_fixture.json: reference trajectories of the body (Python, research/tdf/body.py) for the C# parity test (BodyParityTests).
Motor stimulus: (0.4, 0.9) on ticks 100–129; level 2; kitchen food; kinds house/fruitfly/horsefly/boss; case "house_bait" with bait at (0.5, 0.6, 0.05) on ticks 40–239.
Usage: research/.venv/Scripts/python.exe research/scripts/body_fixture.py"""
import json, os, sys
import numpy as np
sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))
from tdf.body import Body, params_for

FOOD = ((0.54, 0.48, 0.07), (0.175, 0.47, 0.06))
CHECK = (50, 100, 120, 150, 200, 300)

def run(kind, seed, bait=None):
    body = Body.edge_entry(seed, params_for(kind, 2, FOOD)); motor = np.zeros(8); took = landed = 0; rows = []
    for t in range(300):
        motor[:] = 0
        if 100 <= t < 130: motor[0] = 0.4; motor[1] = 0.9
        if bait is not None: body.bait = bait if 40 <= t < 240 else None
        body.step(motor); took += body.took_off; landed += body.landed
        if t + 1 in CHECK: rows.append(dict(seed=seed, kind=kind + ("_bait" if bait else ""), t=t + 1, x=round(float(body.p[0]), 6), y=round(float(body.p[1]), 6), phi=round(float(body.phi), 6)))
    rows.append(dict(seed=seed, kind=kind + ("_bait" if bait else ""), t=-1, took=int(took), landed=int(landed)))
    return rows

rows = []
for kind in ("house", "fruitfly", "horsefly", "boss"):
    for seed in range(1, 9): rows += run(kind, seed)
for seed in range(1, 9): rows += run("house", seed, bait=(0.5, 0.6, 0.05))
out = os.path.join(os.path.dirname(__file__), "..", "..", "app", "Tests.Domain", "body_fixture.json")
json.dump(rows, open(out, "w"), indent=0); print(len(rows), "rows ->", os.path.normpath(out))
