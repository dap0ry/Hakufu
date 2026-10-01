import React from "react";
import { AbsoluteFill, Easing, Img, interpolate, Sequence, useCurrentFrame, useVideoConfig } from "remotion";
import type { Assets } from "../assets";
import { easeOut, tween } from "../ease";
import { Reveal } from "../text/Reveal";

// 24–31 s: cortes al ritmo (1 tiempo = 18 fotogramas a 100 BPM).
export const MONTAGE = { profile: 0, theme: 72, offline: 126, platforms: 162, end: 216 };

const Card: React.FC<{ children: React.ReactNode; duration: number }> = ({ children, duration }) => {
  const frame = useCurrentFrame();
  const inT = interpolate(frame, [0, 10], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: easeOut });
  const outT = interpolate(frame, [duration - 6, duration], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return <AbsoluteFill style={{ opacity: Math.min(inT, outT), scale: `${1.04 - 0.04 * inT}` }}>{children}</AbsoluteFill>;
};

const Profile: React.FC<{ assets: Assets }> = ({ assets }) => {
  const frame = useCurrentFrame();
  // La tarjeta de perfil flota y gira un poco, recortada de la captura de la app.
  const crop = assets.appSize.w === 1920 ? { x: 380, y: 320, w: 1160, h: 640 } : { x: 70, y: 110, w: 1140, h: 640 };
  return (
    <AbsoluteFill style={{ background: "radial-gradient(ellipse at 50% 45%, #1d1d20 0%, #050505 70%)", perspective: 1800 }}>
      <div
        style={{
          position: "absolute", left: "50%", top: "47%", width: crop.w, height: crop.h, translate: "-50% -50%",
          overflow: "hidden", borderRadius: 24,
          transform: `rotateY(${tween(frame, [0, 72], [-16, 6], Easing.bezier(0.33, 0, 0.2, 1))}deg) rotateX(${tween(frame, [0, 72], [8, -2])}deg)`,
          scale: `${tween(frame, [0, 72], [1.12, 1.3])}`,
          boxShadow: "0 60px 120px rgba(0,0,0,0.8)",
        }}
      >
        <Img src={assets.app.profile} style={{ position: "absolute", left: -crop.x, top: -crop.y, width: assets.appSize.w, height: assets.appSize.h }} />
        {/* brillo que recorre la tarjeta */}
        <div
          style={{
            position: "absolute", inset: 0,
            background: `linear-gradient(110deg, rgba(255,255,255,0) ${tween(frame, [8, 60], [-30, 130]) - 12}%, rgba(255,255,255,0.12) ${tween(frame, [8, 60], [-30, 130])}%, rgba(255,255,255,0) ${tween(frame, [8, 60], [-30, 130]) + 12}%)`,
          }}
        />
      </div>
      <div style={{ position: "absolute", left: 0, right: 0, bottom: 70 }}>
        <Reveal text="Tu perfil de lector." from={6} size={78} />
      </div>
    </AbsoluteFill>
  );
};

const Theme: React.FC<{ assets: Assets }> = ({ assets }) => {
  const frame = useCurrentFrame();
  // Barrido de oscuro a claro con una línea de luz en el corte.
  const split = interpolate(frame, [8, 40], [0, 100], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: Easing.bezier(0.65, 0, 0.35, 1) });
  return (
    <AbsoluteFill style={{ background: "#000" }}>
      <AbsoluteFill style={{ padding: 90 }}>
        <div style={{ position: "relative", width: "100%", height: "100%", borderRadius: 22, overflow: "hidden", boxShadow: "0 40px 100px rgba(0,0,0,0.8)" }}>
          <Img src={assets.app.collection} style={{ position: "absolute", inset: 0, width: "100%", height: "100%", objectFit: "cover" }} />
          <Img
            src={assets.app.collectionLight}
            style={{ position: "absolute", inset: 0, width: "100%", height: "100%", objectFit: "cover", clipPath: `inset(0 0 0 ${100 - split}%)` }}
          />
          <div style={{ position: "absolute", top: 0, bottom: 0, left: `${100 - split}%`, width: 3, background: "white", boxShadow: "0 0 30px 6px rgba(255,255,255,0.6)", opacity: split > 0 && split < 100 ? 1 : 0 }} />
        </div>
      </AbsoluteFill>
      <div style={{ position: "absolute", left: 0, right: 0, bottom: 14 }}>
        <Reveal text="Claro u oscuro." from={4} size={64} />
      </div>
    </AbsoluteFill>
  );
};

const Words: React.FC<{ lines: { text: string; at: number }[] }> = ({ lines }) => (
  <AbsoluteFill style={{ background: "#000", justifyContent: "center", alignItems: "center", flexDirection: "column", gap: 10 }}>
    {lines.map((l) => (
      <Reveal key={l.text} text={l.text} from={l.at} size={150} stagger={0} />
    ))}
  </AbsoluteFill>
);

export const Montage: React.FC<{ assets: Assets }> = ({ assets }) => {
  const { fps } = useVideoConfig();
  return (
    <AbsoluteFill style={{ background: "#000" }}>
      <Sequence name="Perfil" from={MONTAGE.profile} durationInFrames={MONTAGE.theme - MONTAGE.profile} premountFor={fps}>
        <Card duration={MONTAGE.theme - MONTAGE.profile}><Profile assets={assets} /></Card>
      </Sequence>
      <Sequence name="Tema" from={MONTAGE.theme} durationInFrames={MONTAGE.offline - MONTAGE.theme} premountFor={fps}>
        <Card duration={MONTAGE.offline - MONTAGE.theme}><Theme assets={assets} /></Card>
      </Sequence>
      <Sequence name="Sin internet" from={MONTAGE.offline} durationInFrames={MONTAGE.platforms - MONTAGE.offline} premountFor={fps}>
        <Card duration={MONTAGE.platforms - MONTAGE.offline}>
          <Words lines={[{ text: "Sin internet.", at: 0 }, { text: "Sin cuentas.", at: 18 }]} />
        </Card>
      </Sequence>
      <Sequence name="Sistemas" from={MONTAGE.platforms} durationInFrames={MONTAGE.end - MONTAGE.platforms} premountFor={fps}>
        <Card duration={MONTAGE.end - MONTAGE.platforms}>
          <Words lines={[{ text: "Windows.", at: 0 }, { text: "macOS.", at: 18 }, { text: "Linux.", at: 36 }]} />
        </Card>
      </Sequence>
    </AbsoluteFill>
  );
};
