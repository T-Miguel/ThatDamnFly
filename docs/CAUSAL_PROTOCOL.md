# Pre-registered causal protocol: That Damn Fly M0

Version 1.1 · September 13, 2026 · Frozen BEFORE running the confirmatory trial. (v1.0 → v1.1: after a 4–6 seed pilot with body v1/v1.1, the TA metric was replaced by "flee" because body v1.2 does not rotate on escape; 180° is now reported but excluded from the 4.1 criteria, because of the known limitation of rear coverage/front-back ambiguity; the pilot is attached to the report as a pilot, not as evidence.) Later changes only through a new dated version, with justification, and never after seeing results of the same configuration.

## 1. What has to be demonstrated

The complete chain: **materialized stimulus → neural activity (MaleCNS circuit) → motor output → effective change of the virtual body's escape**. Compare the intact circuit and the perturbed circuit with the **same stimuli, the same seeds and the same body controller**. A change of neural activity without a change of the body's trajectory does **not** meet the requirement.

## 2. Fixed configuration

| Element | Value |
|---|---|
| Model | `tdf-escape-v0.x` exported from MaleCNS v1.0 (see `research/out/circuit/manifest.json`) |
| Neural / world step | 1 ms / 10 ms (identical to the game) |
| Body | `docs/BODY_MODEL.md` v1.2: fixed parameters for all arms |
| Sensory encoding | `docs/SENSORY_MODEL.md` v1 |
| Arena | W = 1, H = 4/3; the fly starts at the center (0.5 W; 0.667 W), no nearby walls during the 0.6 s measured |
| Initial heading | drawn by seed (uniform 0–360°), the same in every arm of the same seed |
| Threat | disc of radius 0.09 W (pan size), starts 0.45 W from the fly's center, approaches in a straight line at constant speed and stops at contact distance (0.02 W + radius) |
| Directions | 8 azimuths relative to the fly's heading: 0°, 45°, …, 315° (0° = front; positive = left) |
| Intensities | speed 0.6 / 1.2 / 2.4 W·s⁻¹ (arrival in ≈750 / 375 / 188 ms) |
| Window | 300 ms of rest (background only) → stimulus onset at t₀ → 700 ms of observation |
| Seeds | 100 per condition (direction × intensity); seeds 1…100 |
| Arms | **A** intact · **B** VPN→DN ablation (silence every edge from LC4/LPLC2/LPLC1/LPLC4 to DNp01/02/03/04/06/11) · **C** output ablation (silence the motor readout of the DNs: motor channels at zero) · **D** shuffled-weights control (permutation of post-synaptic columns preserving out-degrees, fixed seed 999) · **E** no stimulus (background only) |

Total: 8 × 3 × 100 × 5 = 12,000 trials. Run in closed loop with the **real FlyCore runtime** (`flycore.dll` via ctypes, `scripts/run_causal.py`), not with the Python reference; the Python reference only serves for numeric parity (section 6).

## 3. Metrics (defined before the trials)

Per trial:

- **N_DN**: spikes of the escape output group {DNp01, DNp02, DNp04, DNp11} × {L,R} in [t₀, t₀+400 ms].
- **N_base**: spikes of the same group in [t₀−300 ms, t₀), scaled to 400 ms.
- **LI (lateralization index)**: (N_ipsi − N_contra)/(N_ipsi + N_contra), ipsi = the threat's side; defined only when N_ipsi + N_contra ≥ 3; not computed for 0° and 180°.
- **D_away (primary metric)**: displacement of the body's center between t₀ and t₀+600 ms projected onto the unit vector **û** pointing from the threat's initial position to the fly's initial position (positive = moved away). Unit: W. One visual body length ≈ 0.05 W.
- **flee**: 1 if D_away > 0.02 W (moved away at least ~½ body), 0 otherwise.
- **v_peak**: maximum body speed in [t₀, t₀+600 ms].

Per condition (direction × intensity × arm): mean, standard deviation, median and IQR over the 100 seeds; 95 % CI by bootstrap (1,000 resamples, seed 2026).

## 4. Pass criteria (pre-registered)

**4.1 Minimum response required in the intact circuit (arm A)**, all mandatory:
- (a) mean N_DN ≥ 3 × mean N_base at the medium and fast intensities, in every frontal/lateral direction (0°, ±45°, ±90°, ±135°).
- (b) mean LI ≥ 0.30 in the lateral directions (±45°, ±90°, ±135°), medium and fast intensities.
- (c) mean D_away ≥ 0.05 W (≈ 1 body) in the frontal/lateral directions, medium and fast intensities.
- (d) flee ≥ 0.65 in the frontal/lateral directions, medium and fast intensities.
- 180° is reported but does not enter 4.1 (pre-declared limitation: rear sector with gain 0.3 and front/back ambiguity in the DN readout).

**4.2 Reduction by ablation (arm B vs A)**, primary metric D_away:
- Reduction R_c = 1 − mean(D_away_B,c) / mean(D_away_A,c) per condition c (only conditions where A meets 4.1c).
- Passes if: **median R_c ≥ 0.30** AND **≥ 75 % of the eligible conditions have R_c ≥ 0.30 with the lower bound of the 95 % CI > 0**.
- Also report R for N_DN and for flee (secondary) and the absolute values of A and B.

**4.3 Control (arm D)**: must not meet 4.1(b) nor 4.1(c). If it does, the response is not attributable to the real connectivity and the model is rejected.

**4.4 Output ablation (arm C)**: mean D_away ≈ 0 ± wandering noise (|D_away| ≤ 0.02 W). Confirms the body does not escape without the neural signal.

**4.5 Arm E**, no stimulus: D_away compatible with chance (mean within ±0.02 W).

Failing any criterion = **does not pass**. In that case the report is delivered with the values, candidate hypotheses (encoding, weight scale, selection) and the estimated cost of each revision. Parameters are not optimized after seeing results without recording a new protocol version and reporting both rounds.

## 5. Simulator hypotheses (to document on the science screen and in the manifest)

H1 Retinotopy by sector assigned to each LC4/LPLC2/LPLC1/LPLC4 (the MaleCNS flat files carry no receptive fields). H2 LC4 encodes angular velocity (expansion), LPLC2 encodes angular size (Ache et al. 2019; mapping hypothesis). H3 Synaptic sign by predicted neurotransmitter (ACh +; GABA/Glu −). H4 LIF parameters and weight scale (Shiu & Spiller, MIT; our choices). H5 Motor mapping (body v1.2): escape vector in the body frame e_b = (−0.5·E, B) with E = sum and B = difference (R−L) of the escape DNs' activity; the body does not read the threat's position. H6 Background locomotion: seeded stochastic pattern, independent of threats, identical across arms (documented; it is not "escape").

## 6. Python ↔ C++ parity (before the trials)

With the same model, stimuli and seeds: 20 trials; relative error of the mean rate of the active populations < 5 %; deviation of the median latency of the first DN spike < 10 ms; no lateralization inversion. float32/float64 differences are expected; the order of operations is the same.

## 7. Mandatory outputs

`research/out/causal/`: `trials.parquet` (all trials), `summary.csv` (per condition and arm), `report.md` (observed vs inferred, limitations, decision), figures. All reproducible with `uv run scripts/run_causal.py --protocol v1.1 --seeds 100 --arms A,B,C,D,E`.
