"""
Música y efectos del vídeo de Hakufu, sintetizados desde cero (sin samples ni
licencias de terceros).

    python3 audio/compose.py public/audio

Genera:
  music.wav       pista de 35 s, 100 BPM, si menor (Bm–G–D–A), 48 kHz estéreo
  sfx-*.wav       efectos: tapa del portátil, encendido, barridos, hoja de papel, golpe final

Las secciones van cuadradas con las escenas del vídeo (ver src/timeline.ts).
"""
import os
import sys

import numpy as np
from scipy.io import wavfile
from scipy.signal import butter, fftconvolve, lfilter, sosfilt

SR = 48000
BPM = 100
BEAT = 60 / BPM            # 0,6 s
BAR = BEAT * 4             # 2,4 s
LENGTH = 35.0
rng = np.random.default_rng(7)


def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def midi(n):
    return 440.0 * 2 ** ((n - 69) / 12)


def lowpass(x, cutoff, order=2):
    sos = butter(order, min(cutoff, SR / 2 - 100), "low", fs=SR, output="sos")
    return sosfilt(sos, x)


def highpass(x, cutoff, order=2):
    sos = butter(order, cutoff, "high", fs=SR, output="sos")
    return sosfilt(sos, x)


def bandpass(x, lo, hi, order=2):
    sos = butter(order, [lo, hi], "band", fs=SR, output="sos")
    return sosfilt(sos, x)


def env_adsr(n, a, d, s, r, total):
    """Envolvente ADSR en muestras para una nota de `total` segundos."""
    e = np.zeros(n)
    ai, di, ri = int(a * SR), int(d * SR), int(r * SR)
    hold = max(0, int(total * SR) - ai - di)
    idx = 0
    e[idx:idx + ai] = np.linspace(0, 1, ai, endpoint=False)[: n - idx]; idx += ai
    if idx < n:
        seg = np.linspace(1, s, di, endpoint=False)[: n - idx]; e[idx:idx + len(seg)] = seg; idx += di
    if idx < n:
        seg = np.full(min(hold, n - idx), s); e[idx:idx + len(seg)] = seg; idx += len(seg)
    if idx < n:
        seg = np.linspace(s, 0, ri)[: n - idx]; e[idx:idx + len(seg)] = seg
    return e


class Track:
    def __init__(self, length=LENGTH):
        self.buf = np.zeros((int((length + 4) * SR), 2))

    def add(self, sig, at, gain=1.0, pan=0.0):
        """Mezcla `sig` (mono o estéreo) en el segundo `at` con panorama -1..1."""
        i = int(at * SR)
        if sig.ndim == 1:
            l, r = np.cos((pan + 1) * np.pi / 4), np.sin((pan + 1) * np.pi / 4)
            sig = np.stack([sig * l * 1.414, sig * r * 1.414], axis=1)
        end = min(len(self.buf), i + len(sig))
        self.buf[i:end] += sig[: end - i] * gain


# ── Instrumentos ────────────────────────────────────────────────────────────

def piano(note, dur=2.5, vel=1.0):
    """Piano "de fieltro": parciales ligeramente inarmónicos con caída rápida en los agudos."""
    t = t_axis(dur)
    f = midi(note)
    out = np.zeros_like(t)
    for k in range(1, 9):
        fk = f * k * np.sqrt(1 + 0.0004 * k * k)
        decay = 2.2 + 1.6 * k
        out += np.sin(2 * np.pi * fk * t + rng.uniform(0, 6.28)) * np.exp(-decay * t / 1.6) / (k ** 1.15)
    hammer = lowpass(rng.normal(0, 1, len(t)) * np.exp(-t * 90), 2500) * 0.15
    out = lowpass(out + hammer, 3200)
    out *= np.minimum(1, t / 0.004) * vel
    return out * 0.35


def pad(notes, dur, attack=1.2, release=1.5, bright=1800):
    t = t_axis(dur + release)
    out = np.zeros_like(t)
    for n in notes:
        for det in (-0.08, 0.0, 0.07):
            f = midi(n) * 2 ** (det / 12)
            phase = rng.uniform(0, 1)
            saw = 2 * ((f * t + phase) % 1) - 1
            out += saw
    out = lowpass(out / (len(notes) * 3), bright, order=4)
    return out * env_adsr(len(t), attack, 0.5, 0.85, release, dur) * 0.5


