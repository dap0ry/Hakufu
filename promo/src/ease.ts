import { Easing, interpolate } from "remotion";

const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

/** interpolate con clamp y la curva suave de toda la pieza. */
export const tween = (
  frame: number,
  input: [number, number],
  output: [number, number],
  easing = Easing.bezier(0.65, 0, 0.35, 1),
) => interpolate(frame, input, output, { ...clamp, easing });

export const easeOut = Easing.bezier(0.16, 1, 0.3, 1);
export const easeInSine = (t: number) => 1 - Math.cos((t * Math.PI) / 2);
export const easeOutSine = (t: number) => Math.sin((t * Math.PI) / 2);
