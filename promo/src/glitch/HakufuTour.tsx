import React from "react";
import { Audio } from "@remotion/media";
import { AbsoluteFill, Easing, Img, interpolate, random, staticFile, useCurrentFrame } from "remotion";
import { display, inter } from "../fonts";
import { Burst, C, env, Glitch, Grain, Grid, H, hitIndex, hitsBetween, img, JP, MONO, Shot, ThinLines, Timecode, VText, W, Wires } from "./fx";

// Vídeo de 20 s, 1920×1080, glitch + breakcore, que explica la app:
// portátil que se abre → Inicio → biblioteca → colección → perfil → lector a
// una y dos páginas → se cierra → HAKUFU · Windows Mac Linux → se aleja.
// Cada sección dura compases enteros del breakcore (1 compás = 40 frames).
export const TOUR_DURATION = 600;

const T = {
  laptop: 0,
  home: 80,
  library: 120,
  collection: 200,
  profile: 280,
  stats: 320,
  reader1: 360,
  reader2: 400,
  montage: 480,
  close: 520,
  end: 560,
};

// Donde se ve la app. El último fotograma del portátil deja la pantalla justo aquí.
const FR = { x: 192, y: 60, w: 1536, h: 960 };
const APP = { w: 1728, h: 1080 };

const tour = (name: string) => staticFile(`tour/${name}.png`);
const pad = (n: number, l: number) => String(n).padStart(l, "0");
const libScroll = (i: number) => staticFile(`tour/library-scroll/${pad(i, 3)}.png`);
const colScroll = (i: number) => staticFile(`tour/collection-scroll/${pad(i, 3)}.png`);
const laptop = (i: number) => staticFile(`laptop/${pad(i, 4)}.png`);
const laptopOff = (i: number) => staticFile(`laptop-off/${pad(i, 4)}.png`);

const lerp = (f: number, a: number, b: number, from = 0, to = 1, ease = Easing.linear) =>
  interpolate(f, [a, b], [from, to], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: ease });

// ── Piezas comunes ─────────────────────────────────────────────────────────────

const FrameLines: React.FC<{ color?: string; opacity?: number }> = ({ color = C.ink, opacity = 1 }) => (
  <svg width={W} height={H} style={{ position: "absolute", inset: 0, opacity }}>
    {[FR.x, FR.x + FR.w].map((x) => (
      <line key={x} x1={x} y1={0} x2={x} y2={H} stroke={color} strokeWidth={1} />
    ))}
    {[FR.y, FR.y + FR.h].map((y) => (
      <line key={y} x1={0} y1={y} x2={W} y2={y} stroke={color} strokeWidth={1} />
    ))}
  </svg>
);

const Hud: React.FC<{ f: number; step: string; jp: string; color?: string }> = ({ f, step, jp, color = C.ink }) => (
  <>
    <div style={{ position: "absolute", left: FR.x, top: 22, fontFamily: MONO, fontSize: 18, color, letterSpacing: 1 }}>
      HKF / {step}
    </div>
    <div style={{ position: "absolute", left: FR.x + FR.w / 2 - 150, top: 22, width: 300, textAlign: "center", fontFamily: inter, fontStyle: "italic", fontSize: 20, color }}>
      hakufu by dap0ry
    </div>
    <VText x={FR.x - 62} y={FR.y + 30} text={jp} size={22} color={color} />
    <div style={{ position: "absolute", left: FR.x + FR.w + 14, top: FR.y + 30, fontFamily: MONO, fontSize: 14, color, lineHeight: 1.5 }}>
      v0.10
      <br />
      .cbz
      <br />
      .cbr
      <br />
      .pdf
    </div>
    <div style={{ position: "absolute", left: FR.x + FR.w + 14, top: FR.y + FR.h - 90, fontFamily: JP, fontSize: 14, color, writingMode: "vertical-rl" }}>スタイル-ハクフ</div>
    <div style={{ position: "absolute", left: FR.x + FR.w - 210, top: FR.y + FR.h + 14, transform: "scale(.75)", transformOrigin: "left top" }}>
      <Timecode frame={f} x={0} y={0} />
    </div>
  </>
);

