import React from "react";
import { interpolate, useCurrentFrame } from "remotion";
import { easeOut } from "../ease";
import { display } from "../fonts";

// Rótulo grande abajo a la izquierda que entra subiendo.
export const Caption: React.FC<{ from: number; children: React.ReactNode }> = ({ from, children }) => {
  const frame = useCurrentFrame();
  const t = interpolate(frame, [from, from + 18], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: easeOut });
  return (
    <div
      style={{
        position: "absolute", left: 110, top: "50%", marginTop: -100, fontFamily: display, fontSize: 104, lineHeight: 0.92,
        color: "#F2F2F2", opacity: t, translate: `0px ${(1 - t) * 40}px`, textShadow: "0 6px 30px rgba(0,0,0,0.6)",
        maxWidth: 640,
      }}
    >
      {children}
    </div>
  );
};
