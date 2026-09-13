# Changelog — TDT Online

## 2026.09.13.2 — Parrilla al instante y foco al volver del reproductor

- **Parrilla:** se abre con la guía ya puesta. Cada pantalla creaba su propia guía y volvía a leer
  y analizar los 3,7 MB; en la tele Xiaomi la parrilla decía «no hay guía» durante un minuto largo.
  Ahora la guía es una sola para toda la aplicación, y mientras carga dice «Cargando la guía…».
- **Foco:** al volver del reproductor, el foco vuelve a la tarjeta del canal que se estaba viendo,
  en vez de irse a la primera de la rejilla.

## 2026.09.13.0 — Ya no se cierra al moverse con el mando

- **Corregido:** al pasar la cruceta por las categorías la aplicación se cerraba
  (`IllegalStateException: Cannot call this method while RecyclerView is computing a layout or
  scrolling`): la categoría se abría dentro del propio cambio de foco, en mitad del pase de layout
  de la lista, y avisar de cambios ahí está prohibido. Ahora se abre en el siguiente ciclo. Visto
  en la tele Xiaomi y reproducido en el emulador de Android TV.

## 2026.09.12.4 — Lista propia en GitHub y ajustes

- **Lista propia:** los canales se descargan cada vez que arranca la app de
  [donki/tdt-canales](https://github.com/donki/tdt-canales) (`canales.json`), generada con
  `build.py` a partir de TDTChannels y de Free-TV ya comprobada. La comprobación de direcciones
  deja de hacerse en el dispositivo; solo queda la del directo de DMAX.
- **Ajustes** (rueda dentada en la cabecera): listas de canales con «+» para añadir direcciones
  (JSON de tdt-canales, JSON de TDTChannels o M3U/M3U8; se reconocen por el contenido), papelera
  para quitarlas, ↻ para volver a descargarlas ahora y ↶ para restaurar la lista por defecto. La
  primera lista manda en el orden; las demás añaden canales y emisiones de repuesto.
- Si una lista no baja y no hay copia, se avisa y se sigue con las demás.

## 2026.09.12.3 — Parrilla de programación

- **Parrilla** (botón de rejilla en la cabecera, o el botón rojo del mando): una fila por canal con
  el programa en curso (con barra de progreso) y los siguientes; favoritos delante. Con la cruceta
  se baja de canal en canal y se recorre cada fila; OK sobre el canal o sobre un programa lo abre.
- Probado en un emulador de Android TV (API 28, 1080p) además de la tablet.

## 2026.09.12.2 — Todos, buscador, Free-TV

- **Grupo «Todos»:** todos los canales en una sola rejilla, después de Favoritos.
- **Buscador:** filtra por nombre entre todos los canales, sin acentos ni mayúsculas; con el mando,
  «Buscar» en el teclado cierra el teclado y pasa el foco a los resultados.
- **Favoritos siempre visible:** el grupo sale aunque esté vacío, con una nota de cómo se llena; la
  estrella de cada tarjeta se ve siempre (apagada o encendida) y se toca para marcar.
- **Segunda lista, Free-TV/IPTV:** se toma su grupo «Spain» y se mezcla con TDTChannels (las
  direcciones oficiales van primero). Antes de entrar, cada dirección se comprueba en segundo plano
  y solo se enseñan las que responden: la lista trae muchas muertas o que exigen sesión. Aporta
  Paramount Network, Negocios, 3/24, TVE Internacional… y repuestos para otros canales.
- **DMAX por su propio servicio:** su web no publica dirección fija; la pide a la plataforma de la
  cadena (dos peticiones públicas, sin cuenta, sin DRM) y la app hace lo mismo al ir a verlo
  (`SonicLive`). Solo aparece si la comprobación de arranque ve el directo encendido: en septiembre
  de 2026 la cadena lo tiene apagado en la web (la lista maestra contesta, los vídeos dan 404),
  así que hoy no sale; saldrá solo cuando lo enciendan.
- **Lo que no hay:** Antena 3, laSexta, Neox, Nova, Mega, Telecinco, Cuatro, FDF, Energy, Divinity,
  Be Mad y Boing no publican emisión abierta en ninguna lista; solo se ven en sus propias
  plataformas (Atresplayer, Mitele), con registro y DRM. No se pueden ofrecer.

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
