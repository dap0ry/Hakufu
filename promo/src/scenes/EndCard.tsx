import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { easeOut, tween } from "../ease";
import { display, inter } from "../fonts";

// Cierre con la marca: gris del fondo de Inicio y el corte rojo en diagonal.
export const EndCard: React.FC = () => {
  const frame = useCurrentFrame();
  const slab = tween(frame, [0, 16], [0, 1], easeOut);
  const title = tween(frame, [8, 26], [0, 1], easeOut);
  const sub = tween(frame, [18, 36], [0, 1], easeOut);
  return (
    <AbsoluteFill style={{ background: "#8A8A8A", fontFamily: inter }}>
      <AbsoluteFill
        style={{
          background: "#E5001A",
          clipPath: `polygon(0 0, ${40 * slab}% 0, ${24 * slab}% 100%, 0 100%)`,
        }}
      />
      <AbsoluteFill
        style={{
          backgroundImage: "radial-gradient(circle, rgba(0,0,0,0.18) 2.4px, transparent 2.9px)",
          backgroundSize: "18px 18px",
          maskImage: "linear-gradient(200deg, #000 0%, transparent 55%)",
        }}
      />
      <div style={{ position: "absolute", left: 160, top: 300 }}>
        <div
          style={{
            fontSize: 250, fontWeight: 900, letterSpacing: -10, lineHeight: 0.9, color: "#000",
            opacity: title, translate: `${(1 - title) * -60}px 0px`,
          }}
        >
          Hakufu
        </div>
        <div style={{ fontFamily: display, fontSize: 92, marginTop: 30, color: "#000", opacity: sub, translate: `0px ${(1 - sub) * 30}px` }}>
          Tu manga, en tu equipo.
        </div>
        <div style={{ fontSize: 34, fontWeight: 600, marginTop: 34, color: "#000", opacity: sub }}>
          Gratis para Windows, macOS y Linux
        </div>
        <div style={{ fontSize: 30, marginTop: 10, color: "#1b1b1b", opacity: sub }}>hakufuweb.vercel.app</div>
      </div>
    </AbsoluteFill>
  );
};
