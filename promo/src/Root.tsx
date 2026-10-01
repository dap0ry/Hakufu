import React from "react";
import { Composition } from "remotion";
import { HakufuPromo } from "./HakufuPromo";
import { DURATION, FPS } from "./timeline";

export const RemotionRoot: React.FC = () => {
  return (
    <>
      {/* Con tus mangas reales (Kagurabachi): para uso privado, tiene copyright. */}
      <Composition
        id="HakufuPromo"
        component={HakufuPromo}
        durationInFrames={DURATION}
        fps={FPS}
        width={1920}
        height={1080}
        defaultProps={{ set: "real" as const }}
      />
      {/* Con los mangas de demostración: para publicar en la web y en redes. */}
      <Composition
        id="HakufuPromoDemo"
        component={HakufuPromo}
        durationInFrames={DURATION}
        fps={FPS}
        width={1920}
        height={1080}
        defaultProps={{ set: "demo" as const }}
      />
    </>
  );
};
