import React, { useId } from "react";
import { AbsoluteFill, Img, random, staticFile } from "remotion";
import { HITS, type HitKind } from "./hits";

// Vídeo glitch: 1920×1080, 30 fps, 20 s, breakcore a 180 BPM (1 pulso = 10 frames).
export const W = 1920;
export const H = 1080;

export const C = {
  paper: "#F1F1EC",
  ink: "#0B0B0C",
  blue: "#1F3BFF",
  sky: "#3D9BFF",
  red: "#E5001A",
  grey: "#C0C0C0",
  navy: "#000080",
};
export const JP = '"Hiragino Kaku Gothic ProN", "Hiragino Sans", sans-serif';
export const MONO = 'Menlo, "SF Mono", monospace';

export const img = (f: string) => staticFile(`glitch/${f}`);

// ── Golpes de la música ────────────────────────────────────────────────────────

// 1 justo en el golpe y cae con `decay` frames.
export const env = (f: number, kinds?: HitKind[], decay = 4) => {
  let v = 0;
  for (const [h, k] of HITS) {
    if (h > f) break;
    if (kinds && !kinds.includes(k)) continue;
    v = Math.max(v, Math.exp(-(f - h) / decay));
  }
  return v;
};

// Índice del último golpe (para que cada golpe tenga su propio azar).
export const hitIndex = (f: number, kinds?: HitKind[]) => {
  let idx = -1;
  HITS.forEach(([h, k], i) => {
    if (h <= f && (!kinds || kinds.includes(k))) idx = i;
  });
  return idx;
};

export const hitsBetween = (a: number, b: number, kinds?: HitKind[]) =>
  HITS.filter(([h, k]) => h >= a && h < b && (!kinds || kinds.includes(k))).map(([h]) => h);

// ── Glitch: franjas desplazadas + separación RGB ───────────────────────────────

const RgbFilter: React.FC<{ id: string; dx: number }> = ({ id, dx }) => (
  <svg width={0} height={0} style={{ position: "absolute" }}>
    <filter id={id} x="-5%" y="0" width="110%" height="100%" colorInterpolationFilters="sRGB">
      <feColorMatrix in="SourceGraphic" type="matrix" values="1 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 1 0" result="r" />
      <feOffset in="r" dx={dx} dy={0} result="r2" />
      <feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0 0 1 0 0 0 0 0 0 0 0 0 0 0 1 0" result="g" />
      <feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0 0 0 0 0 0 0 0 1 0 0 0 0 0 1 0" result="b" />
      <feOffset in="b" dx={-dx} dy={0} result="b2" />
      <feBlend in="r2" in2="g" mode="screen" result="rg" />
      <feBlend in="rg" in2="b2" mode="screen" />
    </filter>
  </svg>
);

export const Glitch: React.FC<{
  amount: number;          // 0..1 cuánto se rompe en franjas
  rgb?: number;            // píxeles de separación de color
  seed: string | number;
  children: React.ReactNode;
}> = ({ amount, rgb = 0, seed, children }) => {
  const id = "rgb" + useId().replace(/[^a-zA-Z0-9]/g, "");
  const inner =
    rgb >= 1 ? (
      <AbsoluteFill style={{ filter: `url(#${id})` }}>
        <RgbFilter id={id} dx={rgb} />
        {children}
      </AbsoluteFill>
    ) : (
      children
    );
  if (amount < 0.06) return <AbsoluteFill>{inner}</AbsoluteFill>;
  const n = 7 + Math.floor(random(`${seed}-n`) * 6);
  const cuts = [0, ...Array.from({ length: n - 1 }, (_, i) => random(`${seed}-c${i}`) * 100).sort((a, b) => a - b), 100];
  return (
    <AbsoluteFill>
      {cuts.slice(0, -1).map((top, i) => {
        const bottom = cuts[i + 1];
        const on = random(`${seed}-on${i}`) < 0.55;
        const off = on ? (random(`${seed}-o${i}`) - 0.5) * 2 * amount * 160 : 0;
        return (
          <AbsoluteFill key={i} style={{ clipPath: `inset(${top}% 0 ${100 - bottom}% 0)`, transform: `translateX(${off}px)` }}>
            {inner}
          </AbsoluteFill>
        );
      })}
    </AbsoluteFill>
  );
};

