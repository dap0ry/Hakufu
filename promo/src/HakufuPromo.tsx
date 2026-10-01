import React from "react";
import { linearTiming, TransitionSeries } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import { useVideoConfig } from "remotion";
import { EndCard } from "./scenes/EndCard";
import { LaptopScene } from "./scenes/LaptopScene";
import { MonitorScene } from "./scenes/MonitorScene";
import "./fonts";

// Vídeo de presentación de Hakufu: 15 s, 1920×1080, 30 fps.
export const HakufuPromo: React.FC = () => {
  const { fps } = useVideoConfig();
  return (
    <TransitionSeries>
      <TransitionSeries.Sequence name="Portátil" durationInFrames={220} premountFor={fps}>
        <LaptopScene />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition presentation={fade()} timing={linearTiming({ durationInFrames: 12 })} />
      <TransitionSeries.Sequence name="Monitor" durationInFrames={150} premountFor={fps}>
        <MonitorScene />
      </TransitionSeries.Sequence>
      <TransitionSeries.Transition presentation={fade()} timing={linearTiming({ durationInFrames: 12 })} />
      <TransitionSeries.Sequence name="Cierre" durationInFrames={104} premountFor={fps}>
        <EndCard />
      </TransitionSeries.Sequence>
    </TransitionSeries>
  );
};
