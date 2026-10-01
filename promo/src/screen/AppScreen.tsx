import React from "react";
import { Img, interpolate, staticFile, useCurrentFrame } from "remotion";
import { easeOut } from "../ease";

// Lo que se ve en la pantalla del portátil: capturas reales de la app (1280×800)
// que cambian como al navegar (la nueva entra desde la derecha).

export type Shot = { src: string; from: number };

export const AppScreen: React.FC<{ shots: Shot[]; power: number; w?: number; h?: number }> = ({ shots, power, w = 1280, h = 800 }) => {
  const frame = useCurrentFrame();
  return (
    <div style={{ width: w, height: h, background: "#000", position: "relative", overflow: "hidden" }}>
      {shots.map((s, i) => {
        if (frame < s.from) return null;
        const next = shots[i + 1];
        if (next && frame >= next.from + 12) return null;
        const enter = i === 0 ? 1 : interpolate(frame, [s.from, s.from + 12], [0, 1], {
          extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: easeOut,
        });
        return (
          <Img
            key={s.src}
            src={s.src.startsWith("http") || s.src.startsWith("/") ? s.src : staticFile(s.src)}
            style={{
              position: "absolute", inset: 0, width: w, height: h,
              opacity: enter,
              translate: `${(1 - enter) * 60}px 0px`,
            }}
          />
        );
      })}
      {/* Encendido: de negro a la imagen */}
      <div style={{ position: "absolute", inset: 0, background: "#000", opacity: 1 - power }} />
    </div>
  );
};