// Rótulo que explica cada parte: grande + una línea pequeña debajo.
const Caption: React.FC<{ f: number; from: number; title: string; sub?: string; x?: number; y?: number }> = ({ f, from, title, sub, x = 120, y = 790 }) => {
  const local = f - from;
  // entra a trozos: primero una franja, luego el texto con un salto
  const shown = Math.min(title.length, Math.max(0, Math.floor(local * 1.6)));
  const jitter = local < 6 ? (random(`cap${from}-${local}`) - 0.5) * 30 : 0;
  return (
    <div style={{ position: "absolute", left: x + jitter, top: y }}>
      <div style={{ display: "inline-block", background: C.paper, padding: "8px 20px 4px", borderLeft: `6px solid ${C.blue}` }}>
        <div style={{ fontFamily: display, fontSize: 104, lineHeight: 0.95, color: C.ink, letterSpacing: -1, whiteSpace: "nowrap" }}>
          {title.slice(0, shown)}
          {shown < title.length && <span style={{ color: C.blue }}>█</span>}
        </div>
      </div>
      {sub && local > 5 && (
        <div style={{ marginTop: 6 }}>
          <span style={{ fontFamily: MONO, fontSize: 24, color: "#fff", background: C.ink, padding: "4px 12px" }}>{sub}</span>
        </div>
      )}
    </div>
  );
};

// La app dentro del marco, con zoom hacia un punto (en píxeles de la captura).
const AppFrame: React.FC<{
  f: number;
  src: string;
  zoom?: number;
  at?: [number, number];
  glitch?: number;
  wipe?: { prev: string; p: number };   // pasar página: la nueva entra de derecha a izquierda
}> = ({ f, src, zoom = 1, at = [APP.w / 2, APP.h / 2], glitch = 1, wipe }) => {
  const hit = env(f, ["snare", "stutter"], 2.5) * glitch;
  const idx = hitIndex(f);
  const ox = (at[0] / APP.w) * 100;
  const oy = (at[1] / APP.h) * 100;
  const layer = (s: string) => (
    <AbsoluteFill style={{ transform: `scale(${zoom})`, transformOrigin: `${ox}% ${oy}%` }}>
      <Shot src={s} w={FR.w} h={FR.h} iw={APP.w} ih={APP.h} />
    </AbsoluteFill>
  );
  return (
    <div style={{ position: "absolute", left: FR.x, top: FR.y, width: FR.w, height: FR.h, overflow: "hidden", background: C.ink }}>
      <Glitch amount={hit > 0.4 ? hit * 0.7 : 0} rgb={env(f, ["kick", "stutter"], 2.5) * 14 * glitch} seed={`a${idx}`}>
        {wipe ? (
          <>
            {layer(wipe.prev)}
            <AbsoluteFill style={{ clipPath: `inset(0 0 0 ${(1 - wipe.p) * 100}%)` }}>{layer(src)}</AbsoluteFill>
            {wipe.p < 1 && (
              <div style={{ position: "absolute", top: 0, bottom: 0, left: `${(1 - wipe.p) * 100}%`, width: 2, background: C.sky, boxShadow: `0 0 0 1px ${C.ink}` }} />
            )}
          </>
        ) : (
          layer(src)
        )}
      </Glitch>
    </div>
  );
};

// Fondo común: papel, rejilla fina y rayas que saltan con la batería.
const Paper: React.FC<{ f: number; lines?: number; children?: React.ReactNode }> = ({ f, lines = 1, children }) => {
  const ki = hitIndex(f, ["kick", "snare", "stutter", "roll"]);
  return (
    <AbsoluteFill style={{ background: C.paper }}>
      <Grid opacity={0.1} />
      <ThinLines seed={`bg${ki}`} amount={env(f, ["kick", "snare", "stutter", "roll"], 3) * lines} count={50} angle={random(`ang${ki}`) > 0.5 ? -14 : 8} />
      {children}
    </AbsoluteFill>
  );
};

// ── 1. El portátil se abre ─────────────────────────────────────────────────────

const Laptop: React.FC<{ f: number }> = ({ f }) => {
  const typed = "hakufu — lector de manga para tu ordenador".slice(0, Math.max(0, Math.floor((f - 8) * 1.2)));
  const roll = env(f, ["roll"], 3);
  return (
    <Paper f={f} lines={1.4}>
      <Wires seed="lap" progress={lerp(f, 0, 70, 0, 1, Easing.out(Easing.cubic))} cx={960} cy={560} count={14} width={0.9} rot={f * 0.3} color="rgba(11,11,12,.55)" />
      <FrameLines opacity={lerp(f, 50, 79, 0, 1)} />
      <Img src={laptop(Math.min(79, f))} style={{ position: "absolute", inset: 0, width: W, height: H }} />
      <Burst seed={`lb${hitIndex(f, ["roll"])}`} amount={roll * 0.8} cx={960} cy={540} count={90} />
      <div style={{ position: "absolute", left: 96, top: 960, fontFamily: MONO, fontSize: 22, color: C.ink, opacity: lerp(f, 60, 72, 1, 0) }}>
        {typed}
        <span style={{ opacity: f % 10 < 5 ? 1 : 0 }}>_</span>
      </div>
      <div style={{ position: "absolute", left: 96, top: 40, fontFamily: MONO, fontSize: 18, color: C.ink, opacity: lerp(f, 60, 72, 1, 0) }}>
        HKF / 00 ENCENDIDO
      </div>
      <VText x={1780} y={120} text="ハクフ・漫画リーダー" size={22} boxed={false} />
    </Paper>
  );
};

