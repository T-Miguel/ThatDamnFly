"""ctypes binding to the real FlyCore (flycore.dll / libflycore.so). Used in the closed-loop trials."""
from __future__ import annotations
import ctypes as C
import os, sys
import numpy as np

SECTORS = 8; MOTOR = 8; ABI = 1

class SensoryFrame(C.Structure):
    _fields_ = [("struct_size", C.c_uint32), ("version", C.c_uint32),
                ("expansion", C.c_float * SECTORS), ("angular_size", C.c_float * SECTORS), ("motion_h", C.c_float * SECTORS),
                ("motion_v", C.c_float * SECTORS), ("surface_proximity", C.c_float * SECTORS), ("background_drive", C.c_float)]

class MotorFrame(C.Structure):
    _fields_ = [("struct_size", C.c_uint32), ("version", C.c_uint32), ("channel", C.c_float * MOTOR), ("spikes_this_step", C.c_uint32), ("flags", C.c_uint32)]

class ModelInfo(C.Structure):
    _fields_ = [("struct_size", C.c_uint32), ("format_version", C.c_uint32), ("neuron_count", C.c_uint32), ("edge_count", C.c_uint32),
                ("input_channel_count", C.c_uint32), ("output_channel_count", C.c_uint32), ("max_delay_steps", C.c_uint32), ("reserved", C.c_uint32),
                ("payload_sha256", C.c_uint8 * 32), ("model_id", C.c_char * 64)]

STATUS = {0: "OK", 1: "INVALID_ARG", 2: "BAD_MODEL", 3: "HASH_MISMATCH", 4: "LIMIT", 5: "NUMERIC", 6: "OOM", 7: "ABI"}

def _default_lib_path() -> str:
    here = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    cand = [os.path.join(here, "flycore", "build-win64", "flycore.dll"), os.path.join(here, "flycore", "build-linux", "flycore.so")]
    for c in cand:
        if os.path.exists(c): return c
    raise FileNotFoundError("flycore shared library not found; build flycore first")

class FlyCore:
    def __init__(self, lib_path: str | None = None):
        self.lib = C.CDLL(lib_path or _default_lib_path())
        L = self.lib
        L.fc_abi_version.restype = C.c_uint32
        L.fc_inspect.argtypes = [C.c_char_p, C.c_size_t, C.POINTER(ModelInfo)]; L.fc_inspect.restype = C.c_int
        L.fc_create.argtypes = [C.c_char_p, C.c_size_t, C.c_uint64, C.POINTER(C.c_void_p)]; L.fc_create.restype = C.c_int
        L.fc_step.argtypes = [C.c_void_p, C.POINTER(SensoryFrame), C.c_uint32, C.POINTER(MotorFrame)]; L.fc_step.restype = C.c_int
        L.fc_reset.argtypes = [C.c_void_p, C.c_uint64]; L.fc_reset.restype = C.c_int
        L.fc_set_edge_mask.argtypes = [C.c_void_p, C.c_char_p, C.c_size_t]; L.fc_set_edge_mask.restype = C.c_int
        L.fc_clear_edge_mask.argtypes = [C.c_void_p]; L.fc_clear_edge_mask.restype = C.c_int
        L.fc_read_spike_counts.argtypes = [C.c_void_p, C.POINTER(C.c_uint32), C.c_size_t]; L.fc_read_spike_counts.restype = C.c_int
        L.fc_clear_spike_counts.argtypes = [C.c_void_p]; L.fc_clear_spike_counts.restype = C.c_int
        L.fc_get_info.argtypes = [C.c_void_p, C.POINTER(ModelInfo)]; L.fc_get_info.restype = C.c_int
        L.fc_destroy.argtypes = [C.c_void_p]; L.fc_destroy.restype = None
        assert L.fc_abi_version() == ABI

    def inspect(self, model_bytes: bytes) -> ModelInfo:
        info = ModelInfo(); info.struct_size = C.sizeof(ModelInfo)
        st = self.lib.fc_inspect(model_bytes, len(model_bytes), C.byref(info))
        if st != 0: raise RuntimeError(f"fc_inspect: {STATUS.get(st, st)}")
        return info

class Instance:
    def __init__(self, core: FlyCore, model_bytes: bytes, seed: int):
        self.core = core; self.lib = core.lib; self._model = model_bytes
        h = C.c_void_p()
        st = self.lib.fc_create(model_bytes, len(model_bytes), C.c_uint64(seed), C.byref(h))
        if st != 0: raise RuntimeError(f"fc_create: {STATUS.get(st, st)}")
        self.h = h
        self.info = ModelInfo(); self.info.struct_size = C.sizeof(ModelInfo); self.lib.fc_get_info(self.h, C.byref(self.info))
        self.n = int(self.info.neuron_count); self.e = int(self.info.edge_count)
        self.inp = SensoryFrame(); self.inp.struct_size = C.sizeof(SensoryFrame); self.inp.version = ABI
        self.out = MotorFrame(); self.out.struct_size = C.sizeof(MotorFrame); self.out.version = ABI
        self._counts = (C.c_uint32 * self.n)()

    def set_frame(self, frame: np.ndarray):
        """frame: 41 floats (kind*8+sector; 40 = background)."""
        f = np.asarray(frame, dtype=np.float32)
        self.inp.expansion[:] = f[0:8].tolist(); self.inp.angular_size[:] = f[8:16].tolist(); self.inp.motion_h[:] = f[16:24].tolist()
        self.inp.motion_v[:] = f[24:32].tolist(); self.inp.surface_proximity[:] = f[32:40].tolist(); self.inp.background_drive = float(f[40])

    def step(self, delta_ms: int = 10) -> np.ndarray:
        st = self.lib.fc_step(self.h, C.byref(self.inp), delta_ms, C.byref(self.out))
        if st != 0: raise RuntimeError(f"fc_step: {STATUS.get(st, st)}")
        return np.frombuffer(bytes(self.out.channel), dtype=np.float32).copy()

    @property
    def spikes_last(self) -> int: return int(self.out.spikes_this_step)

    def reset(self, seed: int):
        st = self.lib.fc_reset(self.h, C.c_uint64(seed)); assert st == 0

    def set_edge_mask(self, mask: np.ndarray):
        m = np.ascontiguousarray(mask, dtype=np.uint8); assert len(m) == self.e
        st = self.lib.fc_set_edge_mask(self.h, m.tobytes(), len(m)); assert st == 0, STATUS.get(st, st)

    def clear_edge_mask(self): assert self.lib.fc_clear_edge_mask(self.h) == 0

    def spike_counts(self) -> np.ndarray:
        assert self.lib.fc_read_spike_counts(self.h, self._counts, self.n) == 0
        return np.frombuffer(bytes(self._counts), dtype=np.uint32).copy()

    def clear_spike_counts(self): assert self.lib.fc_clear_spike_counts(self.h) == 0

    def close(self):
        if self.h: self.lib.fc_destroy(self.h); self.h = None
    def __del__(self):
        try: self.close()
        except Exception: pass
