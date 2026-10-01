import { staticFile } from "remotion";

// Dos juegos de imágenes con el mismo vídeo:
//  · "real": tu biblioteca (Kagurabachi) — para uso privado: tiene copyright.
//  · "demo": mangas inventados — para publicar sin problemas.
export type AssetSet = "real" | "demo";

export type Assets = {
  covers: string[];          // portadas en alta (3)
  app: { home: string; library: string; collection: string; collectionLight: string; profile: string };
  appSize: { w: number; h: number };
  page: (n: number) => string;
  pageAspect: number;        // ancho / alto de una página
  firstPage: number;         // página izquierda del primer pliego del lector
  title: string;             // título en la barra del lector
  totalPages: number;
};

export const getAssets = (set: AssetSet): Assets =>
  set === "real"
    ? {
        covers: ["real/covers/t01.jpg", "real/covers/t02.jpg", "real/covers/t03.jpg"].map((f) => staticFile(f)),
        app: {
          home: staticFile("real/app/home.png"),
          library: staticFile("real/app/library.png"),
          collection: staticFile("real/app/collection.png"),
          collectionLight: staticFile("real/app/collection-light.png"),
          profile: staticFile("real/app/profile.png"),
        },
        appSize: { w: 1920, h: 1200 },
        page: (n) => staticFile(`real/pages/p${n}.jpg`),
        pageAspect: 1371 / 2160,
        firstPage: 8,
        title: "Kagurabachi - Tomo 02 (#009-018)",
        totalPages: 185,
      }
    : {
        covers: ["demo/covers/c1.png", "demo/covers/c2.png", "demo/covers/c3.png"].map((f) => staticFile(f)),
        app: {
          home: staticFile("app/home.png"),
          library: staticFile("app/library.png"),
          collection: staticFile("app/collection.png"),
          collectionLight: staticFile("app/collection-light.png"),
          profile: staticFile("app/profile.png"),
        },
        appSize: { w: 1280, h: 800 },
        page: (n) => staticFile(`pages/p${n}.png`),
        pageAspect: 600 / 900,
        firstPage: 1,
        title: "Kurogane 1",
        totalPages: 7,
      };