// ── 2. Inicio ──────────────────────────────────────────────────────────────────

const Home: React.FC<{ f: number }> = ({ f }) => (
  <Paper f={f}>
    <FrameLines />
    <AppFrame f={f} src={tour("home")} zoom={lerp(f, T.home, T.library, 1, 1.06)} at={[300, 400]} />
    <Hud f={f} step="01 INICIO" jp="ホーム・ハクフ" />
    <Caption f={f} from={T.home} title="ESTO ES HAKUFU" sub="tu lector de manga · Windows, Mac y Linux" />
  </Paper>
);

// ── 3. Biblioteca: muchas colecciones y bajar ──────────────────────────────────

const KUROGANE_IN_LIBRARY: [number, number] = [372, 470];

const Library: React.FC<{ f: number }> = ({ f }) => {
  const local = f - T.library;
  const src = local < 18 ? tour("library") : libScroll(Math.round(lerp(local, 18, 66, 0, 59)));
  const dive = lerp(local, 70, 80, 1, 2.6, Easing.in(Easing.cubic));
  return (
    <Paper f={f}>
      <FrameLines />
      <AppFrame f={f} src={src} zoom={dive} at={KUROGANE_IN_LIBRARY} glitch={local > 70 ? 2 : 1} />
      <Hud f={f} step="02 BIBLIOTECA" jp="ライブラリ" />
      <Caption f={f} from={T.library} title="METE TODOS TUS MANGAS" sub="18 colecciones · 82 tomos · .cbz .cbr .pdf" />
    </Paper>
  );
};

// ── 4. Colección con muchos tomos ──────────────────────────────────────────────

const Collection: React.FC<{ f: number }> = ({ f }) => {
  const local = f - T.collection;
  const src = local < 14 ? tour("collection") : colScroll(Math.round(lerp(local, 14, 70, 0, 59)));
  return (
    <Paper f={f}>
      <FrameLines />
      <AppFrame f={f} src={src} zoom={lerp(local, 0, 8, 1.25, 1, Easing.out(Easing.cubic))} at={[300, 300]} />
      <Hud f={f} step="03 COLECCIÓN" jp="コレクション" />
      <Caption f={f} from={T.collection} title="POR COLECCIONES" sub="una carpeta = una colección · Kurogane, 24 tomos" />
    </Paper>
  );
};

// ── 5. Perfil: horas leídas ────────────────────────────────────────────────────

const Profile: React.FC<{ f: number }> = ({ f }) => {
  const stats = f >= T.stats;
  const zoom = stats ? lerp(f, T.stats, T.reader1, 2.1, 2.6, Easing.out(Easing.quad)) : lerp(f, T.profile, T.stats, 1, 1.25);
  const at: [number, number] = stats ? [390, 640] : [860, 600];
  return (
    <Paper f={f} lines={stats ? 0.5 : 1}>
      <FrameLines />
      <AppFrame f={f} src={tour("profile")} zoom={zoom} at={at} glitch={stats ? 0.4 : 1} />
      {stats && <Burst seed="st" amount={lerp(f, T.stats, T.stats + 6, 1, 0.25)} cx={FR.x + FR.w * 0.35} cy={FR.y + FR.h * 0.55} count={70} />}
      <Hud f={f} step="04 PERFIL" jp="プロフィール" />
      {stats ? (
        <Caption f={f} from={T.stats} title="74 H LEYENDO" sub="Hakufu apunta cuánto lees cada día" x={1060} y={780} />
      ) : (
        <Caption f={f} from={T.profile} title="TU PERFIL" sub="horas, páginas, racha y favoritos · se exporta a PNG" />
      )}
    </Paper>
  );
};

// ── 6. Lector a una y a dos páginas ────────────────────────────────────────────

