import React from "react";
import { Img, useCurrentFrame } from "remotion";
import { easeInSine, easeOutSine } from "../ease";
import { inter } from "../fonts";

// El lector de Hakufu a doble página (1280×800), con la misma animación de
// pasar la hoja que la app: la hoja derecha gira sobre el lomo hasta ponerse de
// canto y su reverso (la página nueva) termina el giro en el lado izquierdo.

const W = 1280;
const H = 800;
const TOP = 53;
const BOTTOM = 52;
const PAGE_H = H - TOP - BOTTOM - 16;
const CENTER = W / 2;

// Lo que cambia entre el juego "real" y el "demo" (ver assets.ts).
type Book = { page: (n: number) => string; pageAspect: number; title: string; totalPages: number };
const BookContext = React.createContext<Book | null>(null);
const useBook = () => React.useContext(BookContext)!;

type Flip = { start: number; duration: number };

const PageImg: React.FC<{
  n: number;
  side: "left" | "right";
  style?: React.CSSProperties;
  children?: React.ReactNode;
}> = ({ n, side, style, children }) => {
  const book = useBook();
  const pageW = Math.round(PAGE_H * book.pageAspect);
  return (
    <div
      style={{
        position: "absolute",
        top: TOP + 8,
        left: side === "left" ? CENTER - pageW - 0.5 : CENTER + 0.5,
        width: pageW,
        height: PAGE_H,
        ...style,
      }}
    >
      <Img src={book.page(n)} style={{ width: "100%", height: "100%", display: "block" }} />
      {children}
    </div>
  );
};

/** Degradado negro que nace en el lomo (sombra sobre la página de debajo). */
const spineShadow = (spineOnLeft: boolean, alpha: number) =>
  `linear-gradient(${spineOnLeft ? "90deg" : "270deg"}, rgba(0,0,0,${alpha}) 0%, rgba(0,0,0,0) 45%)`;
/** Oscurece la hoja hacia su borde libre. */
const leafShade = (spineOnLeft: boolean, alpha: number) =>
  `linear-gradient(${spineOnLeft ? "90deg" : "270deg"}, rgba(0,0,0,0) 0%, rgba(0,0,0,${alpha}) 100%)`;

export const Reader: React.FC<{ flips: Flip[]; firstPage?: number } & Book> = ({ flips, firstPage = 1, ...book }) => (
  <BookContext.Provider value={book}>
    <ReaderInner flips={flips} firstPage={firstPage} />
  </BookContext.Provider>
);

const ReaderInner: React.FC<{ flips: Flip[]; firstPage: number }> = ({ flips, firstPage }) => {
  const frame = useCurrentFrame();
  const book = useBook();

  // Cuántas hojas se han pasado ya y cuánto lleva la que se está pasando.
  let done = 0;
  let t = -1;
  for (const f of flips) {
    if (frame >= f.start + f.duration) done++;
    else if (frame >= f.start) t = (frame - f.start) / f.duration;
  }
  const left = firstPage + done * 2;
  const right = left + 1;
  const pageNo = t >= 0 ? left + 2 : left;

  const lift = (deg: number) => Math.sin((Math.abs(deg) * Math.PI) / 180);

  let flipLayer: React.ReactNode = null;
  if (t >= 0) {
    if (t < 0.5) {
      // 1.ª mitad: la hoja derecha se levanta sobre el lomo
      const angle = -90 * easeInSine(t / 0.5);
      flipLayer = (
        <>
          <PageImg n={right + 2} side="right">
            <div style={{ position: "absolute", inset: 0, background: spineShadow(true, 0.36 * lift(angle)) }} />
          </PageImg>
          <PageImg
            n={right}
            side="right"
            style={{ transformOrigin: "left center", transform: `rotateY(${angle}deg)` }}
          >
            <div style={{ position: "absolute", inset: 0, background: leafShade(true, 0.3 * lift(angle)) }} />
          </PageImg>
        </>
      );
    } else {
      // 2.ª mitad: el reverso (página nueva) cae en el lado izquierdo
      const angle = 90 - 90 * easeOutSine((t - 0.5) / 0.5);
      flipLayer = (
        <>
          <PageImg n={right + 2} side="right" />
          <PageImg n={left} side="left">
            <div style={{ position: "absolute", inset: 0, background: spineShadow(false, 0.36 * lift(angle)) }} />
          </PageImg>
          <PageImg
            n={left + 2}
            side="left"
            style={{ transformOrigin: "right center", transform: `rotateY(${angle}deg)` }}
          >
            <div style={{ position: "absolute", inset: 0, background: leafShade(false, 0.3 * lift(angle)) }} />
          </PageImg>
        </>
      );
    }
  }

  return (
    <div style={{ width: W, height: H, background: "#080808", position: "relative", overflow: "hidden", fontFamily: inter }}>
      {/* Barra de arriba */}
      <div
        style={{
          position: "absolute", top: 0, left: 0, right: 0, height: TOP, background: "#0D0D0D",
          display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 20px",
          color: "#F0F0F0", fontSize: 14, fontWeight: 500,
        }}
      >
        <span>{book.title}</span>
        <span style={{ border: "1px solid #44FFFFFF", borderColor: "rgba(255,255,255,0.27)", borderRadius: 6, padding: "6px 18px", fontSize: 13 }}>
          Cerrar
        </span>
      </div>

      {/* Doble página + lomo */}
      <div style={{ position: "absolute", inset: 0, perspective: 2600 }}>
        {t < 0 || t >= 0.5 ? null : <PageImg n={left} side="left" />}
        {t < 0 ? (
          <>
            <PageImg n={left} side="left" />
            <PageImg n={right} side="right" />
          </>
        ) : null}
        {flipLayer}
        <div style={{ position: "absolute", top: TOP + 8, left: CENTER - 0.5, width: 1, height: PAGE_H, background: "rgba(0,0,0,0.25)" }} />
      </div>

      {/* Barra de abajo */}
      <div
        style={{
          position: "absolute", bottom: 0, left: 0, right: 0, height: BOTTOM, background: "#0D0D0D",
          display: "flex", alignItems: "center", justifyContent: "space-between", padding: "0 24px",
          color: "#F0F0F0", fontSize: 13,
        }}
      >
        <span style={{ display: "flex", gap: 34, alignItems: "center" }}>
          <span style={{ fontSize: 16 }}>‹</span>
          <span>{pageNo + 1} / {book.totalPages}</span>
          <span style={{ fontSize: 16 }}>›</span>
        </span>
        <span style={{ display: "flex", gap: 8 }}>
          {["1 pág.", "Zen"].map((b) => (
            <span key={b} style={{ border: "1px solid rgba(255,255,255,0.27)", borderRadius: 6, padding: "5px 12px", fontSize: 12 }}>
              {b}
            </span>
          ))}
        </span>
      </div>
    </div>
  );
};
