import React from "react";
import { AbsoluteFill, Easing, Img, interpolate, useCurrentFrame } from "remotion";
import type { Assets } from "../assets";
import { easeOut, tween } from "../ease";
import { Reveal } from "../text/Reveal";
import { sceneOpacity } from "./fade";

// 12–17 s: dentro de la pantalla. La biblioteca y la colección a pantalla
// completa, y las tres portadas salen de la pantalla hacia la cámara en 3D.
export const Dive: React.FC<{ duration: number; assets: Assets }> = ({ duration, assets }) => {
  const frame = useCurrentFrame();
  const zoomIn = tween(frame, [0, 26], [0.55, 1], Easing.bezier(0.16, 1, 0.3, 1));
  const pop = (i: number) =>
    interpolate(frame, [70 + i * 6, 104 + i * 6], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: easeOut });
  return (
    <AbsoluteFill style={{ background: "#000", opacity: sceneOpacity(frame, duration, 1, 12), perspective: 1800 }}>
      <AbsoluteFill
        style={{
          scale: `${zoomIn}`,
          borderRadius: interpolate(zoomIn, [0.55, 1], [28, 0]),
          overflow: "hidden",
          filter: `brightness(${interpolate(frame, [70, 110], [1, 0.45], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })})`,
        }}
      >
        <Img src={assets.app.collection} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
      </AbsoluteFill>
      {/* Las portadas salen de la pantalla */}
      <AbsoluteFill style={{ perspective: 1600, transformStyle: "preserve-3d" }}>
        {assets.covers.map((c, i) => {
          const p = pop(i);
          return (
            <Img
              key={i}
              src={c}
              style={{
                position: "absolute",
                width: 300,
                left: `calc(50% + ${(i - 1) * 360}px)`,
                top: "44%",
                translate: "-50% -50%",
                opacity: p,
                transform: `translateZ(${p * 180}px) rotateY(${(1 - i) * 14 * p}deg) rotateX(${(1 - p) * 30}deg)`,
                borderRadius: 10,
                boxShadow: `0 ${40 * p}px ${90 * p}px rgba(0,0,0,0.7)`,
              }}
            />
          );
        })}
      </AbsoluteFill>
      <div style={{ position: "absolute", left: 0, right: 0, bottom: 64 }}>
        <Reveal text="Tus colecciones, ordenadas." from={84} size={84} />
      </div>
    </AbsoluteFill>
  );
};
