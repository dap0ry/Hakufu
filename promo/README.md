# Vídeo de presentación de Hakufu

Proyecto de [Remotion](https://www.remotion.dev). 35 s, 1920×1080, 30 fps, con música y efectos.

```bash
npm i
npm run dev                     # Remotion Studio (vista previa)
npx remotion render HakufuPromoDemo out/hakufu-promo-35s-demo.mp4   # versión para publicar
npx remotion render HakufuPromo     out/hakufu-promo-35s.mp4        # con tus mangas reales (privada)
```

- **`HakufuPromoDemo`**: con mangas inventados (`public/pages`, `public/demo`). Es la que se puede subir a la web y a redes.
- **`HakufuPromo`**: con las portadas, páginas y capturas de tu biblioteca real, en `public/real/`. **No está en git**: los mangas tienen copyright y el repo es público. Para regenerarla hay que volver a sacar las capturas de la app y las páginas de tus `.cbz`/`.cbr` a `public/real/{app,covers,pages}` con los nombres que espera `src/assets.ts`.

## Sonido

Todo sintetizado, sin samples ni licencias de terceros:

```bash
python3 -m venv .venv && .venv/bin/pip install numpy scipy
.venv/bin/python audio/compose.py public/audio
```

`music.wav` (100 BPM, si menor) va cuadrada con las escenas de `src/timeline.ts`; los `sfx-*.wav` se colocan en `src/HakufuPromo.tsx`.

## Estructura

- `src/scenes/`: Intro, LaptopReveal, Dive, ReaderScene, Montage, Finale.
- `src/devices/`: portátil y monitor en 3D con CSS (sin modelos ni marcas).
- `src/env/`: estudio (suelo, foco, portadas flotando), grano y viñeta.
- `src/screen/`: capturas de la app y el lector con la animación de pasar la hoja.
