"""Python (numpy) reference simulator of the same numeric scheme as FlyCore - for parity.
Reads a .tdfm, reproduces the order of operations per ms and the same xoshiro256** draws of the inputs."""
from __future__ import annotations
import struct
import numpy as np
from .xoshiro import Xoshiro256

N_IN = 41; N_OUT = 8

class RefModel:
    def __init__(self, data: bytes):
        assert data[:8] == b"TDFMODEL"
        (self.fmt, hs, self.n, self.e, self.m_in, self.m_out, self.max_delay, _flags) = struct.unpack_from("<IIIIIIII", data, 8)
        (payload_len,) = struct.unpack_from("<Q", data, 40)
        p = data[144:]; off = 0
        def take(dtype, count):
            nonlocal off
            arr = np.frombuffer(p, dtype=dtype, count=count, offset=off).copy(); off += arr.nbytes; return arr
        n, e = self.n, self.e
        prm = take("<f4", n * 8).reshape(n, 8)
        self.v_rest, self.v_thresh, self.v_reset, self.tau_m, self.t_ref, self.tau_syn, self.bias = [prm[:, i].astype(np.float32) for i in range(7)]
        self.row_ptr = take("<u4", n + 1); self.post = take("<u4", e); self.weight = take("<f4", e); self.delay = take("<u2", e)
        self.in_ptr = take("<u4", N_IN + 1); self.in_neuron = take("<u4", self.m_in); self.in_rate = take("<f4", self.m_in); self.in_weight = take("<f4", self.m_in)
        self.out_ptr = take("<u4", N_OUT + 1); self.out_neuron = take("<u4", self.m_out); self.out_gain = take("<f4", self.m_out)
        self.out_tau = take("<f4", N_OUT); self.out_scale = take("<f4", N_OUT)
        self.decay_m = np.exp(-1.0 / self.tau_m).astype(np.float32); self.decay_syn = np.exp(-1.0 / self.tau_syn).astype(np.float32)
        self.ref_steps = np.rint(self.t_ref).astype(np.uint32)
        self.edge_mask = np.zeros(e, dtype=bool)

class RefInstance:
    def __init__(self, m: RefModel, seed: int):
        self.m = m; n = m.n
        self.v = m.v_rest.copy(); self.g = np.zeros(n, np.float32); self.ref_left = np.zeros(n, np.uint32)
        self.delay_buf = np.zeros((m.max_delay, n), np.float32); self.ring = 0
        self.out_act = np.zeros(N_OUT, np.float32); self.spike_counts = np.zeros(n, np.uint32)
        self.rng = Xoshiro256(seed)

    def step_ms(self, frame: np.ndarray) -> int:
        m = self.m; n = m.n
        # 1) inputs (same draw order as the C++: per channel, per entry)
        for c in range(N_IN):
            x = float(frame[c])
            if x <= 0.0: continue
            for k in range(int(m.in_ptr[c]), int(m.in_ptr[c + 1])):
                pr = min(1.0, float(m.in_rate[k]) * x)
                if self.rng.uniform() < pr: self.g[m.in_neuron[k]] += m.in_weight[k]
        # 2) delayed events
        self.g += self.delay_buf[self.ring]; self.delay_buf[self.ring] = 0.0
        # 3) membranes
        gi = self.g.copy()
        refr = self.ref_left > 0
        self.ref_left[refr] -= 1; self.v[refr] = m.v_reset[refr]
        act = ~refr
        target = m.v_rest + gi + m.bias
        self.v[act] = (target + (self.v - target) * m.decay_m)[act]
        spk = act & (self.v >= m.v_thresh)
        self.v[spk] = m.v_reset[spk]; self.ref_left[spk] = m.ref_steps[spk]
        self.g = (gi * m.decay_syn).astype(np.float32)
        spikes = np.nonzero(spk)[0]
        # 4) propagation
        for i in spikes:
            self.spike_counts[i] += 1
            a, b = int(m.row_ptr[i]), int(m.row_ptr[i + 1])
            for k in range(a, b):
                if m.edge_mask[k]: continue
                slot = (self.ring + int(m.delay[k])) % m.max_delay
                self.delay_buf[slot, m.post[k]] += m.weight[k]
        # outputs
        spiked = np.zeros(n, bool); spiked[spikes] = True
        for c in range(N_OUT):
            acc = 0.0
            for k in range(int(m.out_ptr[c]), int(m.out_ptr[c + 1])):
                if spiked[m.out_neuron[k]]: acc += float(m.out_gain[k])
            tau = float(m.out_tau[c]) if m.out_tau[c] > 0 else 1.0
            self.out_act[c] += np.float32((acc * float(m.out_scale[c]) - float(self.out_act[c])) * (1.0 / tau))
        self.ring = (self.ring + 1) % m.max_delay
        return len(spikes)

    def step(self, frame: np.ndarray, delta_ms: int = 10):
        total = 0
        for _ in range(delta_ms): total += self.step_ms(frame)
        return self.out_act.copy(), total
