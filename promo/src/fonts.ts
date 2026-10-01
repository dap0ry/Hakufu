import { loadFont as loadLocal } from "@remotion/fonts";
import { loadFont as loadInter } from "@remotion/google-fonts/Inter";
import { staticFile } from "remotion";

// Las mismas fuentes que la app: Inter (interfaz) y la condensada de los rótulos.
export const { fontFamily: inter } = loadInter("normal", {
  weights: ["400", "500", "600", "700", "900"],
  subsets: ["latin"],
});

export const display = "Hakufu Display";
export const displaySemi = "Hakufu Display Semi";

loadLocal({ family: display, url: staticFile("fonts/HakufuDisplay-Black.ttf") });
loadLocal({ family: displaySemi, url: staticFile("fonts/HakufuDisplaySemi-Bold.ttf") });
