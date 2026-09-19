# Neural package format `.tdfm` (format_version 1)

Little-endian. All indices are 32-bit and validated against the counts before any allocation.

## Header (144 bytes)
| Offset | Type | Field |
|---|---|---|
| 0 | char[8] | magic `TDFMODEL` |
| 8 | u32 | format_version = 1 |
| 12 | u32 | header_size = 144 |
| 16 | u32 | neuron_count N |
| 20 | u32 | edge_count E |
| 24 | u32 | input_entry_count M_in |
| 28 | u32 | output_entry_count M_out |
| 32 | u32 | max_delay_steps D (1..64) |
| 36 | u32 | flags (reserved, 0) |
| 40 | u64 | payload_len |
| 48 | u8[32] | SHA-256 of the payload |
| 80 | char[64] | model_id (UTF-8, NUL) |

## Payload (contiguous sections, in this order)
1. **Per-neuron parameters** - N × 8 × f32: `v_rest, v_thresh, v_reset, tau_m_ms, t_ref_ms, tau_syn_ms, bias_mv, reserved`.
2. **Edges (CSR by pre-synaptic neuron)** - `row_ptr[N+1]` u32; `post[E]` u32; `weight_mv[E]` f32 (sign already applied: + excitatory, − inhibitory); `delay[E]` u16 (1 ms steps, 1..D).
3. **Inputs** - `in_ptr[41+1]` u32 (channel = kind×8 + sector; kind 0 expansion, 1 angular_size, 2 motion_h, 3 motion_v, 4 surface_proximity, 5 background with sector 0); `in_neuron[M_in]` u32; `in_rate_per_ms[M_in]` f32 (Poisson rate per unit of stimulus); `in_weight_mv[M_in]` f32.
4. **Outputs** - `out_ptr[8+1]` u32; `out_neuron[M_out]` u32; `out_gain[M_out]` f32; `out_tau_ms[8]` f32; `out_scale[8]` f32.
5. **Provenance** - u32 len + UTF-8 JSON (64-bit IDs as strings; informative, not interpreted by the runtime).

## Dynamics (simulator hypotheses, documented in the manifest)
LIF with current-based synapses and exponential decay (reference: the Shiu & Spiller model, MIT; the parameters are our choices):
`dv/dt = ((v_rest − v) + g + bias) / tau_m`, `dg/dt = −g / tau_syn`, spike at `v ≥ v_thresh` → `v = v_reset` during `t_ref`.
Inputs: a Bernoulli process per ms with probability `min(1, rate_per_ms × x)`; each event adds `in_weight_mv` to `g`.
Outputs: `a_k += (Σ spikes × out_gain × out_scale_k − a_k) × dt / out_tau_k`.
RNG: xoshiro256** with splitmix64 on the seed. No allocations per step.
