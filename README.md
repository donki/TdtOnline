# TDT Online

Los canales de la TDT por internet, en el móvil, la tablet y **Android TV**: lista de canales
por categoría con buscador y favoritos, logotipos, guía de programación y reproducción en directo.

## Cómo funciona

- **De dónde salen los canales.** De la lista de [TDTChannels](https://github.com/LaQuay/TDTChannels)
  (Apache 2.0), mantenida por la comunidad, que recoge las emisiones **oficiales** de cada cadena
  en internet (RTVE, autonómicas, temáticas…). La aplicación se la baja al arrancar, la guarda en
  caché un día y no aloja ni reemite nada: abre lo que cada cadena publica. Solo se enseñan los
  canales con alguna dirección HLS/DASH utilizable; las plantillas de servidores de anuncios se
  descartan.
- **Segunda lista.** El grupo «Spain» de [Free-TV/IPTV](https://github.com/Free-TV/IPTV) se mezcla
  con la anterior (las direcciones oficiales van primero). Como esa lista trae bastantes
  direcciones muertas o que exigen sesión, cada una se comprueba en segundo plano al arrancar y
  solo se enseñan las que responden; el resultado se guarda un día.
- **DMAX.** Su web no publica dirección fija: la pide a la plataforma de la cadena en cada
  reproducción (un token anónimo y la información de reproducción del canal; sin cuenta y sin DRM).
  La app hace esas mismas dos peticiones al ir a verlo (`Services/SonicLive.cs`). Solo aparece si la
  comprobación de arranque ve el directo encendido; la cadena lo tiene apagado en su web desde al
  menos septiembre de 2026, así que de momento no sale.
- **Lo que no está.** Antena 3, laSexta, Neox, Nova, Mega, Telecinco, Cuatro, FDF, Energy, Divinity,
  Be Mad y Boing no publican emisión abierta en ninguna lista pública: solo se ven en sus propias
  plataformas (Atresplayer, Mitele), con registro y DRM. Por eso no salen.
- **Reproductor.** ExoPlayer (Media3) con HLS. Si la dirección principal de un canal falla, se
  prueba la siguiente antes de dar el aviso.
- **Una sola interfaz** para móvil y tele: categorías en pastillas y canales en rejilla; en la tele
  se navega con la cruceta del mando (foco con borde de marca), en el móvil se toca. El APK lleva el
  `LEANBACK_LAUNCHER` y el banner para el lanzador de Android TV, y declara que ni la pantalla
  táctil ni Leanback son obligatorias.
- Es **.NET para Android** sin MAUI: en una tele el foco tiene que ir de tarjeta en tarjeta y los
  controles nativos lo hacen solos.

## Compilar y probar

```
dotnet build -c Debug -f net10.0-android36.0 -p:AndroidKeyStore=true -p:AndroidSigningStorePass=<pass> -p:AndroidSigningKeyPass=<pass>
adb install -r bin\Debug\net10.0-android36.0\com.socratic.tdtonline-Signed.apk
```

Emulador de Android TV (el SDK trae las imágenes):

```
sdkmanager "system-images;android-34;android-tv;x86_64"
avdmanager create avd -n tv -k "system-images;android-34;android-tv;x86_64" -d tv_1080p
emulator -avd tv
```

## Funcionalidades completadas (v2026.09.12.1)

- Canales favoritos con pulsación larga / tecla de mando y categoría inicial dinámica «⭐ Favoritos».
- Guía de programación (EPG) en directo desde TDTChannels (`TV.json`), visible en tarjetas y reproductor.
- Persistencia de último canal visto para reanudación rápida.
- Diálogo canónico «Acerca de» (autor Josep Solà, Socratic, licencia MIT, transparencia de emisiones abiertas).
- Ficha oficial para Google Play redactada en `Mobile/GooglePlayConsole/TdtOnline/ficha.md`.

## Pendiente

- Probar en un Android TV real o en el emulador con la cruceta del mando a distancia.
- Generar capturas de pantalla de la interfaz de Android TV para la ficha de Google Play.
