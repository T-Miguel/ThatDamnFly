"""Generates the M0 sounds procedurally (own work; no third-party samples). Output: app/Assets/Resources/audio/*.wav (16-bit, 44.1 kHz, mono).
Usage: research/.venv/Scripts/python.exe tools/audio/gen_audio.py"""
import os, wave, struct
import numpy as np

SR = 44100
root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
out = os.path.join(root, "app", "Assets", "Resources", "audio"); os.makedirs(out, exist_ok=True)
rng = np.random.default_rng(7)

def write(name, x, peak=0.9):
    x = np.asarray(x, dtype=np.float64); m = np.max(np.abs(x)) or 1.0; x = x / m * peak
    with wave.open(os.path.join(out, name), "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes((x * 32767).astype("<i2").tobytes())
    print(name, f"{len(x)/SR:.2f}s")

def t(sec): return np.arange(int(SR * sec)) / SR
def env(n, a=0.005, d=0.1, s=0.0, r=0.05):
    e = np.ones(n); na, nd, nr = int(a * SR), int(d * SR), int(r * SR)
    e[:na] = np.linspace(0, 1, na); e[na:na + nd] = np.linspace(1, s, nd) if nd else 1
    if nd and na + nd < n: e[na + nd:] = s
    e[-nr:] *= np.linspace(1, 0, nr); return e
def lowpass(x, cutoff):
    # simple 1st-order IIR filter
    a = np.exp(-2 * np.pi * cutoff / SR); y = np.zeros_like(x); acc = 0.0
    for i, v in enumerate(x): acc = a * acc + (1 - a) * v; y[i] = acc
    return y

# buzz: wing beat ~190 Hz with harmonics, slow AM, 1 s loop without a click (integer phase)
tt = t(1.0); f0 = 190.0
buzz = sum((0.6 / k) * np.sin(2 * np.pi * f0 * k * tt + 0.3 * k) for k in range(1, 8))
buzz *= 1.0 + 0.18 * np.sin(2 * np.pi * 7 * tt) + 0.08 * np.sin(2 * np.pi * 23 * tt)
buzz += 0.05 * lowpass(rng.standard_normal(len(tt)), 1200)
write("buzz_loop.wav", buzz, 0.5)

# cloth: "whap": filtered noise burst, fast decay
n = int(0.12 * SR); x = lowpass(rng.standard_normal(n), 900) * env(n, 0.002, 0.08, 0.0, 0.03)
write("impact_cloth.wav", x, 0.8)

# pan: metallic "clang": inharmonic partials + transient
n = int(0.6 * SR); tt = t(0.6)
parts = [(820, 0.35), (1260, 0.28), (1930, 0.22), (2710, 0.15), (3400, 0.1)]
x = sum(a * np.sin(2 * np.pi * f * tt) * np.exp(-tt * (3.5 + f / 900)) for f, a in parts)
x += lowpass(rng.standard_normal(n), 4000) * env(n, 0.001, 0.02, 0.0, 0.01) * 0.6
write("impact_pan.wav", x, 0.9)

# fridge: low boom with a pitch drop + rattle
n = int(0.8 * SR); tt = t(0.8)
f = 70 * np.exp(-tt * 3) + 38
boom = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 4)
rattle = lowpass(rng.standard_normal(n), 2500) * (np.exp(-tt * 6) * (0.5 + 0.5 * np.sin(2 * np.pi * 27 * tt)))
write("impact_fridge.wav", boom * 1.0 + rattle * 0.35, 0.95)

# UI: short tap
n = int(0.05 * SR); tt = t(0.05)
x = np.sin(2 * np.pi * 1100 * tt) * env(n, 0.001, 0.03, 0.0, 0.01) + lowpass(rng.standard_normal(n), 3000) * env(n, 0.0005, 0.005, 0, 0.004) * 0.4
write("ui_tap.wav", x, 0.5)

# fury threshold: two short rising notes
def note(freq, dur, shape=0.4):
    n = int(dur * SR); tt = t(dur); return (np.sin(2 * np.pi * freq * tt) + 0.3 * np.sin(2 * np.pi * freq * 2 * tt)) * env(n, 0.004, dur * shape, 0.3, 0.03)
