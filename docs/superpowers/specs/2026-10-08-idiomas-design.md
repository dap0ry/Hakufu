# Hakufu en español e inglés — diseño

Fecha: 2026-10-08 · Rama: `feat/idiomas` (sale de `feat/actualizaciones`) · Jira: HMR

## Objetivo

Toda la app en español **o** inglés, elegible en Ajustes y con cambio al momento. Sale como
`v0.11.0-beta.4` (pre-release) para que Dani lo reciba con el botón de actualizar.

## Decisiones (Dani, 08/10/2026)

- **Ajustes → Idioma**: «Español» / «English». Cambia **al momento**, sin reiniciar.
- **Primera vez**: idioma del sistema (`es*` → español; cualquier otro → inglés).
  Instalaciones que ya tienen datos (biblioteca, uso o carpeta elegida) se quedan en español.
- Se traduce **todo lo visible**: las 14 vistas, MainWindow, textos de ViewModels y servicios
  (estados, errores, avisos), barra de actualizaciones, perfil y la imagen exportada, atajos,
  guía de Ayuda y texto legal. Números y fechas con la cultura del idioma.
- **Fuera de alcance**: capturas de la guía de Ayuda en inglés (siguen siendo de la app en
  español); traducir los nombres de archivo de mangas (son del usuario).

## Arquitectura

```
Assets/i18n/<area>.es.json, <area>.en.json   ← textos por pantalla/área, clave → texto (EmbeddedResource)
Services/Localizer.cs                        ← singleton: idioma actual, Get(key), Format(key, args), Culture,
                                               SetLanguage(lang), Observe(key) (IObservable<string>), LanguageChanged
Services/T.cs (MarkupExtension)              ← {i18n:T settings.title} → binding a Localizer.Observe(key)
AppDataStore.Language                        ← "es" | "en" | "" (vacío = decidir al arrancar)
```

- Claves con prefijo del área: `home.*`, `library.*`, `settings.*`… `common.*` para lo compartido
  («← Inicio», «Cancelar», «Guardar»…). Formatos con `{0}`: `Localizer.Format("profile.since", fecha)`.
- Clave que falta → se muestra la clave (y el test de paridad lo impide).
- **Vistas**: `Text="{i18n:T key}"`, el `Observe` emite el texto nuevo al cambiar idioma: el cambio
  es instantáneo sin recrear la vista.
- **ViewModels**: usan `L.Get/Format` y `L.Culture`. Al cambiar de idioma, `NavigationService`
  recarga la pantalla actual (misma pantalla, mismo parámetro) para recalcular sus textos; los VM
  persistentes (barra de actualizaciones, MainWindow) se refrescan con `LanguageChanged`.
- Diálogos de sistema (selector de carpeta, guardar PNG): títulos y filtros también traducidos.

## Errores y casos raros

- JSON corrupto o ausente en un idioma → se cae al español; nunca rompe el arranque.
- Textos largos en inglés que no caben: revisar a mano con capturas de cada pantalla en inglés.

## Pruebas

- Paridad: toda clave de `*.es.json` existe en `*.en.json` y al revés; ninguna vacía.
- Ninguna vista `.axaml` tiene texto visible en español a mano (test que recorre los atributos
  `Text`, `Content`, `ToolTip.Tip`, `Watermark`, `BackText`, `Header`… y solo admite bindings,
  `{i18n:T …}` o una lista corta permitida: «Hakufu», «H», símbolos).
- `Localizer`: idioma por defecto, `Format`, cambio de idioma emite en `Observe`.
- Smoke en inglés: todas las vistas se pintan con `Language = "en"` y muestran textos en inglés.
- Capturas headless de cada pantalla en inglés para revisión visual (no se commitean).
