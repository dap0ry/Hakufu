import { interpolate } from "remotion";

/** Opacidad de una escena de `duration` fotogramas con entrada y salida suaves. */
export const sceneOpacity = (frame: number, duration: number, inF = 12, outF = 12) =>
  Math.min(
    interpolate(frame, [0, inF], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }),
    interpolate(frame, [duration - outF, duration], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }),
  );
