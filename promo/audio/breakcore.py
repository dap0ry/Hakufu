"""
Breakcore de 20 s para el vídeo glitch de Hakufu, sintetizado desde cero
(sin samples ni licencias de terceros).

    python3 audio/breakcore.py public/glitch

Genera:
  breakcore.wav           pista de 20 s, 180 BPM (1 pulso = 10 frames a 30 fps), 48 kHz estéreo
  ../../src/glitch/hits.ts  golpes de bombo/caja/stutter en frames, para que el vídeo vaya al golpe

Estructura (compases de 4 pulsos = 1,333 s = 40 frames):
  0-1   intro: pad + campanas, filtrado, redoble al final
  2-7   drop 1: break troceado + reese
  8     respiro: medio tiempo, solo pad y bombo
  9     subida: redoble acelerando
  10-13 drop 2: caos, más stutters y repeticiones
  14    golpe final + parada de cinta
"""
import json
import os
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, sosfilt

SR = 48000
BPM = 180
BEAT = 60 / BPM            # 1/3 s
BAR = BEAT * 4
SIX = BAR / 16             # semicorchea
LENGTH = 20.0
FPS = 30
rng = np.random.default_rng(23)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def lp(x, f, order=2):
    return sosfilt(butter(order, min(f, SR / 2 - 100), "low", fs=SR, output="sos"), x, axis=0)


def hp(x, f, order=2):
    return sosfilt(butter(order, f, "high", fs=SR, output="sos"), x, axis=0)


def bp(x, lo, hi, order=2):
    return sosfilt(butter(order, [lo, hi], "band", fs=SR, output="sos"), x, axis=0)


def saw(freq, t, phase=0.0):
    p = (freq * t + phase) % 1.0
    return 2 * p - 1


def reverb(x, dur=1.8, decay=3.0, seed=1):
    r = np.random.default_rng(seed)
    n = int(dur * SR)
    env = np.exp(-decay * np.arange(n) / SR * (3 / dur))
    irl = r.standard_normal(n) * env
    irr = r.standard_normal(n) * env
    irl, irr = lp(irl, 6000), lp(irr, 6000)
    irl /= np.sqrt(np.sum(irl ** 2)); irr /= np.sqrt(np.sum(irr ** 2))
    if x.ndim == 1:
        x = np.stack([x, x], axis=1)
    return np.stack([fftconvolve(x[:, 0], irl)[: len(x)], fftconvolve(x[:, 1], irr)[: len(x)]], axis=1)


# ── Golpes de batería ──────────────────────────────────────────────────────────

def kick():
    t = t_axis(0.32)
    f = 48 + 140 * np.exp(-t * 38)
    ph = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(ph) * np.exp(-t * 9)
    click = hp(rng.standard_normal(len(t)), 3000) * np.exp(-t * 400) * 0.5
    return np.tanh((body + click) * 2.2) * 0.9


def snare(bright=1.0):
    t = t_axis(0.28)
    tone = (np.sin(2 * np.pi * 185 * t) + 0.5 * np.sin(2 * np.pi * 330 * t)) * np.exp(-t * 28)
    noise = bp(rng.standard_normal(len(t)), 900, 9000) * np.exp(-t * (16 / bright))
    return np.tanh((tone * 0.8 + noise * 1.3) * 2.0) * 0.8


def hat(open_=False):
    t = t_axis(0.25 if open_ else 0.06)
    n = hp(rng.standard_normal(len(t)), 7000) * np.exp(-t * (14 if open_ else 70))
    return n * 0.35


def ride():
    t = t_axis(0.5)
    parts = [3.0, 4.16, 5.43, 6.79, 8.21]
    s = sum(np.sign(np.sin(2 * np.pi * 520 * p * t)) for p in parts)
    return hp(s, 5000) * np.exp(-t * 7) * 0.05


