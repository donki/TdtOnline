# TDT Online

Los canales de la TDT por internet, en el móvil, la tablet y **Android TV**: lista de canales
por categoría con buscador y favoritos, logotipos, guía de programación y reproducción en directo.

## Cómo funciona

- **De dónde salen los canales.** De [donki/tdt-canales](https://github.com/donki/tdt-canales)
  (`canales.json`), una lista propia que `build.py` genera a partir de
  [TDTChannels](https://github.com/LaQuay/TDTChannels) (Apache 2.0; las emisiones **oficiales** de
  cada cadena) y del grupo «Spain» de [Free-TV/IPTV](https://github.com/Free-TV/IPTV), comprobada
  dirección a dirección desde España. La aplicación se la baja **cada vez que arranca** (con copia
  para cuando no hay red) y no aloja ni reemite nada: abre lo que cada cadena publica.
- **Ajustes.** Se pueden añadir más listas (JSON de tdt-canales, JSON de TDTChannels o M3U/M3U8),
  quitarlas, volver a descargarlas ahora mismo o restaurar la lista por defecto. La primera lista
  manda en el orden de categorías; las demás añaden canales y emisiones de repuesto.
- **DMAX.** Su web no publica dirección fija: la pide a la plataforma de la cadena en cada
  reproducción (un token anónimo y la información de reproducción del canal; sin cuenta y sin DRM).
  La app hace esas mismas dos peticiones al ir a verlo (`Services/SonicLive.cs`). Solo aparece si la
  comprobación de arranque ve el directo encendido; la cadena lo tiene apagado en su web desde al
  menos septiembre de 2026, así que de momento no sale.
- **Lo que no está.** Antena 3, laSexta, Neox, Nova, Mega, Telecinco, Cuatro, FDF, Energy, Divinity,
  Be Mad y Boing no publican emisión abierta en ninguna lista pública: solo se ven en sus propias
  plataformas (Atresplayer, Mitele), con registro y DRM. Por eso no salen.
- **Parrilla.** Una fila por canal con lo que emite ahora y lo que viene después (guía de
  TDTChannels); OK sobre el canal o sobre un programa lo abre. Se abre con el botón de rejilla de la
  cabecera o con el botón rojo del mando.
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