def sub_bass(note, dur):
    t = t_axis(dur)
    f = midi(note)
    x = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * 2 * f * t)
    # Ataque y caída de 15–20 ms: con 5 ms cada nota hacía clic.
    x = np.tanh(1.3 * x) * env_adsr(len(t), 0.015, 0.08, 0.7, 0.05, dur - 0.05)
    return lowpass(x, 400) * 0.42


def pluck(note, dur=0.35):
    t = t_axis(dur)
    f = midi(note)
    saw = 2 * ((f * t) % 1) - 1
    sq = np.sign(np.sin(2 * np.pi * f * 2 * t)) * 0.3
    x = lowpass((saw + sq) * np.exp(-t * 11), 4200)
    return x * 0.16


def kick():
    t = t_axis(0.45)
    f = 45 + 95 * np.exp(-t * 32)
    ph = 2 * np.pi * np.cumsum(f) / SR
    body = np.sin(ph) * np.exp(-t * 7.5)
    click = lowpass(rng.normal(0, 1, len(t)) * np.exp(-t * 300), 5000) * 0.25
    return np.tanh(1.4 * (body + click)) * 0.8


def clap():
    t = t_axis(0.35)
    n = rng.normal(0, 1, len(t))
    e = np.zeros_like(t)
    for off in (0.0, 0.011, 0.022):
        e += np.where(t >= off, np.exp(-(t - off) * 60), 0)
    e += np.exp(-t * 16) * 0.5
    return bandpass(n * e, 900, 3500) * 0.35


def hat(open_=False):
    t = t_axis(0.25 if open_ else 0.06)
    x = highpass(rng.normal(0, 1, len(t)), 7500, order=4) * np.exp(-t * (14 if open_ else 70))
    return x * 0.16


def riser(dur):
    t = t_axis(dur)
    n = rng.normal(0, 1, len(t))
    out = np.zeros_like(t)
    # barrido de un paso de banda hacia arriba, por bloques
    blocks = 48
    size = len(t) // blocks
    for b in range(blocks):
        lo = 300 * (14 ** (b / blocks))
        seg = n[b * size:(b + 1) * size]
        out[b * size:(b + 1) * size] = bandpass(seg, lo, lo * 2.2)
    return out * (t / dur) ** 2 * 0.35


def reverb(x, seconds=2.6, mix=0.28):
    """Reverb por convolución con una respuesta sintética (ruido que decae), distinta en cada canal."""
    n = int(seconds * SR)
    t = np.arange(n) / SR
    out = np.zeros_like(x)
    for ch in range(2):
        ir = rng.normal(0, 1, n) * np.exp(-t * 6.5 / seconds)
        ir = lowpass(ir, 6000)
        ir[: int(0.012 * SR)] = 0  # predelay
        ir /= np.sqrt(np.sum(ir ** 2))
        wet = fftconvolve(x[:, ch], ir)[: len(x)]
        out[:, ch] = x[:, ch] * (1 - mix) + wet * mix * 1.6
    return out


# ── Composición ─────────────────────────────────────────────────────────────

# Bm – G – D – A (raíces en MIDI y triadas para el pad)
CHORDS = [
    (47, [59, 62, 66]),   # Bm
    (43, [55, 59, 62]),   # G
    (50, [57, 62, 66]),   # D
    (45, [57, 61, 64]),   # A
]
ARP = {0: [71, 74, 78, 74], 1: [67, 71, 74, 71], 2: [69, 74, 78, 74], 3: [69, 73, 76, 73]}