// ── Imagen de la app recortada dentro de un marco ──────────────────────────────

export const Shot: React.FC<{
  src: string;
  w: number;
  h: number;
  iw?: number;
  ih?: number;
  zoom?: number;
  panX?: number;           // 0 izquierda … 1 derecha
  panY?: number;
}> = ({ src, w, h, iw = 1280, ih = 800, zoom = 1, panX = 0.5, panY = 0.5 }) => {
  const s = Math.max(w / iw, h / ih) * zoom;
  const sw = iw * s;
  const sh = ih * s;
  return (
    <div style={{ position: "absolute", inset: 0, overflow: "hidden", background: C.ink }}>
      <Img src={src} style={{ position: "absolute", width: sw, height: sh, left: -(sw - w) * panX, top: -(sh - h) * panY }} />
    </div>
  );
};

// ── Fragmentos dentados (como cristal roto) ────────────────────────────────────

export const Shards: React.FC<{
  seed: string | number;
  amount: number;          // 0..1, crecen desde el centro
  cx?: number;
  cy?: number;
  count?: number;
  colors?: string[];
}> = ({ seed, amount, cx = W / 2, cy = H / 2, count = 14, colors = [C.blue, C.red, C.ink, C.blue, "#fff"] }) => {
  if (amount < 0.04) return null;
  const grow = Math.min(1, amount * 1.6);
  return (
    <svg width={W} height={H} style={{ position: "absolute", inset: 0 }}>
      {Array.from({ length: count }, (_, i) => {
        const r = (k: string) => random(`${seed}-${i}-${k}`);
        const bx = cx + (r("x") - 0.5) * 360;
        const by = cy + (r("y") - 0.5) * 520;
        const a = r("a") * Math.PI * 2;
        const len = (180 + r("l") * 820) * grow;
        const wid = 10 + r("w") * 70;
        const dx = Math.cos(a);
        const dy = Math.sin(a);
        const nx = -dy;
        const ny = dx;
        const j1 = 0.35 + r("j1") * 0.3;
        const j2 = 0.5 + r("j2") * 0.3;
        const pts = [
          [bx + nx * wid, by + ny * wid],
          [bx + dx * len * j1 + nx * wid * (0.2 + r("k1")), by + dy * len * j1 + ny * wid * (0.2 + r("k1"))],
          [bx + dx * len, by + dy * len],
          [bx + dx * len * j2 - nx * wid * (0.6 + r("k2")), by + dy * len * j2 - ny * wid * (0.6 + r("k2"))],
          [bx - nx * wid * 0.3, by - ny * wid * 0.3],
        ];
        const fill = colors[Math.floor(r("c") * colors.length)];
        return (
          <polygon
            key={i}
            points={pts.map((p) => p.join(",")).join(" ")}
            fill={fill}
            stroke={fill === "#fff" ? C.ink : "none"}
            strokeWidth={2}
          />
        );
      })}
    </svg>
  );
};

// ── Cables: curvas finas que se enredan en un punto ────────────────────────────

const smoothPath = (pts: [number, number][]) => {
  let d = `M ${pts[0][0]} ${pts[0][1]}`;
  for (let i = 0; i < pts.length - 1; i++) {
    const p0 = pts[i - 1] ?? pts[i];
    const p1 = pts[i];
    const p2 = pts[i + 1];
    const p3 = pts[i + 2] ?? p2;
    const c1 = [p1[0] + (p2[0] - p0[0]) / 6, p1[1] + (p2[1] - p0[1]) / 6];
    const c2 = [p2[0] - (p3[0] - p1[0]) / 6, p2[1] - (p3[1] - p1[1]) / 6];
    d += ` C ${c1[0]} ${c1[1]}, ${c2[0]} ${c2[1]}, ${p2[0]} ${p2[1]}`;
  }
  return d;
};

