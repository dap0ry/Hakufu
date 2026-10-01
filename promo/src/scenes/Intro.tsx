import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { tween } from "../ease";
import { Reveal } from "../text/Reveal";
import { sceneOpacity } from "./fade";

// 0–5 s: negro, dos frases que salen del desenfoque y un barrido de luz.
export const Intro: React.FC<{ duration: number }> = ({ duration }) => {
  const frame = useCurrentFrame();
  const sweep = tween(frame, [0, 140], [-40, 140]);
  return (
    <AbsoluteFill style={{ background: "#000", opacity: sceneOpacity(frame, duration, 1, 14) }}>
      <AbsoluteFill
        style={{
          background: `linear-gradient(100deg, rgba(255,255,255,0) ${sweep - 18}%, rgba(255,255,255,0.05) ${sweep}%, rgba(255,255,255,0) ${sweep + 18}%)`,
        }}
      />
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
        <Reveal text="Todo tu manga." from={10} until={64} size={150} />
      </AbsoluteFill>
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
        <Reveal text="En un solo sitio." from={78} size={150} />
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
