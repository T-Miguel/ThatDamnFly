# Sensory model v1: encoding of materialized stimuli

Applies to the game (C#) and the trials (Python). The C# implementation must reproduce this specification; parity fixtures live in `research/out/fixtures/sensory_*.json`.

## Allowed inputs
**rulesVersion 2 (ADR-001):** tools come down over the target: the threat is a disc at the target with radius `r·(0.3 + 0.7·t)` during preparation and `r` in the active phase; the expansion results from the growth of the radius. It is still a materialized, visible object.

Only objects **materialized in the world**: the tool during its **visible preparation** (the cloth/pan already moving to the fixed target) and during the **active** phase; the fridge only from the release of the aim (the aim does not exist for the fly). The edges of the arena. Never: the position of the finger/aim, the selected tool, fury, cooldowns, history, assistance.

## Frame of reference
Fly at position **p** with heading **φ** (rad, 0 = +x, positive = counter-clockwise). Azimuth of a point **q**: α = wrap(atan2(q − p) − φ) ∈ (−π, π]; positive = left. Sectors: s = round(α / 45°) mod 8 → 0 front, 1–3 left (45°, 90°, 135°), 4 back, 5–7 right (225°→−135°, 270°→−90°, 315°→−45°).

## Per threatening object (disc of radius r, center **q**, velocity **v**)
- d = |q − p| − r (distance to the surface), d_min = 0.005 W.
- Angular size θ = 2·atan(r / max(d, d_min)); `angular_size = min(1, θ / π)`.
- Expansion: `expansion = clip(dθ/dt / 12 rad·s⁻¹, 0, 2)` with dθ/dt by finite difference between ticks (10 ms); positive part only (approach).
- Relative motion: relative velocity **v_rel = v − v_fly**; radial component (approach, positive = getting closer) `motion_v = clip(−(v_rel · ê_r) / 2 W·s⁻¹, −1, 1)`; tangential component `motion_h = clip((v_rel · ê_t) / 2 W·s⁻¹, −1, 1)` with ê_t = ê_r rotated +90°.
- Distribution over sectors: the value goes to the sector s of the center's azimuth; if θ > 45°, also to the adjacent sectors with a 0.5 factor (a large object covers more of the visual field). Several objects: per sector, keep the **maximum** of expansion/angular_size and the clipped sum of motion.

## Edges (surface_proximity)
For each sector, the distance from the fly to the arena edge in the sector's central direction: `surface_proximity = clip(1 − dist / 0.10 W, 0, 1)`.

## Background
`background_drive = 1` whenever the simulation is active (a model parameter; not a stimulus).

## Mapping to populations (protocol hypotheses H1/H2)
| Channel | Target population | Note |
|---|---|---|
| expansion[s] | LC4 of the hemisphere covering s; LPLC1, LPLC4 (gain 0.5) | LC4 ∝ angular velocity (Ache et al. 2019) |
| angular_size[s] | LPLC2 of the hemisphere covering s | LPLC2 ∝ angular size (Ache et al. 2019) |
| motion_h / motion_v | reserved (0 gain in v0) | channels planned for evolution; not used in M0 |
| surface_proximity | reserved (0 gain in v0) | idem |
| background | all neurons (low rate) | spontaneous activity |

Coverage: the **left** hemisphere covers sectors {0,1,2,3}; the **right** covers {0,7,6,5}; sector 4 (back) receives the neurons of sectors 3 and 5 with gain 0.3. Each neuron is assigned to **one** sector of its hemisphere deterministically (bodyId order, cycling through the sectors), with sector 0 shared by both sides. This assignment is a hypothesis (H1) and is recorded in the manifest.
