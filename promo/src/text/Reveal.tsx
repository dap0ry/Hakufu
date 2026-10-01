import React from "react";
import { Easing, interpolate, useCurrentFrame } from "remotion";
import { inter } from "../fonts";

// Texto que aparece palabra a palabra saliendo de un desenfoque, con un
// degradado blanco → gris como los rótulos de presentación de producto.
export const Reveal: React.FC<{
  text: string;
  from: number;
  until?: number;            // a partir de aquí se desvanece
  size?: number;
  stagger?: number;
  weight?: number;
  style?: React.CSSProperties;
  align?: "center" | "left";
}> = ({ text, from, until, size = 140, stagger = 4, weight = 700, style, align = "center" }) => {
  const frame = useCurrentFrame();
  const words = text.split(" ");
  const out = until === undefined ? 1 : interpolate(frame, [until, until + 12], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const outBlur = until === undefined ? 0 : interpolate(frame, [until, until + 12], [0, 18], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <div
      style={{
        fontFamily: inter, fontWeight: weight, fontSize: size, letterSpacing: "-0.035em", lineHeight: 1.02,
        textAlign: align, opacity: out, filter: `blur(${outBlur}px)`, ...style,
      }}
    >
      {words.map((w, i) => {
        const t = interpolate(frame, [from + i * stagger, from + i * stagger + 22], [0, 1], {
          extrapolateLeft: "clamp", extrapolateRight: "clamp", easing: Easing.bezier(0.16, 1, 0.3, 1),
        });
        return (
          <span
            key={i}
            style={{
              display: "inline-block", marginRight: "0.24em", opacity: t,
              filter: `blur(${(1 - t) * 22}px)`, translate: `0px ${(1 - t) * 0.35 * size}px`,
              backgroundImage: "linear-gradient(180deg, #ffffff 30%, #a9abb2 100%)",
              WebkitBackgroundClip: "text", backgroundClip: "text", color: "transparent",
            }}
          >
            {w}
          </span>
        );
      })}
    </div>
  );
};
