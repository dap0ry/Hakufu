import React from "react";
import { AbsoluteFill } from "remotion";

// Escenario 3D: perspectiva y una "cámara" (rotaciones del mundo) centrada.
export const Stage: React.FC<{
  yaw: number;
  tilt: number;
  zoom: number;
  y?: number;
  x?: number;
  children: React.ReactNode;
}> = ({ yaw, tilt, zoom, x = 0, y = 0, children }) => (
  <AbsoluteFill style={{ perspective: 2600, perspectiveOrigin: "50% 38%" }}>
    <div
      style={{
        position: "absolute",
        left: `calc(50% + ${x}px)`,
        top: `calc(62% + ${y}px)`,
        width: 0,
        height: 0,
        transformStyle: "preserve-3d",
        transform: `scale3d(${zoom}, ${zoom}, ${zoom}) rotateX(${tilt}deg) rotateY(${yaw}deg)`,
      }}
    >
      {children}
    </div>
  </AbsoluteFill>
);

export const Backdrop: React.FC = () => (
  <AbsoluteFill style={{ background: "radial-gradient(ellipse 70% 60% at 50% 45%, #232323 0%, #0c0c0c 60%, #050505 100%)" }} />
);