export const Wires: React.FC<{
  seed: string | number;
  progress: number;        // 0..1 cuánto se ha dibujado
  cx?: number;
  cy?: number;
  count?: number;
  rot?: number;
  color?: string;
  width?: number;
}> = ({ seed, progress, cx = W / 2, cy = H / 2, count = 18, rot = 0, color = C.ink, width = 1.6 }) => (
  <svg width={W} height={H} style={{ position: "absolute", inset: 0 }}>
    <g transform={`rotate(${rot} ${cx} ${cy})`}>
      {Array.from({ length: count }, (_, i) => {
        const r = (k: string) => random(`${seed}-w${i}-${k}`);
        const edge = (k: string): [number, number] => {
          const a = r(k) * Math.PI * 2;
          return [cx + Math.cos(a) * 1500, cy + Math.sin(a) * 1500];
        };
        const loops: [number, number][] = Array.from({ length: 4 }, (_, j) => {
          const a = r(`la${j}`) * Math.PI * 2;
          const rad = 40 + r(`lr${j}`) * 320;
          return [cx + Math.cos(a) * rad, cy + Math.sin(a) * rad * 1.2];
        });
        const d = smoothPath([edge("s"), ...loops, edge("e")]);
        return (
          <path
            key={i}
            d={d}
            fill="none"
            stroke={color}
            strokeWidth={width * (0.5 + r("sw"))}
            pathLength={1}
            strokeDasharray={1}
            strokeDashoffset={1 - Math.min(1, Math.max(0, progress * 1.4 - r("delay") * 0.4))}
          />
        );
      })}
    </g>
  </svg>
);

// ── Bloques de cielo con borde escalonado ──────────────────────────────────────

export const Sky: React.FC<{ x: number; y: number; w: number; h: number; seed: string | number; step?: number }> = ({
  x,
  y,
  w,
  h,
  seed,
  step = 22,
}) => {
  const cols = Math.ceil(w / step);
  const top = Array.from({ length: cols + 1 }, (_, i) => Math.floor(random(`${seed}-t${Math.floor(i / 2)}`) * 3) * step);
  const bot = Array.from({ length: cols + 1 }, (_, i) => h - Math.floor(random(`${seed}-b${Math.floor(i / 3)}`) * 3) * step);
  const pts: string[] = [];
  top.forEach((t, i) => {
    const px = Math.min(w, i * step);
    pts.push(`${px}px ${t}px`, `${Math.min(w, px + step)}px ${t}px`);
  });
  [...bot].reverse().forEach((b, ri) => {
    const i = bot.length - 1 - ri;
    const px = Math.min(w, i * step);
    pts.push(`${Math.min(w, px + step)}px ${b}px`, `${px}px ${b}px`);
  });
  const c = (k: string) => random(`${seed}-cl-${k}`) * 100;
  return (
    <div
      style={{
        position: "absolute",
        left: x,
        top: y,
        width: w,
        height: h,
        clipPath: `polygon(${pts.join(",")})`,
        background: [
          `radial-gradient(ellipse 26% 14% at ${c("a")}% ${40 + c("b") * 0.5}%, rgba(255,255,255,.95), rgba(255,255,255,0) 70%)`,
          `radial-gradient(ellipse 18% 10% at ${c("c")}% ${50 + c("d") * 0.4}%, rgba(255,255,255,.85), rgba(255,255,255,0) 70%)`,
          `radial-gradient(ellipse 34% 12% at ${c("e")}% ${60 + c("f") * 0.3}%, rgba(255,255,255,.7), rgba(255,255,255,0) 70%)`,
          `linear-gradient(180deg, #1650F0, #3D9BFF 60%, #8CCBFF)`,
        ].join(","),
      }}
    />
  );
};

// ── Ventana de Windows 98 ──────────────────────────────────────────────────────

const bevel = {
  borderTop: "3px solid #fff",
  borderLeft: "3px solid #fff",
  borderRight: "3px solid #404040",
  borderBottom: "3px solid #404040",
};

