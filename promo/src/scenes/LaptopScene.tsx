import React from "react";
import { AbsoluteFill, Easing, useCurrentFrame } from "remotion";
import { Laptop } from "../devices/Laptop";
import { tween } from "../ease";
import { AppScreen } from "../screen/AppScreen";
import { Backdrop, Stage } from "./Stage";
import { Caption } from "./Caption";

// El portátil cerrado gira, se abre, se enciende con el menú de Hakufu y
// navega a la biblioteca y a una colección.
export const LaptopScene: React.FC = () => {
  const frame = useCurrentFrame();
  return (
    <AbsoluteFill>
      <Backdrop />
      <Stage
        yaw={tween(frame, [0, 220], [-40, 22], Easing.bezier(0.45, 0, 0.25, 1))}
        tilt={tween(frame, [0, 120], [-34, -12])}
        zoom={tween(frame, [0, 120], [0.66, 0.8])}
        x={tween(frame, [84, 130], [0, 330])}
        y={tween(frame, [0, 120], [-80, -30])}
      >
        <Laptop lidAngle={tween(frame, [16, 80], [0, 104], Easing.bezier(0.5, 0, 0.2, 1))}>
          <AppScreen
            power={tween(frame, [60, 82], [0, 1])}
            shots={[
              { src: "app/home.png", from: 0 },
              { src: "app/library.png", from: 122 },
              { src: "app/collection.png", from: 172 },
            ]}
          />
        </Laptop>
      </Stage>
      <Caption from={100}>Tu manga, en tu equipo.</Caption>
    </AbsoluteFill>
  );
};
