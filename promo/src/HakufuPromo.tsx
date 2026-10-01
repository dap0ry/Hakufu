import React from "react";
import { Audio } from "@remotion/media";
import { AbsoluteFill, Sequence, staticFile, useVideoConfig } from "remotion";
import { getAssets, type AssetSet } from "./assets";
import { Grain, Vignette } from "./env/Grain";
import { Dive } from "./scenes/Dive";
import { Finale } from "./scenes/Finale";
import { Intro } from "./scenes/Intro";
import { LaptopReveal } from "./scenes/LaptopReveal";
import { MONTAGE, Montage } from "./scenes/Montage";
import { READER_FLIPS, ReaderScene } from "./scenes/ReaderScene";
import { T } from "./timeline";
import "./fonts";

export type PromoProps = { set: AssetSet };

// Un efecto de sonido en un fotograma concreto.
const Sfx: React.FC<{ at: number; src: string; volume?: number; name: string }> = ({ at, src, volume = 1, name }) => {
  const { fps } = useVideoConfig();
  return (
    <Sequence name={name} from={at} durationInFrames={4 * fps} layout="none">
      <Audio src={staticFile(`audio/${src}`)} volume={volume} />
    </Sequence>
  );
};

// Vídeo de presentación de Hakufu: 35 s, 1920×1080, 30 fps, con música y efectos.
export const HakufuPromo: React.FC<PromoProps> = ({ set }) => {
  const { fps } = useVideoConfig();
  const assets = getAssets(set);
  return (
    <AbsoluteFill style={{ background: "#000" }}>
      <Sequence name="Intro" from={T.intro.from} durationInFrames={T.intro.duration} premountFor={fps}>
        <Intro duration={T.intro.duration} />
      </Sequence>
      <Sequence name="Portátil" from={T.laptop.from} durationInFrames={T.laptop.duration} premountFor={fps}>
        <LaptopReveal duration={T.laptop.duration} assets={assets} />
      </Sequence>
      <Sequence name="Dentro de la pantalla" from={T.dive.from} durationInFrames={T.dive.duration} premountFor={fps}>
        <Dive duration={T.dive.duration} assets={assets} />
      </Sequence>
      <Sequence name="Lector" from={T.reader.from} durationInFrames={T.reader.duration} premountFor={fps}>
        <ReaderScene duration={T.reader.duration} assets={assets} />
      </Sequence>
      <Sequence name="Montaje" from={T.montage.from} durationInFrames={T.montage.duration} premountFor={fps}>
        <Montage assets={assets} />
      </Sequence>
      <Sequence name="Final" from={T.finale.from} durationInFrames={T.finale.duration} premountFor={fps}>
        <Finale />
      </Sequence>

      <Vignette strength={0.55} />
      <Grain opacity={0.06} />

      {/* Sonido */}
      <Audio src={staticFile("audio/music.wav")} volume={0.95} />
      <Sfx name="Barrido entrada portátil" at={T.laptop.from - 6} src="sfx-whoosh.wav" volume={0.5} />
      <Sfx name="Tapa" at={T.laptop.from + 92 - 26} src="sfx-lid.wav" volume={0.9} />
      <Sfx name="Encendido" at={T.laptop.from + 94} src="sfx-power.wav" volume={0.7} />
      <Sfx name="Barrido a la pantalla" at={T.dive.from - 8} src="sfx-whoosh.wav" volume={0.55} />
      <Sfx name="Barrido monitor" at={T.reader.from - 4} src="sfx-whoosh.wav" volume={0.5} />
      {READER_FLIPS.map((f, i) => (
        <Sfx key={i} name={`Hoja ${i + 1}`} at={T.reader.from + f.start} src="sfx-page.wav" volume={0.95} />
      ))}
      <Sfx name="Barrido montaje" at={T.montage.from + MONTAGE.theme} src="sfx-whoosh.wav" volume={0.35} />
      <Sfx name="Golpe final" at={T.finale.from} src="sfx-hit.wav" volume={0.85} />
    </AbsoluteFill>
  );
};
