# Body model v1.6: from MotorFrame to movement

**History:** v1 mapped the escape as a *forward* impulse + rotation ∝ B. A pilot trial (4 seeds × 3 directions, before the pre-registered protocol) showed that, with a frontal threat, the forward impulse drives the fly *into* the threat: a design defect of the body, not a property of the circuit. v1.1 replaced the impulse with an **escape vector in the body frame** (backwards and towards the side opposite the more active hemisphere), consistent with the literature on take-off directed away from the stimulus (Card & Dickinson 2008). It still does not read the threat's position. **v1.2** (same pilot, 6 seeds × 8 directions): the rotation ∝ B combined with the backing made the backing point at the threat after half a turn → escape rotation removed (k_turn = 0; the visual orientation follows the velocity) and moderate backing (c_back = 0.5) so that the lateral component dominates. Frozen before the confirmatory trial; the pilot is not confirmatory evidence.

The body controller converts the neural output into velocity and rotation. It **does not read** the position of attacks, aim, finger or fury. Parameters are fixed per rules version (`rulesVersion`). Same implementation in Python (trials) and C# (game), with parity fixtures.

## State
Position **p** (W), heading φ (rad), scalar speed v (W·s⁻¹), angular velocity ω (rad·s⁻¹). Tick Δt = 0.01 s.

## Inputs per tick (fc_motor_frame)
`a_escL, a_escR` (channels 0,1), `a_turnL, a_turnR` (2,3), `a_fwdL, a_fwdR` (4,5): filtered activities, ≥ 0.

**v1.3 (September 14, 2026, ADR-001):** agitation levels per fly (`Level`), short darts, entry from the edge, livelier escape gains (k_esc 2.0; a_max 16; v_max 1.3). The causal protocol was repeated with v1.3 (level 0).

**v1.4 (September 14, 2026, ADR-003):** pauses become **landings** (0.5–1.6 s at level 0) and the **neural escape interrupts the landing** (take-off when E > 0.25; 1 s refractory period without landing; no landing while E > 0.25). Factors per fly kind (speed, darts, landings) and body radius per kind (0.020 / 0.028 / 0.015). All durations (entry, dart, landing, refractory) are counted in integer ticks for exact C#/Python parity (`app/Tests.Domain/BodyParityTests.cs`). The causal protocol was repeated with v1.4 (level 0).

**v1.5 (September 14, 2026, ADR-007):** **food attraction**: each scene declares food spots (position, radius). Within 0.35 W of the nearest spot, the desired heading gains the term `ω_bias = k_attr · sin(θ_food − φ) · (1 − d/0.35)`, k_attr = 3 rad·s⁻¹; within radius + 0.04 W of the food the landing probability is ×6 and the duration ×1.5. **Short landings** (horsefly kind): 0.2–0.4 s followed by a dart with a random turn ("fakes a landing"). None of these terms reads threats, attacks or the player: they are functions of the scene and the seed. The causal protocol runs without food spots (default): the wandering stays identical across arms A–E. C#/Python parity verified with food and horsefly (168 points).

**v1.6 (September 17, 2026, ADR-015):** **bait**: the simulation may set on each fly a temporary point `Bait` (position, radius; active 5 s). Within 0.6 W of the bait, it *replaces* the scene's food in the attraction term, with `k_bait = 4.5 rad·s⁻¹` and `w = 1 − d/0.6`; next to it (radius + 0.04 W) the same landing factors as food apply (×6 probability, ×1.5 duration). Outside that range, the scene's food still applies. It still does not read threats: the bait is a function of the scene and the player, not of the attack. Without bait (default), v1.6 ≡ v1.5: the causal protocol does not change. C#/Python parity: `research/scripts/body_fixture.py` (280 points: house fly, fruit fly, horsefly, boss and a bait case).

## Wandering (H6: documented hypothesis, independent of threats)
Stochastic process with the round's seed (own xoshiro256**, separate from the neural one):
- `ω_w` = Ornstein–Uhlenbeck: ω_w += (−ω_w · Δt/τ_w) + σ_w·√Δt·N(0,1), τ_w = 0.25 s, σ_w = 4 rad·s⁻¹.
- Base speed `v_w = min(0.6; 0.22 + 0.08·Level) · SpeedFactor` (ramp tuned on Sept 14 after the owner reached 7 flies/round; SpeedFactor 1 / 0.8 / 1.25 per kind).
- Landings (v1.4): probability per tick `max(0.0008; 0.003 − 0.0003·Level) · LandFactor`, duration 0.5 s to `max(0.5; 1.6 − 0.15·Level)`; only when E ≤ 0.25 and outside the 1 s refractory period after take-off. During a landing v_w = 0; the escape stays active (take-off).
- Darts: probability per tick `(0.005 + 0.0025·Level) · DartFactor`; duration 0.25 s at `(0.75 + 0.1·Level) · SpeedFactor` W·s⁻¹ with a random 60–180° turn.
- Entry: the fly is born at a random point of the edge facing inwards, with an initial dart of 0.6 W·s⁻¹ for 0.4 s.
- At the edges, besides the tangential projection, it turns ±90° (random, body seed).
The wandering is identical across protocol arms A–E because it depends only on the seed.

## Escape (from the circuit)
- E = a_escL + a_escR; B = a_escR − a_escL (positive = more activity on the right).
- Escape vector in the body frame (x̂ = forward, ŷ = left): **e_b = (−c_back · E, B)**, c_back = 0.5. Interpretation H5: symmetric activation (frontal threat) → backing; more active left hemisphere (B < 0) → displacement to the right. The model only resolves left/right; front/back depends only on the visual coverage (weak rear sector): a recorded limitation.
- Desired velocity (world frame): `v_des = v_w · (cos φ, sin φ) + k_esc · R(φ) · e_b`, limited to |v_des| ≤ v_max. k_esc = 2.0 W·s⁻¹ per unit; v_max = 1.3 W·s⁻¹.
- Heading: `ω_des = ω_w` (k_turn = 0 in v1.2). In the presentation, the sprite faces the velocity when |v| > 0.3 W·s⁻¹.

## Integration
- ω → ω_des with time constant τ_ω = 0.03 s; **v** (vector) → **v_des** with τ_v = 0.04 s and limited acceleration |Δ**v**| ≤ 16 W·s⁻² · Δt.
- φ += ω·Δt; p += **v**·Δt.
- Edges: keep the body radius (0.02 W for the house fly) inside the arena; on contact, project the velocity tangentially and reflect φ smoothly (no teleporting).

## What is excluded by design
No route is chosen as a function of the player's target. If the circuit produces no lateralization, the body does **not** turn "away"; that is exactly what the protocol measures.