write("fury_up.wav", np.concatenate([note(440, 0.09), note(660, 0.14)]), 0.6)

# win: three rising notes + a brief silence
win = np.concatenate([note(523, 0.11), note(659, 0.11), note(784, 0.28, 0.6), np.zeros(int(0.15 * SR))])
write("win.wav", win, 0.7)

# time out: two soft falling notes
write("timeout.wav", np.concatenate([note(392, 0.18, 0.6), note(294, 0.35, 0.7)]), 0.55)

# resume countdown: soft tick
n = int(0.06 * SR); tt = t(0.06); write("tick.wav", np.sin(2 * np.pi * 880 * tt) * env(n, 0.002, 0.04, 0, 0.01), 0.4)

# tool approach: short "whoosh" (low-pass noise with an envelope)
n = int(0.25 * SR); x = lowpass(rng.standard_normal(n), 600) * env(n, 0.05, 0.12, 0.2, 0.06); write("whoosh.wav", x, 0.5)

# catch: "splat": short thud + squish (low-pass noise with a pitch drop) + a small pause
n = int(0.35 * SR); tt = t(0.35)
f = 160 * np.exp(-tt * 12) + 50
thud = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 14)
squish = lowpass(rng.standard_normal(n), 1800) * np.exp(-tt * 9) * (0.6 + 0.4 * np.sin(2 * np.pi * 40 * tt))
write("catch.wav", thud * 1.0 + squish * 0.5, 0.9)

# new fly: short rising buzz
n = int(0.3 * SR); tt = t(0.3)
f = 150 + 120 * tt / 0.3
write("fly_in.wav", np.sin(2 * np.pi * np.cumsum(f) / SR) * env(n, 0.01, 0.1, 0.5, 0.1) * (1 + 0.3 * np.sin(2 * np.pi * 25 * tt)), 0.45)

# ---- ADR-003: arcade batch ----
# combo: two bright rising "dings" (the presentation raises the pitch per combo level)
combo = np.concatenate([note(784, 0.08, 0.5), note(1175, 0.16, 0.6)])
write("combo.wav", combo, 0.6)

# extra time: clean "ding" with a tail
n = int(0.35 * SR); tt = t(0.35)
write("bonus.wav", (np.sin(2 * np.pi * 1319 * tt) + 0.4 * np.sin(2 * np.pi * 2637 * tt)) * np.exp(-tt * 9) * env(n, 0.002, 0.1, 0.5, 0.05), 0.5)

# escaped: doppler buzz: pitch rises and falls, with wing AM
n = int(0.5 * SR); tt = t(0.5)
f = 230 + 220 * np.sin(np.pi * tt / 0.5) ** 2 * (1 - tt / 0.5)
x = sum((0.6 / k) * np.sin(2 * np.pi * k * np.cumsum(f) / SR) for k in range(1, 6)) * (1 + 0.25 * np.sin(2 * np.pi * 30 * tt))
write("escape.wav", x * env(n, 0.01, 0.15, 0.6, 0.15), 0.55)

# take-off: short "brrt": wings starting (pitch rising fast)
n = int(0.16 * SR); tt = t(0.16)
f = 120 + 260 * tt / 0.16
write("takeoff.wav", np.sin(2 * np.pi * np.cumsum(f) / SR) * (1 + 0.5 * np.sin(2 * np.pi * 45 * tt)) * env(n, 0.005, 0.06, 0.5, 0.05), 0.5)

# music: 4-bar loop at 112 BPM, "sneaky" minor key: plucked bass, soft square-wave melody, shaker and thump
BPM = 112.0; beat = 60.0 / BPM; bars = 4; total = bars * 4 * beat; N = int(total * SR)
music = np.zeros(N)
def add(x, t0):
    i = int(t0 * SR); j = min(N, i + len(x)); music[i:j] += x[: j - i]