// Cada pase de página: índice de la página y frame en que empieza el pase.
const turns = (f: number, from: number, at: number[], pages: number) => {
  const done = at.filter((h) => h >= from && h <= f);
  const n = done.length;
  const last = done[n - 1] ?? -99;
  return { page: n % pages, prev: (n - 1 + pages) % pages, p: lerp(f, last, last + 4, 0, 1, Easing.out(Easing.quad)), n };
};

const Reader: React.FC<{ f: number }> = ({ f }) => {
  const two = f >= T.reader2;
  let src: string;
  let wipe: { prev: string; p: number } | undefined;
  if (!two) {
    const t = turns(f, T.reader1 + 1, hitsBetween(T.reader1, T.reader2, ["roll"]).filter((_, i) => i % 2 === 0), 5);
    src = tour(`reader-1p-${t.page}`);
    if (t.n > 0) wipe = { prev: tour(`reader-1p-${t.prev}`), p: t.p };
  } else {
    const beats = Array.from({ length: 8 }, (_, i) => T.reader2 + 10 + i * 10);
    const t = turns(f, T.reader2 + 1, beats, 3);
    src = tour(`reader-2p-${t.page}`);
    if (t.n > 0) wipe = { prev: tour(`reader-2p-${t.prev}`), p: t.p };
  }
  return (
    <Paper f={f}>
      <FrameLines />
      <AppFrame f={f} src={src} wipe={wipe} glitch={two ? 1 : 0.6} />
      <Hud f={f} step={two ? "06 DOS PÁGINAS" : "05 UNA PÁGINA"} jp="リーダー・読む" />
      {two ? (
        <Caption f={f} from={T.reader2} title="O A DOBLE PÁGINA" sub="como un tomo de verdad, con giro 3D al pasar" />
      ) : (
        <Caption f={f} from={T.reader1} title="LEE A UNA PÁGINA" sub="pasa con ← → · modo zen a pantalla completa" />
      )}
    </Paper>
  );
};

// ── 7. Montaje rápido ──────────────────────────────────────────────────────────

const MONTAGE = [
  () => tour("home"),
  () => tour("library"),
  () => libScroll(40),
  () => tour("collection"),
  () => colScroll(59),
  () => tour("profile"),
  () => tour("reader-1p-2"),
  () => tour("reader-2p-1"),
];

const Montage: React.FC<{ f: number }> = ({ f }) => {
  const cut = hitIndex(f, ["kick", "snare", "stutter"]);
  const src = MONTAGE[Math.floor(random(`mc${cut}`) * MONTAGE.length)]();
  const zoom = 1 + random(`mz${cut}`) * 0.8;
  const at: [number, number] = [random(`mx${cut}`) * APP.w, random(`my${cut}`) * APP.h];
  const invert = env(f, ["snare"], 2) > 0.5 && random(`mi${hitIndex(f, ["snare"])}`) > 0.5;
  return (
    <AbsoluteFill style={{ filter: invert ? "invert(1)" : undefined }}>
      <Paper f={f} lines={2}>
        <FrameLines />
        <AppFrame f={f} src={src} zoom={zoom} at={at} glitch={1.6} />
        <ThinLines seed={`mt${cut}`} amount={1} count={30} angle={random(`ma${cut}`) * 180} spread={6} />
        <Hud f={f} step="07 OFFLINE" jp="ハクフ・漫画リーダー" />
        <Caption f={f} from={T.montage} title="SIN CUENTAS. SIN INTERNET." sub="todo se queda en tu ordenador" />
      </Paper>
    </AbsoluteFill>
  );
};

// ── 8. Se cierra la app (apagado de tubo) y luego la tapa ──────────────────────

const Close: React.FC<{ f: number }> = ({ f }) => {
  const local = f - T.close;
  if (local < 12) {
    const sy = lerp(local, 0, 6, 1, 0.004, Easing.in(Easing.cubic));
    const sx = lerp(local, 6, 10, 1, 0, Easing.in(Easing.cubic));
    return (
      <Paper f={f}>
        <FrameLines />
        <div style={{ position: "absolute", left: FR.x, top: FR.y, width: FR.w, height: FR.h, background: C.ink }}>
          <div style={{ position: "absolute", inset: 0, transform: `scale(${sx}, ${sy})`, filter: local > 4 ? "brightness(3)" : undefined }}>
            <Shot src={tour("reader-2p-2")} w={FR.w} h={FR.h} iw={APP.w} ih={APP.h} />
          </div>
        </div>
        <Hud f={f} step="08 CERRAR" jp="おわり" />
      </Paper>
    );
  }
  // tapa cerrándose: el render del portátil al revés, con la pantalla apagada
  // del pase con la pantalla apagada solo hay uno de cada tres fotogramas (0, 3 … 78)
  const i = Math.round(lerp(local, 12, 40, 78, 0, Easing.inOut(Easing.cubic)) / 3) * 3;
  return (
    <Paper f={f} lines={0.6}>
      <FrameLines opacity={lerp(local, 12, 24, 1, 0)} />
      <Img src={laptopOff(i)} style={{ position: "absolute", inset: 0, width: W, height: H }} />
    </Paper>
  );
};

