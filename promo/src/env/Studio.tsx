import React from "react";
import { AbsoluteFill, Img, interpolate, useCurrentFrame } from "remotion";

// Estudio de producto: fondo grafito, foco cenital con haz de luz, suelo que
// refleja, y portadas flotando a distintas profundidades (desenfocadas según lo
// lejos que estén), como los anuncios de producto.

type Floater = { src: string; x: number; y: number; z: number; rot: number; w: number };

export const Studio: React.FC<{
  covers: string[];
  light?: number;          // 0..1 encendido del foco
  drift?: number;          // desplazamiento lateral del fondo (parallax), en px
}> = ({ covers, light = 1, drift = 0 }) => {
  const frame = useCurrentFrame();
  const floaters: Floater[] = [
    { src: covers[0], x: -720, y: -170, z: -900, rot: -14, w: 300 },
    { src: covers[1], x: 760, y: -230, z: -1100, rot: 11, w: 320 },
    { src: covers[2], x: -470, y: 120, z: -1500, rot: 8, w: 260 },
    { src: covers[1], x: 420, y: 160, z: -1700, rot: -9, w: 240 },
    { src: covers[0], x: 120, y: -330, z: -2100, rot: 4, w: 220 },
  ];
  return (
    <AbsoluteFill style={{ background: "#050505", overflow: "hidden" }}>
      {/* Pared: degradado grafito con el foco */}
      <AbsoluteFill
        style={{
          background: `radial-gradient(ellipse 55% 60% at 50% 38%, rgba(70,72,78,${0.9 * light}) 0%, rgba(24,25,28,${light}) 45%, #060607 100%)`,
        }}
      />
      {/* Portadas flotando al fondo, con profundidad de campo */}
      <AbsoluteFill style={{ perspective: 1400 }}>
        {floaters.map((f, i) => {
          const bob = Math.sin((frame + i * 37) / 70) * 14;
          const blur = Math.min(14, Math.abs(f.z) / 140);
          return (
            <Img
              key={i}
              src={f.src}
              style={{
                position: "absolute",
                left: `calc(50% + ${f.x + drift * (1 + i * 0.15)}px)`,
                top: `calc(45% + ${f.y + bob}px)`,
                width: f.w,
                translate: `-50% -50%`,
                transform: `translateZ(${f.z}px) rotate(${f.rot}deg) rotateY(${f.rot * 1.4}deg)`,
                filter: `blur(${blur}px) brightness(${0.55 * light})`,
                borderRadius: 6,
                boxShadow: "0 30px 60px rgba(0,0,0,0.6)",
              }}
            />
          );
        })}
      </AbsoluteFill>
      {/* Haz de luz volumétrico */}
      <AbsoluteFill
        style={{
          background: `conic-gradient(from 180deg at 50% -10%, rgba(255,255,255,0) 168deg, rgba(255,255,255,${0.07 * light}) 180deg, rgba(255,255,255,0) 192deg)`,
          filter: "blur(30px)",
          opacity: interpolate(Math.sin(frame / 40), [-1, 1], [0.75, 1]),
        }}
      />
      {/* Suelo: horizonte suave + charco de luz */}
      <div
        style={{
          position: "absolute", left: 0, right: 0, top: "64%", bottom: 0,
          background: `linear-gradient(180deg, rgba(0,0,0,0) 0%, rgba(0,0,0,0.55) 30%, #030303 100%)`,
        }}
      />
      <div
        style={{
          position: "absolute", left: "15%", right: "15%", top: "58%", height: "30%",
          background: `radial-gradient(ellipse at 50% 30%, rgba(255,255,255,${0.09 * light}) 0%, rgba(255,255,255,0) 60%)`,
          filter: "blur(20px)",
        }}
      />
    </AbsoluteFill>
  );
};
