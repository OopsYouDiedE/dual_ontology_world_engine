"""Procedural 1890s sound effects for the casebook game.

Every cue is synthesised from physical sketches (inharmonic brass partials, filtered
paper noise, wooden resonators). Edit PARAMS and rerun to regenerate:

    python3 tools/audio/synth_sfx.py            # all cues
    python3 tools/audio/synth_sfx.py page bell_slot

Output: assets/environment/Audio/Sfx/<name>.ogg (mono, 44.1 kHz, Vorbis q5).
Requires numpy and ffmpeg with libvorbis.
"""
import subprocess, sys, tempfile, wave
from pathlib import Path
import numpy as np

SR = 44100
ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "assets/environment/Audio/Sfx"

PARAMS = {
    "page":        {"length": 0.42, "brightness": 3200.0, "flutters": 5},
    "ui_click":    {"pitch": 1750.0, "decay": 0.035},
    "evidence":    {"scratch": 0.32, "bell": 988.0},
    "answer_ok":   {"notes": [784.0, 1046.5], "decay": 0.9},
    "answer_wrong":{"thud": 92.0, "notes": [311.1, 293.7]},
    "bell_slot":   {"pitch": 1318.5, "decay": 1.6, "tremolo": 6.5},
    "door_open":   {"creak_from": 210.0, "creak_to": 330.0, "length": 0.9},
    "door_close":  {"thud": 68.0, "latch": 2400.0},
    "door_unbar":  {"scrape": 0.45, "clunk": 120.0},
    "tube":        {"length": 0.85, "sweep_from": 400.0, "sweep_to": 2600.0},
    "tuning_fork": {"pitch": 440.0, "decay": 2.4},
    "replay":      {"ticks": 9, "start_gap": 0.11, "end_gap": 0.04},
}

rng = np.random.default_rng(1890)

def t(seconds): return np.arange(int(SR * seconds)) / SR
def env(n, attack=0.002, decay=0.2):
    x = np.arange(n) / SR
    return np.minimum(1.0, x / max(attack, 1e-4)) * np.exp(-x / max(decay, 1e-4))
def noise(seconds): return rng.uniform(-1, 1, int(SR * seconds))
def bandpass(x, centre, q=4.0):
    # two-pole resonator (RBJ band-pass, constant peak gain)
    w = 2 * np.pi * centre / SR; alpha = np.sin(w) / (2 * q)
    b0, b2 = alpha, -alpha; a0, a1, a2 = 1 + alpha, -2 * np.cos(w), 1 - alpha
    y = np.zeros_like(x); x1 = x2 = y1 = y2 = 0.0
    for i, v in enumerate(x):
        o = (b0 * v + b2 * x2 - a1 * y1 - a2 * y2) / a0
        x2, x1, y2, y1 = x1, v, y1, o; y[i] = o
    return y
def lowpass(x, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR); y = np.zeros_like(x); s = 0.0
    for i, v in enumerate(x): s = (1 - a) * v + a * s; y[i] = s
    return y
def brass(freq, seconds, decay):
    x = t(seconds); out = np.zeros_like(x)
    for ratio, amp, d in ((1.0, 1.0, 1.0), (2.76, 0.45, 0.55), (5.40, 0.22, 0.3), (8.93, 0.1, 0.18)):
        out += amp * np.sin(2 * np.pi * freq * ratio * x) * np.exp(-x / (decay * d))
    return out * env(len(x), 0.001, 99)
def mix(*parts):
    n = max(int(at * SR) + len(p) for at, p in parts); out = np.zeros(n)
    for at, p in parts:
        i = int(at * SR); out[i:i + len(p)] += p[: n - i]
    return out
def pad(x, seconds): return np.concatenate([x, np.zeros(int(SR * seconds))])