export const Win98: React.FC<{ x: number; y: number; w?: number; title?: string; text: string; buttons?: string[] }> = ({
  x,
  y,
  w = 560,
  title = "Hakufu.exe",
  text,
  buttons = ["Seguir", "Cancelar"],
}) => (
  <div style={{ position: "absolute", left: x, top: y, width: w, background: C.grey, ...bevel, fontFamily: "Tahoma, Verdana, sans-serif" }}>
    <div
      style={{
        height: 44,
        background: `linear-gradient(90deg, ${C.navy}, #1084D0)`,
        color: "#fff",
        fontWeight: 700,
        fontSize: 24,
        display: "flex",
        alignItems: "center",
        padding: "0 8px",
        justifyContent: "space-between",
      }}
    >
      <span>{title}</span>
      <span style={{ display: "flex", gap: 4 }}>
        {["_", "□", "×"].map((s) => (
          <span key={s} style={{ width: 32, height: 28, background: C.grey, color: "#000", ...bevel, borderWidth: 2, fontSize: 20, lineHeight: "22px", textAlign: "center" }}>
            {s}
          </span>
        ))}
      </span>
    </div>
    <div style={{ padding: "26px 26px 22px", fontSize: 28, color: "#000" }}>{text}</div>
    <div style={{ display: "flex", justifyContent: "flex-end", gap: 16, padding: "0 22px 22px" }}>
      {buttons.map((b) => (
        <span key={b} style={{ minWidth: 150, padding: "8px 0", textAlign: "center", fontSize: 26, color: "#000", ...bevel }}>
          {b}
        </span>
      ))}
    </div>
  </div>
);

// ── Texto en franjas azules que se repite ──────────────────────────────────────

export const Bars: React.FC<{ x: number; y: number; text: string; lines: number; size?: number; shown?: number }> = ({
  x,
  y,
  text,
  lines,
  size = 40,
  shown = lines,
}) => (
  <div style={{ position: "absolute", left: x, top: y, fontFamily: MONO, fontSize: size, lineHeight: 1.12 }}>
    {Array.from({ length: Math.min(lines, shown) }, (_, i) => (
      <div key={i} style={{ marginLeft: i * size * 0.62, display: "inline-block", width: "max-content", background: C.blue, color: "#fff", padding: "0 10px", clear: "both", float: "left" }}>
        {text}
      </div>
    ))}
  </div>
);

// ── Rejilla, grano y rótulos pequeños ──────────────────────────────────────────

export const Grid: React.FC<{ color?: string; opacity?: number }> = ({ color = C.ink, opacity = 0.18 }) => {
  const xs = [96, 384, 672, 960, 1248, 1536, 1824];
  const ys = [60, 300, 540, 780, 1020];
  return (
    <svg width={W} height={H} style={{ position: "absolute", inset: 0, opacity }}>
      {xs.map((x) => (
        <line key={`x${x}`} x1={x} y1={0} x2={x} y2={H} stroke={color} strokeWidth={1.5} />
      ))}
      {ys.map((y) => (
        <line key={`y${y}`} x1={0} y1={y} x2={W} y2={y} stroke={color} strokeWidth={1.5} />
      ))}
    </svg>
  );
};

export const Grain: React.FC<{ frame: number; opacity?: number }> = ({ frame, opacity = 0.16 }) => (
  <svg width={W} height={H} style={{ position: "absolute", inset: 0, opacity, mixBlendMode: "multiply", pointerEvents: "none" }}>
    <filter id="grain">
      <feTurbulence type="fractalNoise" baseFrequency={0.85} numOctaves={2} seed={frame % 24} />
      <feColorMatrix type="saturate" values="0" />
    </filter>
    <rect width={W} height={H} filter="url(#grain)" />
  </svg>
);

export const Cross: React.FC<{ x: number; y: number; size?: number; color?: string }> = ({ x, y, size = 46, color = C.ink }) => (
  <div style={{ position: "absolute", left: x - size / 2, top: y - size / 2, width: size, height: size }}>
    <div style={{ position: "absolute", left: size / 2 - 2.5, top: 0, width: 5, height: size, background: color }} />
    <div style={{ position: "absolute", top: size / 2 - 2.5, left: 0, height: 5, width: size, background: color }} />
  </div>
);

