import React from "react";

// Portátil en 3D con CSS (sin modelos ni marcas): base de aluminio con teclado
// y trackpad, tapa que gira sobre la bisagra y pantalla con lo que se le pase
// como children (1280×800, escalado al hueco del panel).
//
// Ejes: el origen es el centro de la bisagra. La base se extiende hacia el
// espectador (+z) y la tapa hacia arriba. lidAngle: 0 cerrada, ~105 abierta.

const W = 960;          // ancho
const D = 640;          // fondo de la base
const T = 18;           // grosor de la base
const LID_H = 620;      // alto de la tapa
const BEZEL = 22;
const SCREEN_W = W - BEZEL * 2;
const SCREEN_H = LID_H - BEZEL * 2 - 8;

const alu = "linear-gradient(160deg, #e4e6e9 0%, #c3c7cc 45%, #a7abb1 100%)";
const aluDark = "linear-gradient(180deg, #b9bdc2 0%, #8c9096 100%)";

const face: React.CSSProperties = {
  position: "absolute",
  backfaceVisibility: "hidden",
  transformStyle: "preserve-3d",
};

const Keyboard: React.FC = () => {
  const rows = [14, 14, 13, 12, 11];
  return (
    <div style={{ position: "absolute", left: 70, right: 70, top: 46, display: "flex", flexDirection: "column", gap: 7 }}>
      {rows.map((n, r) => (
        <div key={r} style={{ display: "flex", gap: 7, justifyContent: "center" }}>
          {Array.from({ length: n }).map((_, i) => (
            <div
              key={i}
              style={{
                flex: r === 4 && i === 5 ? 5 : 1,
                height: 46,
                borderRadius: 6,
                background: "linear-gradient(180deg, #2a2c30 0%, #17181b 100%)",
                boxShadow: "inset 0 1px 0 rgba(255,255,255,0.07), 0 1px 0 rgba(0,0,0,0.35)",
              }}
            />
          ))}
        </div>
      ))}
    </div>
  );
};

export const Laptop: React.FC<{
  lidAngle: number;
  children: React.ReactNode;
  style?: React.CSSProperties;
}> = ({ lidAngle, children, style }) => {
  const lidTilt = lidAngle - 90; // rotateX: -90 cerrada (tumbada sobre la base)
  return (
    <div style={{ position: "absolute", left: 0, top: 0, width: 0, height: 0, transformStyle: "preserve-3d", transform: `translateZ(${-D / 2}px)`, ...style }}>
      {/* Sombra en el suelo */}
      <div
        style={{
          ...face, left: -W / 2 - 60, top: 0, width: W + 120, height: D + 120,
          transformOrigin: "top center",
          transform: `translateY(${T + 1}px) rotateX(90deg) translateY(-40px)`,
          background: "radial-gradient(ellipse at center, rgba(0,0,0,0.55) 0%, rgba(0,0,0,0) 65%)",
          filter: "blur(14px)",
        }}
      />

      {/* Base: cara de arriba (teclado + trackpad) */}
      <div
        style={{
          ...face, left: -W / 2, top: 0, width: W, height: D,
          transformOrigin: "top center", transform: "rotateX(90deg)",
          background: alu, borderRadius: "0 0 22px 22px",
          boxShadow: "inset 0 0 0 1px rgba(255,255,255,0.35)",
        }}
      >
        <div style={{ position: "absolute", left: 40, right: 40, top: 30, height: 300, borderRadius: 10, background: "rgba(0,0,0,0.08)" }} />
        <Keyboard />
        <div
          style={{
            position: "absolute", left: W / 2 - 190, width: 380, top: 380, height: 220, borderRadius: 14,
            background: "linear-gradient(160deg, #d9dce0, #b5b9be)",
            boxShadow: "inset 0 0 0 1px rgba(0,0,0,0.12), inset 0 1px 2px rgba(0,0,0,0.12)",
          }}
        />
      </div>
      {/* Base: canto delantero */}
      <div
        style={{
          ...face, left: -W / 2, top: 0, width: W, height: T,
          transform: `translateZ(${D}px)`,
          background: aluDark, borderRadius: "0 0 14px 14px",
        }}
      />
      {/* Base: cantos laterales */}
      <div style={{ ...face, left: -W / 2, top: 0, width: D, height: T, transformOrigin: "left top", transform: `rotateY(-90deg)`, background: aluDark }} />
      <div style={{ ...face, left: W / 2, top: 0, width: D, height: T, transformOrigin: "left top", transform: `translateZ(${D}px) rotateY(90deg)`, background: aluDark }} />

      {/* Tapa: gira sobre la bisagra */}
      <div
        style={{
          ...face, left: -W / 2, top: -LID_H, width: W, height: LID_H,
          transformOrigin: "bottom center", transform: `rotateX(${lidTilt}deg)`,
        }}
      >
        {/* Delante: pantalla */}
        <div
          style={{
            ...face, inset: 0, borderRadius: "24px 24px 6px 6px",
            background: "#0b0b0c",
            boxShadow: "inset 0 0 0 2px #2a2b2e",
          }}
        >
          <div
            style={{
              position: "absolute", left: BEZEL, top: BEZEL, width: SCREEN_W, height: SCREEN_H,
              borderRadius: 6, overflow: "hidden", background: "#000",
            }}
          >
            <div style={{ width: 1280, height: 800, transformOrigin: "0 0", scale: `${SCREEN_W / 1280} ${SCREEN_H / 800}` }}>
              {children}
            </div>
            {/* Reflejo del cristal */}
            <div
              style={{
                position: "absolute", inset: 0, pointerEvents: "none",
                background: "linear-gradient(115deg, rgba(255,255,255,0.10) 0%, rgba(255,255,255,0.02) 38%, rgba(255,255,255,0) 60%)",
              }}
            />
          </div>
          <div style={{ position: "absolute", bottom: 10, left: 0, right: 0, textAlign: "center", color: "#55575c", fontSize: 13, letterSpacing: 1 }} />
        </div>
        {/* Detrás: aluminio */}
        <div
          style={{
            ...face, inset: 0, borderRadius: "24px 24px 6px 6px",
            transform: "rotateY(180deg)",
            background: alu,
            boxShadow: "inset 0 0 0 1px rgba(255,255,255,0.4)",
          }}
        />
      </div>
    </div>
  );
};
