# Calibration p1 (before the causal protocol): September 13, 2026

Method: **frontal** open-loop stimulus (sector 0, expansion=1.0, angular_size=0.5, 300 ms), stationary fly, 3 seeds; `scripts/calibrate.py`. Only background/evoked rates were used to choose values. Lateral directions, lateralization, ablation and the body were **not** used: those metrics stay untouched for the protocol.

Calibration targets fixed a priori: background VPN ≤ 2 Hz; background DN ≤ 3 Hz; evoked DN ≥ 50 Hz (GF/DNp04) under the strong frontal stimulus; interneurons active but without runaway.

| Iteration | Change | Result (mean per type, L side) |
|---|---|---|
| 0 | w_syn 0.275 × w_scale 0.1; vpn_weight 10 mV; background 4 mV @ 4 Hz; bias 0 | everything ≈ 0 Hz; LC4 4 Hz; DN 0 → inputs too weak |
| 1 | vpn_weight 16/22/28 | DNp01 10/37/53 Hz; DNp04 39/66/89 Hz; background 0 |
| 2 | vpn_weight 24; background 10 mV @ 20 Hz; bias 5; w_scale 0.1/0.2/0.4 | background DN 43–156 Hz → rejected (excessive spontaneous activity) |
| 3 | background 6 mV @ 20 Hz; bias 0/1/2/3 | bias 3: background VPN 0.0 Hz, DN 0.1 Hz; evoked GF 86 Hz, DNp04 111 Hz, DNp02/11 37–40 Hz; inter 3.6 Hz |

Choice **p1**: `w_scale 0.1; w_cap 30 mV; delay 2 ms; vpn_rate 0.12/ms; vpn_weight 24 mV; background 0.02/ms × 6 mV; bias 3 mV`.

Recorded observation (not corrected): an L/R asymmetry in the data - LC4 71 L vs 55 R; LC4→DNp04 6,811 vs 4,786 synapses - produces esc_L > esc_R (0.70 vs 0.55) even with a frontal stimulus. Left for the protocol to evaluate; any compensation will be a new, documented parameter version.