export const Timecode: React.FC<{ frame: number; x: number; y: number }> = ({ frame, x, y }) => {
  const s = Math.floor(frame / 30);
  const ff = frame % 30;
  const p = (n: number) => String(n).padStart(2, "0");
  return (
    <div style={{ position: "absolute", left: x, top: y, background: C.ink, color: "#fff", fontFamily: MONO, fontSize: 30, padding: "4px 12px" }}>
      19:37:{p(37 + s)}:{p(ff)}
    </div>
  );
};

export const VText: React.FC<{ x: number; y: number; text: string; size?: number; color?: string; boxed?: boolean }> = ({
  x,
  y,
  text,
  size = 30,
  color = C.ink,
  boxed = true,
}) => (
  <div
    style={{
      position: "absolute",
      left: x,
      top: y,
      writingMode: "vertical-rl",
      fontFamily: JP,
      fontSize: size,
      color,
      letterSpacing: size * 0.12,
      padding: boxed ? "10px 4px" : 0,
      border: boxed ? `2px solid ${color}` : "none",
      background: boxed ? "rgba(255,255,255,.9)" : "none",
      whiteSpace: "nowrap",
    }}
  >
    {text}
  </div>
);

// ── Líneas finas: ráfagas de rayas de 1–3 px que cruzan la pantalla ────────────

export const ThinLines: React.FC<{
  seed: string | number;
  amount: number;          // 0..1, cuántas y cuánto se alargan
  count?: number;
  colors?: string[];
  angle?: number;          // grados; las rayas van casi paralelas a este ángulo
  spread?: number;
}> = ({ seed, amount, count = 60, colors = [C.ink, C.blue, C.red, C.ink, C.sky], angle = -18, spread = 14 }) => {
  if (amount < 0.03) return null;
  const n = Math.round(count * Math.min(1, amount * 1.3));
  return (
    <svg width={W} height={H} style={{ position: "absolute", inset: 0, pointerEvents: "none" }}>
      {Array.from({ length: n }, (_, i) => {
        const r = (k: string) => random(`${seed}-l${i}-${k}`);
        const a = ((angle + (r("a") - 0.5) * spread) * Math.PI) / 180;
        const cx = r("x") * W;
        const cy = r("y") * H;
        const len = (200 + r("len") * 1600) * amount;
        const dx = (Math.cos(a) * len) / 2;
        const dy = (Math.sin(a) * len) / 2;
        return (
          <line
            key={i}
            x1={cx - dx}
            y1={cy - dy}
            x2={cx + dx}
            y2={cy + dy}
            stroke={colors[Math.floor(r("c") * colors.length)]}
            strokeWidth={r("w") < 0.75 ? 1 : r("w") < 0.95 ? 2 : 3}
          />
        );
      })}
    </svg>
  );
};

// Líneas de velocidad de manga, finas, desde un punto.
export const Burst: React.FC<{ seed: string | number; amount: number; cx?: number; cy?: number; count?: number; color?: string }> = ({
  seed,
  amount,
  cx = W / 2,
  cy = H / 2,
  count = 140,
  color = C.ink,
}) => {
  if (amount < 0.03) return null;
  return (
    <svg width={W} height={H} style={{ position: "absolute", inset: 0, pointerEvents: "none" }}>
      {Array.from({ length: count }, (_, i) => {
        const r = (k: string) => random(`${seed}-b${i}-${k}`);
        const a = r("a") * Math.PI * 2;
        const r0 = 160 + r("r0") * 260 + (1 - amount) * 500;
        const r1 = r0 + 300 + r("r1") * 1200;
        return (
          <line
            key={i}
            x1={cx + Math.cos(a) * r0}
            y1={cy + Math.sin(a) * r0}
            x2={cx + Math.cos(a) * r1}
            y2={cy + Math.sin(a) * r1}
            stroke={i % 9 === 0 ? C.blue : i % 13 === 0 ? C.red : color}
            strokeWidth={r("w") < 0.8 ? 1 : 2}
            opacity={amount}
          />
        );
      })}
    </svg>
  );
};
