import React from "react";
import { Composition, Folder } from "remotion";
import { HakufuPromo } from "./HakufuPromo";
import { EndCard } from "./scenes/EndCard";
import { LaptopScene } from "./scenes/LaptopScene";
import { MonitorScene } from "./scenes/MonitorScene";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      <Composition id="HakufuPromo" component={HakufuPromo} durationInFrames={450} fps={30} width={1920} height={1080} />
      <Folder name="Escenas">
        <Composition id="Portatil" component={LaptopScene} durationInFrames={220} fps={30} width={1920} height={1080} />
        <Composition id="Monitor" component={MonitorScene} durationInFrames={150} fps={30} width={1920} height={1080} />
        <Composition id="Cierre" component={EndCard} durationInFrames={104} fps={30} width={1920} height={1080} />
      </Folder>
    </>
  );
};
