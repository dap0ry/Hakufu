// Línea de tiempo del vídeo (30 fps, música a 100 BPM: 1 compás = 72 fotogramas).
// Las escenas se solapan unos fotogramas para fundirse; los cortes del montaje
// caen en los tiempos de la música.
export const FPS = 30;
export const DURATION = 1050; // 35 s

export const T = {
  intro: { from: 0, duration: 156 },
  laptop: { from: 144, duration: 228 },
  dive: { from: 360, duration: 156 },
  reader: { from: 504, duration: 222 },
  montage: { from: 720, duration: 216 },
  finale: { from: 936, duration: 114 },
};
