import React from "react";

// Monitor de PC en 3D con CSS: panel con marco fino, trasera gruesa, cuello y
// pie. El origen es el centro de la base del pie.

const W = 1180;
const H = Math.round((W * 800) / 1280) + 40;
const DEPTH = 46;
const BEZEL = 14;
const NECK_H = 230;

const face: React.CSSProperties = { position: "absolute", backfaceVisibility: "visible", transformStyle: "preserve-3d" };
const dark = "linear-gradient(180deg, #2b2d31 0%, #141518 100%)";

export const Monitor: React.FC<{ children: React.ReactNode; style?: React.CSSProperties; contentW?: number; contentH?: number }> = ({
  children,
  style,
  contentW = 1280,
  contentH = 800,
}) => {
  const panelBottom = -NECK_H + 40;
  return (
    <div style={{ position: "absolute", left: 0, top: 0, width: 0, height: 0, transformStyle: "preserve-3d", ...style }}>
      {/* Sombra */}
      <div
        style={{
          ...face, left: -320, top: 0, width: 640, height: 420, transformOrigin: "top center",
          transform: "rotateX(90deg) translateY(-210px)",
          background: "radial-gradient(ellipse at center, rgba(0,0,0,0.6) 0%, rgba(0,0,0,0) 65%)", filter: "blur(16px)",
        }}
      />
      {/* Pie */}
      <div
        style={{
          ...face, left: -170, top: 0, width: 340, height: 240, transformOrigin: "top center",
          transform: "rotateX(90deg) translateY(-150px)", background: dark, borderRadius: 26,
          boxShadow: "inset 0 0 0 1px rgba(255,255,255,0.08)",
        }}
      />
      {/* Cuello */}
      <div style={{ ...face, left: -40, top: -NECK_H, width: 80, height: NECK_H, transform: "translateZ(-70px)", background: dark, borderRadius: 10 }} />

      {/* Panel */}
      <div style={{ ...face, left: -W / 2, top: panelBottom - H, width: W, height: H }}>
        {/* Delante */}
        <div style={{ ...face, inset: 0, borderRadius: 14, background: "#0a0a0b", boxShadow: "inset 0 0 0 2px #2c2d31" }}>
          <div style={{ position: "absolute", left: BEZEL, top: BEZEL, right: BEZEL, bottom: BEZEL + 26, overflow: "hidden", borderRadius: 4 }}>
            <div style={{ width: contentW, height: contentH, transformOrigin: "0 0", scale: `${(W - BEZEL * 2) / contentW} ${(H - BEZEL * 2 - 26) / contentH}` }}>
              {children}
            </div>
            <div style={{ position: "absolute", inset: 0, background: "linear-gradient(115deg, rgba(255,255,255,0.08) 0%, rgba(255,255,255,0) 45%)" }} />
          </div>
        </div>
        {/* Detrás */}
        <div style={{ ...face, inset: 0, borderRadius: 14, transform: `translateZ(-${DEPTH}px) rotateY(180deg)`, background: dark }} />
        {/* Cantos */}
        <div style={{ ...face, left: 0, top: 0, width: DEPTH, height: H, transformOrigin: "left center", transform: "rotateY(90deg)", background: "#1d1e21" }} />
        <div style={{ ...face, left: W, top: 0, width: DEPTH, height: H, transformOrigin: "left center", transform: "rotateY(90deg)", background: "#1d1e21" }} />
        <div style={{ ...face, left: 0, top: 0, width: W, height: DEPTH, transformOrigin: "center top", transform: "rotateX(-90deg)", background: "#232428" }} />
      </div>
    </div>
  );
};