def midi(m): return 440.0 * 2 ** ((m - 69) / 12)
def pluck(freq, dur, bright=0.5):
    n = int(dur * SR); tt = t(dur)
    x = np.sin(2 * np.pi * freq * tt) + bright * 0.5 * np.sin(2 * np.pi * 2 * freq * tt) + bright * 0.25 * np.sin(2 * np.pi * 3 * freq * tt)
    return x * np.exp(-tt * 6) * env(n, 0.003, 0.05, 0.6, 0.03)
def lead(freq, dur):
    n = int(dur * SR); tt = t(dur); vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * tt)
    ph = 2 * np.pi * np.cumsum(freq * vib) / SR
    x = np.sign(np.sin(ph)) * 0.35 + 0.65 * np.sin(ph)     # softened square
    return lowpass(x, 2200) * env(n, 0.01, 0.08, 0.7, 0.05)
def kick(t0):
    n = int(0.18 * SR); tt = t(0.18); f = 110 * np.exp(-tt * 25) + 45
    add(np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 18) * 0.9, t0)
def shaker(t0, acc=1.0):
    n = int(0.06 * SR); add(lowpass(rng.standard_normal(n), 6000) * env(n, 0.002, 0.03, 0.0, 0.02) * 0.18 * acc, t0)
def block(t0):
    n = int(0.05 * SR); tt = t(0.05); add(np.sin(2 * np.pi * 1600 * tt) * np.exp(-tt * 90) * 0.35, t0)
# harmony (Am – F – Dm – E): bass in eighth notes with swing
bass_roots = [45, 41, 38, 40]
for b in range(bars):
    r = bass_roots[b]
    pattern = [r, r + 7, r + 12, r + 7, r, r + 3, r + 7, r + 10] if b < 3 else [r, r + 7, r + 12, r + 11, r + 7, r + 4, r, r - 5]
    for k, m in enumerate(pattern):
        sw = 0.06 * beat if k % 2 else 0.0
        add(pluck(midi(m), 0.45 * beat, 0.6) * 0.55, (b * 4 + k * 0.5) * beat + sw)
    kick(b * 4 * beat); kick((b * 4 + 2) * beat); kick((b * 4 + 2.75) * beat) if b % 2 else None
    for k in range(8): shaker((b * 4 + k * 0.5) * beat + (0.06 * beat if k % 2 else 0), 1.0 if k % 2 == 0 else 0.6)
    block((b * 4 + 1) * beat); block((b * 4 + 3) * beat)
# sneaky melody (quarter/eighth notes), a motif that climbs and "peeks"
mel = [
    (69, 1.0), (72, 0.5), (76, 0.5), (75, 1.0), (72, 1.0),
    (69, 0.5), (72, 0.5), (77, 1.0), (76, 0.5), (74, 0.5), (72, 1.0),
    (74, 0.5), (77, 0.5), (81, 1.0), (79, 0.5), (77, 0.5), (74, 1.0),
    (76, 0.5), (75, 0.5), (76, 1.0), (79, 0.5), (76, 0.5), (0, 1.0),
]
tm = 0.0
for m, d in mel:
    if m: add(lead(midi(m), d * beat * 0.92) * 0.32, tm)
    tm += d * beat
write("music_loop.wav", music, 0.7)

# kitchen ambience (4 s loop): fridge hum + clock every second
n = int(4.0 * SR); tt = t(4.0)
hum = sum((0.5 / k) * np.sin(2 * np.pi * 50 * k * tt + 0.7 * k) for k in range(1, 5)) * (1 + 0.05 * np.sin(2 * np.pi * 0.5 * tt))
amb = hum * 0.25 + lowpass(rng.standard_normal(n), 400) * 0.06
for s in range(4):
    i = int(s * SR); m = int(0.02 * SR); tk = t(0.02)
    amb[i:i + m] += np.sin(2 * np.pi * 2400 * tk) * np.exp(-tk * 400) * (0.5 if s % 2 == 0 else 0.35)
write("ambience_loop.wav", amb, 0.5)

# ---- picnic (second scene) ----
# napkin: light "fwap": low-pass noise, higher and shorter than the cloth
n = int(0.10 * SR); x = lowpass(rng.standard_normal(n), 1600) * env(n, 0.002, 0.06, 0.0, 0.03)
write("impact_napkin.wav", x, 0.7)

