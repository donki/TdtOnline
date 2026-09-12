# Changelog — TDT Online

## 2026.09.12.1 — Favoritos, Guía EPG, Último canal y Acerca de

- **Canales favoritos:** posibilidad de marcar/desmarcar canales (con pulsación larga en tarjeta/mando o desde el reproductor con tecla de favoritos) y categoría dinámica inicial «⭐ Favoritos».
- **Guía de programación (EPG):** integración con el servicio EPG de TDTChannels (`TV.json`), mostrando el programa en emisión actual tanto en las tarjetas de canal como en el reproductor a pantalla completa con rango horario y descripción.
- **Persistencia de último canal:** recuerda el canal reproducido previamente y permite reanudarlo directamente desde la cabecera.
- **Pantalla «Acerca de» canónica:** diálogo sOCratic adaptado para móvil y Android TV accesible mediante botón en cabecera o tecla `Info` del mando, con versión, autor Josep Solà (`jsoladelarosa@gmail.com`), licencia MIT y aviso de privacidad y emisiones abiertas.
- **Ficha de Play Store y aclaración de catálogo:** redactada la ficha oficial en `GooglePlayConsole/TdtOnline/ficha.md` con política de transparencia sobre canales abiertos (exclusión de plataformas con DRM cerrado).

## 2026.09.11.1 — Primera versión

- Lista de canales de TDTChannels (Apache 2.0) por categorías, con logotipos y caché de un día.
- Reproducción en directo con ExoPlayer (Media3, HLS), con paso automático a la siguiente
  dirección si la principal falla.
- Misma interfaz para móvil, tablet y Android TV (foco con la cruceta, `LEANBACK_LAUNCHER`,
  banner).
