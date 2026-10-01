import React from "react";
import { AbsoluteFill, Easing, useCurrentFrame } from "remotion";
import type { Assets } from "../assets";
import { Monitor } from "../devices/Monitor";
import { tween } from "../ease";
import { Studio } from "../env/Studio";
import { Reader } from "../screen/Reader";
import { Reveal } from "../text/Reveal";
import { sceneOpacity } from "./fade";
import { Reflected, Stage } from "./Stage";

export const READER_FLIPS = [
  { start: 46, duration: 26 },
  { start: 92, duration: 26 },
  { start: 138, duration: 26 },
];

// 17–24 s: un monitor de PC entra girando con el lector y pasa hojas; al
// final la cámara se acerca a la página (sube la tensión antes del clímax).
export const ReaderScene: React.FC<{ duration: number; assets: Assets }> = ({ duration, assets }) => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill style={{ opacity: sceneOpacity(frame, duration, 12, 8) }}>
      <Studio covers={assets.covers} drift={tween(frame, [0, duration], [-50, 50])} />
      <Stage
        yaw={tween(frame, [0, 50], [62, -12], Easing.bezier(0.16, 1, 0.3, 1)) + tween(frame, [50, duration], [0, 10])}
        tilt={tween(frame, [0, duration], [-9, -3])}
        zoom={tween(frame, [0, 50], [0.62, 0.74], Easing.bezier(0.16, 1, 0.3, 1)) + tween(frame, [150, duration], [0, 0.55], Easing.bezier(0.6, 0, 0.9, 0.4))}
        x={tween(frame, [0, 50], [520, 0], Easing.bezier(0.16, 1, 0.3, 1))}
        y={tween(frame, [0, 50], [200, 230])}
      >
        <Reflected floorY={0}>
          <Monitor>
            <Reader
              flips={READER_FLIPS}
              firstPage={assets.firstPage}
              page={assets.page}
              pageAspect={assets.pageAspect}
              title={assets.title}
              totalPages={assets.totalPages}
            />
          </Monitor>
        </Reflected>
      </Stage>
      <div style={{ position: "absolute", left: 0, right: 0, top: 70 }}>
        <Reveal text="Pasa las páginas como en papel." from={20} until={160} size={84} />
      </div>
    </AbsoluteFill>
  );
};
