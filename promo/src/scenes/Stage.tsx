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

/**
 * Suelo que refleja: una copia del dispositivo en espejo por debajo del suelo
 * y, entre las dos, un plano oscuro semitransparente (más opaco lejos del
 * centro) que la atenúa. El suelo está en y = floorY del sistema del dispositivo.
 */
export const Reflected: React.FC<{ children: React.ReactNode; floorY?: number }> = ({ children, floorY = 18 }) => (
  <>
    <div
      style={{
        position: "absolute", left: 0, top: 0, width: 0, height: 0, transformStyle: "preserve-3d",
        transform: `translateY(${2 * floorY}px) scaleY(-1)`,
      }}
    >
      {children}
    </div>
    <div
      style={{
        position: "absolute", left: -3000, top: floorY - 2500, width: 6000, height: 5000,
        transform: "rotateX(90deg)", transformOrigin: "center center",
        // Se funde con el fondo hacia los bordes: sin horizonte duro.
        background: "radial-gradient(ellipse 16% 16% at 50% 50%, rgba(6,6,7,0.7) 0%, rgba(6,6,7,0.9) 45%, rgba(6,6,7,0) 100%)",
      }}
    />
    {children}
  </>
);