# frisbee: plastic "thock": transient + short muffled partial
n = int(0.35 * SR); tt = t(0.35)
x = (np.sin(2 * np.pi * 420 * tt) * np.exp(-tt * 22) + 0.5 * np.sin(2 * np.pi * 1180 * tt) * np.exp(-tt * 40)) + lowpass(rng.standard_normal(n), 2500) * env(n, 0.001, 0.03, 0.0, 0.01) * 0.7
write("impact_frisbee.wav", x, 0.85)

# basket: wicker thump: medium thud + wicker crackle
n = int(0.7 * SR); tt = t(0.7)
f = 95 * np.exp(-tt * 4) + 48
thud = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 5)
creak = lowpass(rng.standard_normal(n), 3500) * (np.exp(-tt * 7) * (0.5 + 0.5 * np.sin(2 * np.pi * 41 * tt)))
write("impact_basket.wav", thud * 1.0 + creak * 0.4, 0.95)

# picnic ambience (6 s loop): breeze (undulating filtered noise) + birds (short FM chirps)
n = int(6.0 * SR); tt = t(6.0)
breeze = lowpass(rng.standard_normal(n), 500) * (0.5 + 0.5 * np.sin(2 * np.pi * 0.17 * tt + 1.0)) * 0.35
amb = breeze.copy()
def chirp(t0, f0, f1, dur, vib=40.0):
    m = int(dur * SR); tk = t(dur); f = f0 + (f1 - f0) * tk / dur + 60 * np.sin(2 * np.pi * vib * tk)
    i = int(t0 * SR); j = min(n, i + m); amb[i:j] += (np.sin(2 * np.pi * np.cumsum(f) / SR) * env(m, 0.005, dur * 0.4, 0.5, 0.03) * 0.22)[: j - i]
for t0, f0, f1, d in ((0.4, 2600, 3400, 0.12), (0.58, 3200, 2800, 0.1), (0.74, 2600, 3600, 0.14), (2.3, 3900, 3300, 0.09), (2.46, 3300, 3900, 0.09), (4.1, 2400, 2900, 0.16), (4.32, 2900, 2500, 0.12), (5.2, 3500, 4200, 0.08), (5.34, 4200, 3400, 0.08)):
    chirp(t0, f0, f1, d)
write("ambience_picnic.wav", amb, 0.5)

# ---- picnic music (ADR-006): same motif in a major mode, brighter (C major; bass in fifths; more present shaker) ----
music = np.zeros(N)
bass_roots_major = [48, 45, 41, 43]   # C – Am – F – G
for b in range(bars):
    r = bass_roots_major[b]
    pattern = [r, r + 7, r + 12, r + 7, r + 4, r + 7, r + 12, r + 9]
    for k, m in enumerate(pattern):
        sw = 0.06 * beat if k % 2 else 0.0
        add(pluck(midi(m), 0.45 * beat, 0.7) * 0.5, (b * 4 + k * 0.5) * beat + sw)
    kick(b * 4 * beat); kick((b * 4 + 2) * beat)
    for k in range(8): shaker((b * 4 + k * 0.5) * beat + (0.06 * beat if k % 2 else 0), 1.0 if k % 2 == 0 else 0.8)
    block((b * 4 + 1.5) * beat); block((b * 4 + 3) * beat)
mel_major = [
    (72, 1.0), (76, 0.5), (79, 0.5), (77, 1.0), (76, 1.0),
    (72, 0.5), (76, 0.5), (81, 1.0), (79, 0.5), (77, 0.5), (76, 1.0),
    (77, 0.5), (81, 0.5), (84, 1.0), (83, 0.5), (81, 0.5), (79, 1.0),
    (76, 0.5), (77, 0.5), (79, 1.0), (84, 0.5), (79, 0.5), (0, 1.0),
]
tm = 0.0
for m, d in mel_major:
    if m: add(lead(midi(m), d * beat * 0.9) * 0.3, tm)
    tm += d * beat