def compose():
    music = Track()
    drums = Track()
    n_bars = int(np.ceil(LENGTH / BAR))

    for bar in range(n_bars):
        t0 = bar * BAR
        root, triad = CHORDS[bar % 4]
        # Secciones: intro 0–2 compases, groove 2–5, build 5–9, subida 9, clímax 10–13, final 13+
        intro = bar < 2
        final = t0 >= 31.2

        if final:
            continue

        # Pad: siempre, más brillante según avanza
        bright = 900 if intro else (1500 if bar < 5 else 2600)
        music.add(pad(triad, BAR, attack=1.4 if intro else 0.4, bright=bright), t0, gain=0.9 if intro else 0.75)

        # Piano: notas sueltas en la intro, acorde roto después
        if intro:
            for i, n in enumerate([triad[2] + 12, triad[1] + 12]):
                music.add(piano(n, 3.0, 0.8), t0 + i * BEAT * 2, gain=0.9, pan=-0.2 + 0.4 * i)
        else:
            for i, n in enumerate([triad[0] + 12, triad[1] + 12, triad[2] + 12]):
                music.add(piano(n, 2.4, 0.6), t0 + i * BEAT * 0.5, gain=0.55, pan=-0.3 + 0.3 * i)

        # Bajo a corcheas desde el compás 2
        if bar >= 2:
            for i in range(8):
                vel = 1.0 if i % 2 == 0 else 0.7
                music.add(sub_bass(root, BEAT / 2 * 0.9) * vel, t0 + i * BEAT / 2, gain=0.4)

        # Arpegio a semicorcheas desde el compás 5
        if bar >= 5:
            for i in range(16):
                note = ARP[bar % 4][i % 4] + (12 if bar >= 10 and i % 8 >= 4 else 0)
                music.add(pluck(note), t0 + i * BEAT / 4, gain=0.8 if bar >= 10 else 0.55, pan=0.35 if i % 2 else -0.35)

        # Percusión
        if bar >= 2:
            for b in range(4):
                bt = t0 + b * BEAT
                if bar >= 5 or b in (0, 2):
                    drums.add(kick(), bt, gain=0.7 if bar >= 10 else 0.5)
                if bar >= 5 and b in (1, 3):
                    drums.add(clap(), bt, gain=0.9 if bar >= 10 else 0.6, pan=0.05)
                for h in range(2):
                    drums.add(hat(open_=(h == 1 and bar >= 10)), bt + h * BEAT / 2, gain=0.8, pan=0.25)

    # Subida antes del clímax (compás 9 → 21,6–24 s)
    music.add(riser(BAR), 9 * BAR, gain=1.0)
    for i in range(8):  # redoble de palmas que acelera
        drums.add(clap() * (0.4 + i / 10), 9 * BAR + BAR / 2 + i * BAR / 16, gain=0.6)

    # Final: acorde en D que se queda sonando
    end = 31.2
    music.add(pad([62, 66, 69, 74], 3.0, attack=0.05, release=2.5, bright=2200), end, gain=0.9)
    for i, n in enumerate([50, 62, 66, 69, 74, 78]):
        music.add(piano(n, 4.5, 0.9), end + i * 0.04, gain=0.7, pan=-0.3 + 0.12 * i)
    music.add(sub_bass(38, 1.6), end, gain=0.45)
    drums.add(kick(), end, gain=1.0)

    # Bombeo: el bajo y el pad ceden un poco a cada bombo desde el clímax
    duck = np.ones(len(music.buf))
    for bar in range(5, 13):
        for b in range(4):
            i = int((bar * BAR + b * BEAT) * SR)
            k = np.arange(int(0.28 * SR))
            duck[i:i + len(k)] = np.minimum(duck[i:i + len(k)], 0.55 + 0.45 * (k / len(k)) ** 0.5)
    music.buf *= duck[:, None]

    mix = reverb(music.buf, 2.8, 0.3) + reverb(drums.buf, 1.2, 0.12)
    mix = mix[: int(LENGTH * SR)]
    # Fundido final y limitador suave
    fade = int(1.2 * SR)
    mix[-fade:] *= np.linspace(1, 0, fade)[:, None]
    mix = np.tanh(mix * 0.9)
    mix /= np.max(np.abs(mix)) / 0.89
    return mix


# ── Efectos ─────────────────────────────────────────────────────────────────

def stereo(x):
    return np.stack([x, x], axis=1)