def make(name, p):
    if name == "page":
        n = noise(p["length"]); e = np.zeros_like(n); x = t(p["length"])
        for k in range(p["flutters"]):
            c = (k + 0.5) / p["flutters"] * p["length"]
            e += np.exp(-((x - c) / 0.035) ** 2) * (1 - k / (p["flutters"] + 1))
        return lowpass(bandpass(n, p["brightness"], 0.7) * e, 7000) * 1.6
    if name == "ui_click":
        n = noise(0.06) * env(int(SR * 0.06), 0.0005, 0.004)
        return bandpass(n, p["pitch"], 6.0) * 4.0 + np.sin(2*np.pi*p["pitch"]*t(0.06)) * env(int(SR*0.06), 0.0005, p["decay"]) * 0.25
    if name == "evidence":
        sc = noise(p["scratch"]); jitter = 0.5 + 0.5 * np.abs(np.sin(2 * np.pi * 23 * t(p["scratch"])))
        scratch = bandpass(sc, 4200, 2.5) * jitter * env(len(sc), 0.01, 0.25) * 1.4
        return mix((0, scratch), (p["scratch"] * 0.85, brass(p["bell"], 1.1, 0.5) * 0.35))
    if name == "answer_ok":
        a, b = p["notes"]
        return mix((0, brass(a, 1.2, p["decay"]) * 0.5), (0.14, brass(b, 1.4, p["decay"]) * 0.5))
    if name == "answer_wrong":
        thud = np.sin(2 * np.pi * p["thud"] * t(0.35)) * env(int(SR * 0.35), 0.002, 0.09)
        knock = bandpass(noise(0.08), 260, 3) * env(int(SR * 0.08), 0.001, 0.02) * 2
        a, b = p["notes"]
        tone = (np.sin(2*np.pi*a*t(0.7)) + np.sin(2*np.pi*b*t(0.7))) * env(int(SR*0.7), 0.01, 0.25) * 0.18
        return mix((0, thud * 0.8), (0, knock), (0.05, tone))
    if name == "bell_slot":
        x = t(2.2); trem = 1 - 0.25 * (0.5 + 0.5 * np.sin(2 * np.pi * p["tremolo"] * x))
        clapper = bandpass(noise(0.02), 3000, 2) * env(int(SR * 0.02), 0.0005, 0.004)
        return mix((0, brass(p["pitch"], 2.2, p["decay"]) * trem * 0.55), (0, clapper))
    if name == "door_open":
        x = t(p["length"]); f = np.linspace(p["creak_from"], p["creak_to"], len(x)) * (1 + 0.04 * np.sin(2*np.pi*7*x))
        phase = 2 * np.pi * np.cumsum(f) / SR; saw = 2 * ((phase / (2*np.pi)) % 1) - 1
        stick = (rng.random(len(x)) < 0.9).astype(float) * 0.7 + 0.3
        creak = bandpass(saw * stick, 900, 1.5) * env(len(x), 0.08, 0.6) * 1.5
        latch = bandpass(noise(0.05), 2600, 5) * env(int(SR*0.05), 0.0005, 0.008) * 3
        return mix((0, latch), (0.06, creak))
    if name == "door_close":
        thud = np.sin(2*np.pi*p["thud"]*t(0.5)) * env(int(SR*0.5), 0.003, 0.12)
        body = lowpass(noise(0.3), 400) * env(int(SR*0.3), 0.002, 0.05) * 2
        latch = bandpass(noise(0.04), p["latch"], 6) * env(int(SR*0.04), 0.0005, 0.006) * 3
        return mix((0, thud), (0, body), (0.09, latch))
    if name == "door_unbar":
        sc = noise(p["scrape"]); scrape = bandpass(sc, 700, 1.2) * (0.6 + 0.4*np.sin(2*np.pi*31*t(p["scrape"]))) * env(len(sc), 0.03, 0.3)
        clunk = mix((0, np.sin(2*np.pi*p["clunk"]*t(0.35)) * env(int(SR*0.35), 0.002, 0.07)),
                    (0, bandpass(noise(0.06), 500, 2) * env(int(SR*0.06), 0.001, 0.015) * 2))
        return mix((0, scrape * 1.3), (p["scrape"] - 0.03, clunk))
    if name == "tube":
        n = noise(p["length"]); x = t(p["length"]); out = np.zeros_like(n)
        cents = np.geomspace(p["sweep_from"], p["sweep_to"], 8)
        seg = len(n) // len(cents)
        for i, c in enumerate(cents):
            out[i*seg:(i+1)*seg] = bandpass(n[i*seg:(i+1)*seg], c, 1.0)
        whoosh = out * np.sin(np.pi * x / p["length"]) ** 2 * 1.4
        thunk = np.sin(2*np.pi*110*t(0.25)) * env(int(SR*0.25), 0.002, 0.06)
        return mix((0, whoosh), (p["length"] - 0.02, thunk))
    if name == "tuning_fork":
        x = t(p["decay"] * 1.3)
        return (np.sin(2*np.pi*p["pitch"]*x) + 0.08*np.sin(2*np.pi*p["pitch"]*6.27*x)*np.exp(-x/0.15)) * env(len(x), 0.004, p["decay"]) * 0.5
    if name == "replay":
        gaps = np.linspace(p["start_gap"], p["end_gap"], p["ticks"]); at = 0.0; parts = []
        for g in gaps:
            tick = bandpass(noise(0.03), 3400, 8) * env(int(SR*0.03), 0.0003, 0.005) * 3
            parts.append((at, tick)); at += g
        parts.append((at, brass(1568.0, 0.6, 0.25) * 0.25))
        return mix(*parts)
    raise KeyError(name)

def write(name, signal):
    signal = pad(signal, 0.05)
    peak = np.max(np.abs(signal)) or 1.0
    pcm = (signal / peak * 0.89 * 32767).astype(np.int16)
    OUT.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory() as tmp:
        wav = Path(tmp) / (name + ".wav")
        with wave.open(str(wav), "wb") as w:
            w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
        subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", str(wav), "-c:a", "libvorbis", "-q:a", "5", str(OUT / (name + ".ogg"))], check=True)

if __name__ == "__main__":
    names = sys.argv[1:] or list(PARAMS)
    for n in names:
        write(n, make(n, PARAMS[n])); print("wrote", OUT / (n + ".ogg"))