write("music_picnic.wav", music, 0.7)

# ---- extra scenes (ADR-011): parametrized impact families and ambiences ----
def flap(name, cutoff, dur, peak=0.75):
    n = int(dur * SR); write(name, lowpass(rng.standard_normal(n), cutoff) * env(n, 0.002, dur * 0.6, 0.0, dur * 0.25), peak)
def thud(name, f0, decay, noise_cut, noise_amt, dur=0.4, extra=None, peak=0.9):
    n = int(dur * SR); tt = t(dur); f = f0 * np.exp(-tt * 10) + f0 * 0.35
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * decay) + lowpass(rng.standard_normal(n), noise_cut) * np.exp(-tt * decay * 1.5) * noise_amt
    if extra is not None: x = x + extra(tt, n)
    write(name, x, peak)
def clang(name, parts, dur=0.6, noise=0.5, peak=0.9):
    n = int(dur * SR); tt = t(dur)
    x = sum(a * np.sin(2 * np.pi * f * tt) * np.exp(-tt * (3.5 + f / 900)) for f, a in parts) + lowpass(rng.standard_normal(n), 4000) * env(n, 0.001, 0.02, 0.0, 0.01) * noise
    write(name, x, peak)
# fast (flap): roll, newspaper, flip-flop, menu, fan
flap("impact_paper.wav", 1400, 0.11); flap("impact_newspaper.wav", 700, 0.13, 0.8); flap("impact_flipflop.wav", 1100, 0.10, 0.85); flap("impact_menu.wav", 1800, 0.09); flap("impact_fan.wav", 900, 0.16, 0.6)
# medium: plunger (rubber thwop), cushion (poof), ball (boing), tray (clang), grill (rattling metal)
thud("impact_plunger.wav", 140, 12, 900, 0.6, 0.35, extra=lambda tt, n: 0.4 * np.sin(2 * np.pi * (300 - 200 * tt) * tt) * np.exp(-tt * 20))
thud("impact_cushion.wav", 90, 9, 500, 0.9, 0.4, peak=0.7)
thud("impact_ball.wav", 220, 6, 1200, 0.3, 0.5, extra=lambda tt, n: 0.5 * np.sin(2 * np.pi * np.cumsum(180 + 60 * np.sin(2 * np.pi * 9 * tt)) / SR) * np.exp(-tt * 5))
clang("impact_tray.wav", [(1100, 0.35), (1700, 0.3), (2600, 0.2), (3900, 0.1)], 0.5)
clang("impact_grate.wav", [(620, 0.3), (940, 0.28), (1500, 0.2), (2200, 0.15)], 0.55, noise=0.8)
# giants: lid (porcelain), sofa (heavy wood), parasol/umbrella (cloth + pole), barbecue lid (low metal)
clang("impact_lid.wav", [(900, 0.35), (1500, 0.25), (2400, 0.15)], 0.7, noise=0.4)
thud("impact_sofa.wav", 60, 4, 700, 0.5, 0.8, peak=0.95)
def cloth_pole(name):
    n = int(0.7 * SR); tt = t(0.7)
    x = lowpass(rng.standard_normal(n), 800) * np.exp(-tt * 7) * 0.8 + sum(a * np.sin(2 * np.pi * f * tt) * np.exp(-tt * 6) for f, a in [(700, 0.3), (1400, 0.15)]) * (tt > 0.08)
    write(name, x, 0.85)
cloth_pole("impact_umbrella.wav"); cloth_pole("impact_parasol.wav")
thud("impact_grilllid.wav", 75, 3.5, 2200, 0.35, 0.9, extra=lambda tt, n: 0.4 * np.sin(2 * np.pi * 410 * tt) * np.exp(-tt * 8), peak=0.95)
# ambiences (6 s loops)
def amb(name, build):
    n = int(6.0 * SR); tt = t(6.0); x = build(tt, n); write(name, x, 0.5)
