"""Writing/reading of the .tdfm package (format v1, see flycore/docs_FORMAT.md)."""
from __future__ import annotations
import hashlib, json, struct
import numpy as np

MAGIC = b"TDFMODEL"; FORMAT_VERSION = 1; HEADER_SIZE = 144; N_IN_CHANNELS = 41; N_OUT_CHANNELS = 8
KIND = {"expansion": 0, "angular_size": 1, "motion_h": 2, "motion_v": 3, "surface_proximity": 4, "background": 5}

def channel_index(kind: str, sector: int = 0) -> int:
    k = KIND[kind]
    return 40 if k == 5 else k * 8 + sector

def write_tdfm(path: str, *, model_id: str, params: np.ndarray, pre: np.ndarray, post: np.ndarray, weight_mv: np.ndarray, delay: np.ndarray,
               in_channel: np.ndarray, in_neuron: np.ndarray, in_rate: np.ndarray, in_weight: np.ndarray,
               out_channel: np.ndarray, out_neuron: np.ndarray, out_gain: np.ndarray, out_tau: list[float], out_scale: list[float],
               max_delay: int, provenance: dict) -> dict:
    n = params.shape[0]; assert params.shape == (n, 8)
    e = len(pre)
    order = np.argsort(pre, kind="stable"); pre = pre[order]; post = post[order]; weight_mv = weight_mv[order]; delay = delay[order]
    row_ptr = np.zeros(n + 1, dtype=np.uint32); np.add.at(row_ptr, pre + 1, 1); row_ptr = np.cumsum(row_ptr).astype(np.uint32)
    oi = np.argsort(in_channel, kind="stable"); in_channel = in_channel[oi]; in_neuron = in_neuron[oi]; in_rate = in_rate[oi]; in_weight = in_weight[oi]
    in_ptr = np.zeros(N_IN_CHANNELS + 1, dtype=np.uint32); np.add.at(in_ptr, in_channel + 1, 1); in_ptr = np.cumsum(in_ptr).astype(np.uint32)
    oo = np.argsort(out_channel, kind="stable"); out_channel = out_channel[oo]; out_neuron = out_neuron[oo]; out_gain = out_gain[oo]
    out_ptr = np.zeros(N_OUT_CHANNELS + 1, dtype=np.uint32); np.add.at(out_ptr, out_channel + 1, 1); out_ptr = np.cumsum(out_ptr).astype(np.uint32)
    prov = json.dumps(provenance, ensure_ascii=False, separators=(",", ":")).encode("utf-8")
    payload = b"".join([
        params.astype("<f4").tobytes(),
        row_ptr.astype("<u4").tobytes(), post.astype("<u4").tobytes(), weight_mv.astype("<f4").tobytes(), delay.astype("<u2").tobytes(),
        in_ptr.astype("<u4").tobytes(), in_neuron.astype("<u4").tobytes(), in_rate.astype("<f4").tobytes(), in_weight.astype("<f4").tobytes(),
        out_ptr.astype("<u4").tobytes(), out_neuron.astype("<u4").tobytes(), out_gain.astype("<f4").tobytes(),
        np.asarray(out_tau, dtype="<f4").tobytes(), np.asarray(out_scale, dtype="<f4").tobytes(),
        struct.pack("<I", len(prov)), prov,
    ])
    sha = hashlib.sha256(payload).digest()
    mid = model_id.encode("utf-8")[:63].ljust(64, b"\0")
    header = MAGIC + struct.pack("<IIIIIIII", FORMAT_VERSION, HEADER_SIZE, n, e, len(in_neuron), len(out_neuron), max_delay, 0) + struct.pack("<Q", len(payload)) + sha + mid
    assert len(header) == HEADER_SIZE
    data = header + payload
    with open(path, "wb") as f: f.write(data)
    return {"bytes": len(data), "payload_sha256": sha.hex(), "file_sha256": hashlib.sha256(data).hexdigest(), "neurons": n, "edges": e,
            "csr": {"row_ptr": row_ptr, "post": post, "weight": weight_mv, "delay": delay}}
