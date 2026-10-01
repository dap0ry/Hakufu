import React from "react";
import { AbsoluteFill, Easing, useCurrentFrame } from "remotion";
import type { Assets } from "../assets";
import { Laptop } from "../devices/Laptop";
import { tween } from "../ease";
import { Studio } from "../env/Studio";
import { AppScreen } from "../screen/AppScreen";
import { Reveal } from "../text/Reveal";
import { sceneOpacity } from "./fade";
import { Reflected, Stage } from "./Stage";

// 5–12 s: el portátil gira en el estudio, se abre y se enciende con Hakufu.
export const LaptopReveal: React.FC<{ duration: number; assets: Assets }> = ({ duration, assets }) => {
  const frame = useCurrentFrame();
  const lid = tween(frame, [24, 92], [0, 106], Easing.bezier(0.55, 0, 0.15, 1));
  const device = (
    <Laptop lidAngle={lid} contentW={assets.appSize.w} contentH={assets.appSize.h}>
      <AppScreen
        w={assets.appSize.w}
        h={assets.appSize.h}
        power={tween(frame, [92, 112], [0, 1])}
        shots={[{ src: assets.app.home, from: 0 }]}
      />
    </Laptop>
  );
  return (
    <AbsoluteFill style={{ opacity: sceneOpacity(frame, duration, 16, 10) }}>
      <Studio covers={assets.covers} light={tween(frame, [0, 40], [0.2, 1])} drift={tween(frame, [0, duration], [60, -60])} />
      <Stage
        yaw={tween(frame, [0, duration], [-48, 18], Easing.bezier(0.33, 0, 0.2, 1))}
        tilt={tween(frame, [0, 120], [-30, -11])}
        zoom={tween(frame, [0, duration], [0.58, 0.78], Easing.bezier(0.33, 0, 0.2, 1))}
        y={tween(frame, [0, 120], [-20, 70])}
      >
        <Reflected>{device}</Reflected>
      </Stage>
      <div style={{ position: "absolute", left: 0, right: 0, top: 48 }}>
        <Reveal text="Hakufu." from={120} size={88} />
      </div>
    </AbsoluteFill>
  );
};