def drip(tt, n):   # bathroom: echoing drips and an extractor fan hum
    x = lowpass(rng.standard_normal(n), 300) * 0.15
    for t0 in (0.5, 1.9, 3.1, 4.6, 5.4):
        m = int(0.25 * SR); tk = t(0.25); i = int(t0 * SR); j = min(n, i + m)
        d = np.sin(2 * np.pi * (1800 * np.exp(-tk * 25) + 500) * tk) * np.exp(-tk * 18) * 0.5
        x[i:j] += d[: j - i]; k = int((t0 + 0.09) * SR); j2 = min(n, k + m); x[k:j2] += 0.3 * d[: j2 - k]
    return x
def tv(tt, n):   # living room: TV murmur (formant noise) and a clock
    x = lowpass(rng.standard_normal(n), 900) * (0.25 + 0.15 * np.sin(2 * np.pi * 0.7 * tt) + 0.1 * np.sin(2 * np.pi * 2.3 * tt + 1))
    for s in range(6):
        i = int(s * SR); m = int(0.02 * SR); tk = t(0.02); x[i:i + m] += np.sin(2 * np.pi * 2200 * tk) * np.exp(-tk * 400) * 0.35
    return x
def waves(tt, n):   # beach: waves (undulating noise) and seagulls
    x = lowpass(rng.standard_normal(n), 1200) * (0.35 + 0.35 * np.sin(2 * np.pi * 0.16 * tt) ** 2)
    for t0, f0 in ((1.2, 1500), (1.5, 1700), (4.2, 1600)):
        m = int(0.35 * SR); tk = t(0.35); i = int(t0 * SR); j = min(n, i + m)
        x[i:j] += (np.sin(2 * np.pi * np.cumsum(f0 * (1 + 0.3 * np.sin(2 * np.pi * 6 * tk))) / SR) * env(m, 0.02, 0.1, 0.6, 0.1) * 0.25)[: j - i]
    return x
def chatter(tt, n):   # café terrace: chatter and cups
    x = lowpass(rng.standard_normal(n), 700) * (0.3 + 0.12 * np.sin(2 * np.pi * 1.1 * tt) + 0.08 * np.sin(2 * np.pi * 3.7 * tt))
    for t0 in (0.8, 2.6, 4.9):
        m = int(0.3 * SR); tk = t(0.3); i = int(t0 * SR); j = min(n, i + m)
        x[i:j] += (sum(a * np.sin(2 * np.pi * f * tk) * np.exp(-tk * 12) for f, a in [(2600, 0.3), (4100, 0.2)]))[: j - i]
    return x
def sizzle(tt, n):   # backyard: birds and the sizzle of the barbecue
    x = lowpass(rng.standard_normal(n), 5000) * 0.12 * (0.6 + 0.4 * np.sin(2 * np.pi * 1.7 * tt) ** 2)
    for t0, f0, f1, d in ((0.4, 2600, 3400, 0.12), (0.6, 3200, 2800, 0.1), (2.9, 3900, 3300, 0.09), (3.06, 3300, 3900, 0.09), (5.0, 2400, 2900, 0.16)):
        m = int(d * SR); tk = t(d); f = f0 + (f1 - f0) * tk / d + 60 * np.sin(2 * np.pi * 40 * tk); i = int(t0 * SR); j = min(n, i + m)
        x[i:j] += (np.sin(2 * np.pi * np.cumsum(f) / SR) * env(m, 0.005, d * 0.4, 0.5, 0.03) * 0.22)[: j - i]
    return x
amb("ambience_bath.wav", drip); amb("ambience_living.wav", tv); amb("ambience_beach.wav", waves); amb("ambience_cafe.wav", chatter); amb("ambience_yard.wav", sizzle)

# ---- ADR-014: request fulfilled (three-note chime) and boss fly (low buzz with beating) ----
star = np.concatenate([note(784, 0.09, 0.5), note(988, 0.09, 0.5), note(1319, 0.22, 0.7)])
write("star.wav", star, 0.6)
n = int(0.9 * SR); tt = t(0.9)
f = 95 + 25 * np.sin(2 * np.pi * 1.5 * tt) - 20 * tt
boss = np.sin(2 * np.pi * np.cumsum(f) / SR) * (1 + 0.6 * np.sin(2 * np.pi * 18 * tt)) + 0.35 * np.sin(2 * np.pi * np.cumsum(f * 2.01) / SR)
write("boss_in.wav", lowpass(boss, 900) * env(n, 0.02, 0.3, 0.6, 0.25), 0.85)

