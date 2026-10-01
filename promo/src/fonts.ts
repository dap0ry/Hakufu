import { loadFont } from "@remotion/fonts";
import { staticFile } from "remotion";

// Fuentes dentro del proyecto (el render no depende de internet): Inter
// (interfaz, variable 100–900) y la condensada de los rótulos de la app.
export const inter = "Inter";
export const display = "Hakufu Display";
export const displaySemi = "Hakufu Display Semi";

loadFont({ family: inter, url: staticFile("fonts/Inter.ttf"), weight: "100 900" });
loadFont({ family: display, url: staticFile("fonts/HakufuDisplay-Black.ttf") });
loadFont({ family: displaySemi, url: staticFile("fonts/HakufuDisplaySemi-Bold.ttf") });
