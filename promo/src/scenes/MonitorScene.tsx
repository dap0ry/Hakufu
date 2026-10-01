import React from "react";
import { AbsoluteFill, Easing, useCurrentFrame } from "remotion";
import { Monitor } from "../devices/Monitor";
import { tween } from "../ease";
import { Reader } from "../screen/Reader";
import { Backdrop, Stage } from "./Stage";
import { Caption } from "./Caption";

// Un monitor de PC entra girando con el lector a doble página pasando hojas.
export const MonitorScene: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill>
      <Backdrop />
      <Stage
        yaw={tween(frame, [0, 46], [70, -16], Easing.bezier(0.16, 1, 0.3, 1)) + tween(frame, [46, 150], [0, 8])}
        tilt={tween(frame, [0, 150], [-10, -4])}
        zoom={tween(frame, [0, 150], [0.74, 0.8])}
        x={tween(frame, [0, 46], [900, 330], Easing.bezier(0.16, 1, 0.3, 1))}
        y={tween(frame, [0, 46], [140, 180], Easing.bezier(0.16, 1, 0.3, 1))}
      >
        <Monitor>
          <Reader
            flips={[
              { start: 44, duration: 24 },
              { start: 78, duration: 24 },
              { start: 112, duration: 24 },
            ]}
          />
        </Monitor>
      </Stage>
      <Caption from={30}>Pasa las páginas como en papel.</Caption>
    </AbsoluteFill>
  );
};