# ---- ADR-015: bait (sweet plop with a sparkle) and slow motion (pitch descent with a breath) ----
n = int(0.35 * SR); tt = t(0.35)
plop = np.sin(2 * np.pi * np.cumsum(420 - 260 * np.clip(tt / 0.08, 0, 1)) / SR) * np.exp(-tt * 18)
shine = (np.sin(2 * np.pi * 2093 * tt) + 0.5 * np.sin(2 * np.pi * 3136 * tt)) * np.exp(-(tt - 0.1) * 14) * (tt > 0.1)
write("bait.wav", plop + 0.35 * shine, 0.6)
n = int(0.6 * SR); tt = t(0.6)
f = 660 * np.exp(-tt * 3.2)
write("slow.wav", (np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.7 + lowpass(rng.standard_normal(n), 900) * 0.5) * env(n, 0.01, 0.2, 0.5, 0.3), 0.55)

# ---- ADR-016: percussion layer (same 4 bars at 112 BPM; rises with fury) and heartbeat (last 10 s) ----
music = np.zeros(N)
def tom(t0, f0=160):
    n = int(0.16 * SR); tt = t(0.16); f = f0 * np.exp(-tt * 12) + f0 * 0.5
    add(np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-tt * 14) * 0.7, t0)
for b in range(bars):
    for k in range(4): kick((b * 4 + k) * beat)                                   # kick on every beat
    for k in range(8): shaker((b * 4 + k * 0.5 + 0.25) * beat, 1.2)               # shaker on the off-beat
    for k in range(16): block((b * 4 + k * 0.25) * beat) if k % 4 == 2 else None  # block on alternating sixteenth notes
    if b == 3:
        for k, f0 in enumerate((220, 190, 160, 130)): tom((b * 4 + 2 + k * 0.5) * beat, f0)   # tom fill at the end
    else: tom((b * 4 + 3.5) * beat, 150)
write("music_layer.wav", music, 0.75)
n = int(0.5 * SR); tt = t(0.5)
def thump(t0, amp):
    m = int(0.14 * SR); tk = t(0.14); i = int(t0 * SR); j = min(n, i + m); x = np.zeros(n)
    x[i:j] = (np.sin(2 * np.pi * np.cumsum(70 * np.exp(-tk * 10) + 40) / SR) * np.exp(-tk * 22) * amp)[: j - i]; return x
write("heartbeat.wav", thump(0.0, 1.0) + thump(0.17, 0.7), 0.85)

# ---- ADR-019: slap (hand clap) and the character's "ouch" (falling formant) ----
n = int(0.22 * SR); tt = t(0.22)
clap = lowpass(rng.standard_normal(n), 3200) * np.exp(-tt * 28) + 0.5 * lowpass(rng.standard_normal(n), 900) * np.exp(-tt * 40)
write("impact_slap.wav", clap * env(n, 0.001, 0.05, 0.3, 0.05), 0.9)
n = int(0.42 * SR); tt = t(0.42)
f0 = 260 * np.exp(-tt * 1.4); ph = 2 * np.pi * np.cumsum(f0) / SR
ouch = np.sin(ph) + 0.6 * np.sin(2 * ph) + 0.35 * np.sin(3 * ph) + 0.2 * np.sin(4 * ph)
ouch = lowpass(ouch * (1 + 0.15 * np.sin(2 * np.pi * 7 * tt)), 1800) * env(n, 0.02, 0.15, 0.6, 0.15)
write("ouch.wav", ouch, 0.6)
n = int(0.6 * SR); tt = t(0.6)
boom = np.sin(2 * np.pi * np.cumsum(70 * np.exp(-tt * 6) + 30) / SR) * np.exp(-tt * 5)
write("pop.wav", boom + 0.6 * lowpass(rng.standard_normal(n), 1500) * np.exp(-tt * 12), 0.9)