def build_break():
    """Un compás de break tipo amen (16 semicorcheas) como fuente para trocear.
    Devuelve el audio y qué golpe principal hay en cada semicorchea."""
    n = int(BAR * SR) + int(0.4 * SR)
    buf = np.zeros(n)
    # K=bombo S=caja g=caja fantasma
    pat = "K.K.S..gg.KKS..g"
    kinds = []
    for i, c in enumerate(pat):
        at = int(i * SIX * SR)
        if c == "K":
            k = kick(); buf[at:at + len(k)] += k
        elif c == "S":
            s = snare(); buf[at:at + len(s)] += s
        elif c == "g":
            s = snare(0.6) * 0.35; buf[at:at + len(s)] += s
        if i % 2 == 0:
            h = ride() if i % 4 == 0 else hat(); buf[at:at + len(h)] += h
        else:
            h = hat() * 0.6; buf[at:at + len(h)] += h
        kinds.append(c)
    # sala corta para que suene a break grabado
    wet = reverb(buf, dur=0.35, decay=4.0, seed=4)[:, 0]
    buf = np.tanh((buf + wet * 0.25) * 1.4)
    return buf, kinds


BREAK, KINDS = build_break()


def slice_(i, length=1, rate=1.0, reverse=False):
    """Trozo de `length` semicorcheas desde la semicorchea `i` del break."""
    a = int(i * SIX * SR)
    n_out = int(length * SIX * SR)
    n_src = int(n_out * rate)
    src = BREAK[a:a + n_src]
    if len(src) < n_src:
        src = np.pad(src, (0, n_src - len(src)))
    if rate != 1.0:
        src = np.interp(np.linspace(0, n_src - 1, n_out), np.arange(n_src), src)
    if reverse:
        src = src[::-1]
    fade = min(64, len(src) // 4)
    if fade:
        src[:fade] *= np.linspace(0, 1, fade)
        src[-fade:] *= np.linspace(1, 0, fade)
    return src


# ── Pista ──────────────────────────────────────────────────────────────────────

class Track:
    def __init__(self):
        self.buf = np.zeros((int((LENGTH + 3) * SR), 2))

    def add(self, sig, at, gain=1.0, pan=0.0):
        i = int(round(at * SR))
        if sig.ndim == 1:
            l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
            sig = np.stack([sig * l * 1.414, sig * r * 1.414], axis=1)
        end = min(len(self.buf), i + len(sig))
        if end > i:
            self.buf[i:end] += sig[: end - i] * gain


hits = []          # (frame, tipo) para el vídeo


def hit(at, kind):
    hits.append((int(round(at * FPS)), kind))


def play_bar(tr, bar, chaos, gain=1.0):
    """Trocea el break en un compás. chaos 0..1 = cuánto se desordena."""
    t0 = bar * BAR
    i = 0
    while i < 16:
        at = t0 + i * SIX
        r = rng.random()
        if r < chaos * 0.22 and i < 14:
            # stutter: repetir un golpe en fusas o en tresillos de fusa
            src = rng.choice([4, 12, 0, 10])
            reps = rng.choice([4, 6, 8])
            step = 2 * SIX / reps
            for k in range(reps):
                s = slice_(src, length=step / SIX, rate=1.0 + 0.04 * k * rng.choice([-1, 1]))
                tr.add(s, at + k * step, gain * (0.7 + 0.3 * k / reps))
            hit(at, "stutter")
            i += 2
            continue
        if r < chaos * 0.32:
            src = int(rng.integers(0, 16))
            tr.add(slice_(src, 1, reverse=True), at, gain * 0.8)
            i += 1
            continue
        if r < chaos * 0.45:
            src = int(rng.integers(0, 16))
            rate = rng.choice([0.5, 0.75, 1.5])
            tr.add(slice_(src, 1, rate=rate), at, gain)
            if KINDS[src] in "KS":
                hit(at, "kick" if KINDS[src] == "K" else "snare")
            i += 1
            continue
        src = i if rng.random() > chaos * 0.5 else int(rng.choice([0, 2, 4, 10, 11, 12]))
        tr.add(slice_(src, 1), at, gain)
        if KINDS[src] in "KS":
            hit(at, "kick" if KINDS[src] == "K" else "snare")
        i += 1


def snare_roll(tr, start, dur, accel=True):
    t = start
    while t < start + dur:
        p = (t - start) / dur
        step = SIX * (2 if p < 0.33 else 1 if p < 0.66 else 0.5) if accel else SIX
        tr.add(snare() * (0.35 + 0.65 * p), t, 0.8)
        hit(t, "roll")
        t += step


def tape_stop(x, start, dur):
    """Frena el audio de `start` a `start+dur` como una cinta que se para."""
    a, n = int(start * SR), int(dur * SR)
    seg = x[a:a + n * 2].copy()
    speed = np.linspace(1, 0, n) ** 1.5
    pos = np.cumsum(speed)
    pos = np.clip(pos, 0, len(seg) - 1)
    out = np.stack([np.interp(pos, np.arange(len(seg)), seg[:, c]) for c in range(2)], axis=1)
    out *= np.linspace(1, 0, n)[:, None]
    x[a:a + n] = out
    x[a + n:] = 0
    return x


# acordes (2 compases cada uno): Dmaj7 – F#m7 – Bm9 – A6
CHORDS = [
    (50, [62, 66, 69, 73]),
    (54, [61, 66, 69, 73]),
    (47, [62, 66, 69, 73, 61]),
    (45, [61, 64, 66, 69]),
]


def chord_at(bar):
    return CHORDS[(bar // 2) % len(CHORDS)]


def pad(tr):
    for bar in range(0, 15, 2):
        root, notes = chord_at(bar)
        dur = BAR * 2
        t = t_axis(dur + 0.6)
        env = np.minimum(1, t / 0.25) * np.minimum(1, np.maximum(0, (dur + 0.6 - t) / 0.6))
        sig = np.zeros((len(t), 2))
        for n in notes:
            for d in (-0.11, -0.04, 0.0, 0.05, 0.12):
                f = midi(n + d)
                sig[:, 0] += saw(f, t, rng.random()) * (1 if d <= 0 else 0.6)
                sig[:, 1] += saw(f * 1.002, t, rng.random()) * (1 if d >= 0 else 0.6)
        sig = lp(sig, 2600) * env[:, None] * 0.018
        tr.add(sig, bar * BAR)


def bells(tr, bars):
    for bar in bars:
        _, notes = chord_at(bar)
        arp = sorted(n + 12 for n in notes)
        for i in range(8):
            n = arp[(i * 3) % len(arp)] + (12 if i % 4 == 3 else 0)
            t = t_axis(0.9)
            f = midi(n)
            mod = np.sin(2 * np.pi * f * 3.5 * t) * 2.2 * np.exp(-t * 8)
            s = np.sin(2 * np.pi * f * t + mod) * np.exp(-t * 4.5) * 0.09
            tr.add(s, bar * BAR + i * 2 * SIX, pan=-0.5 if i % 2 else 0.5)


def reese(tr, bars):
    for bar in bars:
        root, _ = chord_at(bar)
        t = t_axis(BAR)
        f = midi(root - 12)
        s = saw(f * 0.995, t) + saw(f * 1.006, t, 0.3) + 0.5 * saw(f * 2.003, t, 0.7)
        # filtro que se abre y cierra: mezcla de un paso bajo cerrado y otro abierto
        lfo = np.sin(2 * np.pi * 0.75 * (t + bar * BAR)) ** 2
        s = lp(s, 320, order=4) * (1 - lfo) + lp(s, 900, order=4) * lfo
        # bombeo con el pulso
        duck = 1 - 0.75 * np.exp(-((t % BEAT) / 0.07))
        s = np.tanh(s * 1.8) * duck * 0.22
        tr.add(s, bar * BAR)


def sub_hits(tr, bars):
    for bar in bars:
        root, _ = chord_at(bar)
        t = t_axis(BAR)
        s = np.sin(2 * np.pi * midi(root - 24) * t) * np.minimum(1, (BAR - t) / 0.05) * 0.25
        duck = 1 - 0.9 * np.exp(-((t % BEAT) / 0.06))
        tr.add(s * duck, bar * BAR)


def crackle(n):
    c = np.zeros(n)
    idx = rng.integers(0, n, n // 2500)
    c[idx] = rng.standard_normal(len(idx)) * 0.4
    return hp(c, 1500) + hp(rng.standard_normal(n), 4000) * 0.004


def riser(dur):
    t = t_axis(dur)
    n = bp(rng.standard_normal(len(t)), 800, 12000)
    sweep = np.sin(2 * np.pi * np.cumsum(300 + 2500 * (t / dur) ** 2) / SR)
    return (n * 0.5 + sweep * 0.15) * (t / dur) ** 2


def main(out_dir):
    os.makedirs(out_dir, exist_ok=True)
    music = Track()
    drums = Track()

    # intro
    pad(music)
    bells(music, [0, 1, 8, 9])
    music.add(riser(BAR * 2), 0, 0.35)
    snare_roll(drums, BAR * 1.5, BAR * 0.5)
    hit(0, "start")

    # drop 1
    for bar in range(2, 8):
        play_bar(drums, bar, chaos=0.25 + 0.08 * (bar - 2))
    reese(music, range(2, 8))
    sub_hits(music, range(2, 8))
    bells(music, [4, 5, 6, 7])

    # respiro (medio tiempo) y subida
    for k in (0, 6):
        drums.add(kick(), 8 * BAR + k * SIX, 0.9); hit(8 * BAR + k * SIX, "kick")
    drums.add(snare(), 8 * BAR + 8 * SIX, 0.8); hit(8 * BAR + 8 * SIX, "snare")
    snare_roll(drums, 9 * BAR, BAR)
    music.add(riser(BAR * 2), 8 * BAR, 0.5)

    # drop 2
    for bar in range(10, 14):
        play_bar(drums, bar, chaos=0.6 + 0.1 * (bar - 10), gain=1.05)
    reese(music, range(10, 14))
    sub_hits(music, range(10, 15))
    bells(music, [10, 11, 12, 13])

    # golpe final
    drums.add(kick(), 14 * BAR, 1.1); drums.add(snare(), 14 * BAR, 1.0)
    hit(14 * BAR, "final")
    drums.add(reverb(snare(), dur=2.0, decay=2.5, seed=9), 14 * BAR, 0.6)

    # mezcla: batería con saturación de bus, pad con reverb larga
    d = drums.buf
    d = np.tanh(d * 1.6) * 0.9
    m = music.buf
    m = m + reverb(m, dur=2.6, decay=2.2, seed=2) * 0.45
    mix = d + m
    mix[:, 0] += crackle(len(mix)) * 0.5
    mix[:, 1] += crackle(len(mix)) * 0.5

    # parada de cinta en el último compás y corte a 20 s
    mix = tape_stop(mix, 14 * BAR + BEAT * 0.5, BAR - BEAT * 0.5 - 0.02)
    mix = mix[: int(LENGTH * SR)]
    mix = hp(mix, 28)
    peak = np.max(np.abs(mix))
    mix = np.tanh(mix / peak * 1.3) / np.tanh(1.3) * 0.93
    wavfile.write(os.path.join(out_dir, "breakcore.wav"), SR, (mix * 32767).astype(np.int16))

    seen = {}
    for f, k in sorted(hits):
        if f < LENGTH * FPS and (f not in seen or k in ("final", "kick")):
            seen[f] = k
    here = os.path.dirname(os.path.abspath(__file__))
    ts = os.path.join(here, "..", "src", "glitch", "hits.ts")
    os.makedirs(os.path.dirname(ts), exist_ok=True)
    with open(ts, "w") as fh:
        fh.write("// Generado por audio/breakcore.py: no editar a mano.\n")
        fh.write("export type HitKind = \"kick\" | \"snare\" | \"stutter\" | \"roll\" | \"start\" | \"final\";\n")
        fh.write("export const HITS: [number, HitKind][] = ")
        fh.write(json.dumps([[f, k] for f, k in sorted(seen.items())]))
        fh.write(";\n")
    print("ok", len(seen), "golpes")


if __name__ == "__main__":
    main(sys.argv[1] if len(sys.argv) > 1 else "public/glitch")
