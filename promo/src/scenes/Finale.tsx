import React from "react";
import { AbsoluteFill, Easing, interpolate, useCurrentFrame } from "remotion";
import { easeOut, tween } from "../ease";
import { inter } from "../fonts";
import { Reveal } from "../text/Reveal";

// 31–35 s: el nombre con un destello que lo recorre y la llamada final.
export const Finale: React.FC = () => {
  const frame = useCurrentFrame();
  const hit = interpolate(frame, [0, 14], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: easeOut });
  const shine = tween(frame, [10, 50], [-20, 120], Easing.bezier(0.45, 0, 0.25, 1));
  const glow = interpolate(frame, [0, 6, 40], [0, 1, 0.35], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill style={{ background: "#000", justifyContent: "center", alignItems: "center", fontFamily: inter }}>
      {/* halo detrás del nombre */}
      <div
        style={{
          position: "absolute", width: 1100, height: 520, borderRadius: "50%",
          background: "radial-gradient(ellipse, rgba(229,0,26,0.28) 0%, rgba(229,0,26,0) 70%)",
          opacity: glow, filter: "blur(40px)",
        }}
      />
      <div
        style={{
          fontSize: 260, fontWeight: 800, letterSpacing: "-0.05em", lineHeight: 1,
          scale: `${1.12 - 0.12 * hit}`, opacity: hit, filter: `blur(${(1 - hit) * 20}px)`,
          backgroundImage: `linear-gradient(105deg, #b9bbc2 ${shine - 14}%, #ffffff ${shine}%, #b9bbc2 ${shine + 14}%)`,
          WebkitBackgroundClip: "text", backgroundClip: "text", color: "transparent",
          marginTop: -60,
        }}
      >
        Hakufu
      </div>
      <div style={{ position: "absolute", top: "64%", left: 0, right: 0 }}>
        <Reveal text="Gratis." from={22} size={64} weight={600} />
      </div>
      <div
        style={{
          position: "absolute", top: "74%", fontSize: 34, color: "#8e9096", letterSpacing: "-0.01em",
          opacity: tween(frame, [34, 52], [0, 1]),
        }}
      >
        hakufuweb.vercel.app
      </div>
    </AbsoluteFill>
  );
};