// ── 9. Final: HAKUFU · Windows Mac Linux, golpe y se aleja ─────────────────────

const End: React.FC<{ f: number }> = ({ f }) => {
  const local = f - T.end;
  const flash = local < 2;
  const away = lerp(local, 14, 40, 1, 0.42, Easing.inOut(Easing.cubic));
  const reader = lerp(local, 16, 30, 0, 1);
  const shake = local < 8 ? (random(`es${local}`) - 0.5) * 24 * (1 - local / 8) : 0;
  return (
    <AbsoluteFill style={{ background: flash ? C.ink : C.paper }}>
      <Grid opacity={0.12} />
      <Burst seed="end" amount={lerp(local, 0, 10, 1, 0.3)} count={160} color={flash ? "#fff" : C.ink} />
      {/* MANGA READER detrás, aparece al alejarse */}
      <div
        style={{
          position: "absolute",
          inset: 0,
          display: "flex",
          alignItems: "flex-end",
          justifyContent: "center",
          paddingBottom: 150,
          fontFamily: display,
          fontSize: 220,
          letterSpacing: 6,
          color: "transparent",
          WebkitTextStroke: `2px ${C.ink}`,
          opacity: reader,
          transform: `scale(${1.25 - reader * 0.25})`,
          whiteSpace: "nowrap",
        }}
      >
        MANGA READER
      </div>
      <AbsoluteFill style={{ transform: `translate(${shake}px, ${(1 - away) * -330}px) scale(${away})` }}>
        <AbsoluteFill style={{ background: C.paper, clipPath: `inset(${FR.y}px ${W - FR.x - FR.w}px ${H - FR.y - FR.h}px ${FR.x}px)` }} />
        <FrameLines />
        <Glitch amount={local < 5 ? 0.8 : 0} rgb={local < 5 ? 24 : 0} seed={`end${local}`}>
          <AbsoluteFill style={{ alignItems: "center", justifyContent: "center", flexDirection: "column" }}>
            <div style={{ display: "flex", alignItems: "center", gap: 40 }}>
              <Img src={img("logo.png")} style={{ width: 240, height: 240, filter: flash ? "none" : "invert(1)" }} />
              <div style={{ fontFamily: display, fontSize: 280, lineHeight: 0.9, color: flash ? "#fff" : C.ink }}>HAKUFU</div>
            </div>
            <div style={{ display: "flex", gap: 18, marginTop: 40 }}>
              {["WINDOWS", "MAC", "LINUX"].map((p, i) => (
                <span
                  key={p}
                  style={{
                    fontFamily: MONO,
                    fontSize: 40,
                    color: "#fff",
                    background: C.blue,
                    padding: "6px 22px",
                    opacity: local >= i * 2 ? 1 : 0,
                  }}
                >
                  {p}
                </span>
              ))}
            </div>
            <div style={{ marginTop: 30, fontFamily: MONO, fontSize: 28, color: C.ink }}>gratis · hakufuweb.vercel.app</div>
          </AbsoluteFill>
        </Glitch>
      </AbsoluteFill>
      <ThinLines seed="endl" amount={lerp(local, 14, 40, 0, 0.8)} count={70} angle={0} spread={4} />
    </AbsoluteFill>
  );
};

export const HakufuTour: React.FC = () => {
  const f = useCurrentFrame();
  let scene: React.ReactNode;
  if (f < T.home) scene = <Laptop f={f} />;
  else if (f < T.library) scene = <Home f={f} />;
  else if (f < T.collection) scene = <Library f={f} />;
  else if (f < T.profile) scene = <Collection f={f} />;
  else if (f < T.reader1) scene = <Profile f={f} />;
  else if (f < T.montage) scene = <Reader f={f} />;
  else if (f < T.close) scene = <Montage f={f} />;
  else if (f < T.end) scene = <Close f={f} />;
  else scene = <End f={f} />;
  return (
    <AbsoluteFill style={{ background: C.paper, overflow: "hidden" }}>
      {scene}
      <Grain frame={f} opacity={0.1} />
      <Audio src={img("breakcore.wav")} />
    </AbsoluteFill>
  );
};