def sfx_lid():
    """Tapa del portátil: bisagra suave (ruido filtrado) y un golpe sordo al llegar."""
    t = t_axis(1.0)
    hinge = bandpass(rng.normal(0, 1, len(t)), 1800, 4500) * np.exp(-((t - 0.25) / 0.18) ** 2) * 0.12
    thump_t = t_axis(0.3)
    thump = np.sin(2 * np.pi * (70 + 40 * np.exp(-thump_t * 30)) * thump_t) * np.exp(-thump_t * 18) * 0.5
    out = hinge
    i = int(0.55 * SR)
    out[i:i + len(thump)] += thump[: len(out) - i]
    return stereo(out)


def sfx_power():
    """Encendido: campanita suave en dos notas."""
    t = t_axis(2.2)
    out = np.zeros_like(t)
    for f, at in ((midi(86), 0.0), (midi(93), 0.09)):
        tt = np.clip(t - at, 0, None)
        out += np.sin(2 * np.pi * f * tt) * np.exp(-tt * 3.2) * (t >= at) * 0.18
        out += np.sin(2 * np.pi * f * 2.01 * tt) * np.exp(-tt * 6) * (t >= at) * 0.05
    return reverb(stereo(out), 2.0, 0.4)


def sfx_whoosh(dur=0.9):
    t = t_axis(dur)
    n = rng.normal(0, 1, len(t))
    env = np.sin(np.pi * t / dur) ** 2
    out = np.zeros_like(t)
    blocks = 30
    size = len(t) // blocks
    for b in range(blocks):
        c = 400 * (8 ** (np.sin(np.pi * b / blocks)))
        seg = n[b * size:(b + 1) * size]
        out[b * size:(b + 1) * size] = bandpass(seg, c, c * 2.5)
    out *= env * 0.4
    l = out * np.linspace(1, 0.4, len(out))
    r = out * np.linspace(0.4, 1, len(out))
    return np.stack([l, r], axis=1)


def sfx_page():
    """Hoja de papel: roce con crujidos y un pequeño golpe de aire al caer."""
    dur = 0.55
    t = t_axis(dur)
    n = rng.normal(0, 1, len(t))
    crinkle = np.zeros_like(t)
    for _ in range(14):
        at = rng.uniform(0.02, 0.32)
        crinkle += np.exp(-np.abs(t - at) * rng.uniform(250, 600)) * rng.uniform(0.3, 1)
    swish = np.sin(np.clip(t / 0.4, 0, 1) * np.pi) ** 1.5
    x = bandpass(n, 1200, 7000) * (0.25 * swish + 0.6 * crinkle)
    flap = lowpass(n, 400) * np.exp(-np.abs(t - 0.42) * 40) * 0.6
    x = (x + flap) * 0.5
    return reverb(stereo(x), 0.6, 0.15)


def sfx_hit():
    """Golpe final del logo: impacto grave + brillo."""
    t = t_axis(3.0)
    boom = np.sin(2 * np.pi * (40 + 60 * np.exp(-t * 12)) * t) * np.exp(-t * 2.5) * 0.7
    air = highpass(rng.normal(0, 1, len(t)), 5000) * np.exp(-t * 4) * 0.06
    shimmer = sum(np.sin(2 * np.pi * midi(n) * t) * np.exp(-t * 1.6) for n in (86, 90, 93, 98)) * 0.04
    return reverb(stereo(np.tanh(boom + air + shimmer)), 2.5, 0.35)


def write(path, x):
    x = np.clip(x, -1, 1)
    wavfile.write(path, SR, (x * 32767).astype(np.int16))


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else "public/audio"
    os.makedirs(out, exist_ok=True)
    write(os.path.join(out, "music.wav"), compose())
    write(os.path.join(out, "sfx-lid.wav"), sfx_lid())
    write(os.path.join(out, "sfx-power.wav"), sfx_power())
    write(os.path.join(out, "sfx-whoosh.wav"), sfx_whoosh())
    write(os.path.join(out, "sfx-page.wav"), sfx_page())
    write(os.path.join(out, "sfx-hit.wav"), sfx_hit())
    print("listo:", out)
