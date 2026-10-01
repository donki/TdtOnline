# Solicitud de acceso a producción en Google Play — TDT Online

Respuestas para el cuestionario de Play Console › **Panel › Solicitar acceso a producción**, en
catalán (el idioma de la consola). Cada texto cabe en los 300 caracteres del formulario; el número
entre paréntesis es su longitud. Constitución Mobile §11. **Última actualización: 2026-10-01**
(versión 2026.09.30.0). Estado en Play: prueba cerrada (alpha 2026.09.13.1 publicada; borrador con versionCode 2026093000; en la pista desde el 2026-09-13).

> Lo marcado con ⚠ no lo puedo saber yo: compruébalo en la consola antes de enviarlo y cámbialo si
> no es así.
>
> - ⚠ En alpha publicada solo está la 2026.09.13.1. El borrador (versionCode 2026093000) se llama en la consola **«SMS Forwarder 2026.09.28.00»**: cambia el nombre de la versión antes de enviarla a revisión.
> - ⚠ Clave de subida propia (`socratic-tdtonline-upload.keystore`), no la compartida.

---

## Informació sobre la prova tancada

**Com has reclutat usuaris per a la prova tancada?** (288) ⚠ *comprueba que la probaste también en el móvil (en el CHANGELOG solo constan la tele Xiaomi y el emulador).*

```
He afegit a la prova tancada quatre grups públics de Google de verificadors voluntaris (comunitats d'intercanvi de proves de 12 persones durant 14 dies). No he fet servir cap proveïdor de pagament. També l'he provada en un mòbil real, en un televisor Xiaomi amb Android TV i a l'emulador.
```

**Fins a quin punt t'ha resultat fàcil reclutar verificadors?** — Propuesta: **Ni fàcil ni difícil** (los grupos públicos dan el número, pero participan poco).

**Descriu la implicació dels verificadors** (227) ⚠ *comprueba en Estadísticas / Prova tancada que de verdad la abrieron; si no hay datos, quita la parte de las funciones.*

```
Els verificadors han instal·lat l'app i han obert canals en directe, el cercador, els preferits i la graella. Un usuari real la faria servir sobretot a la tele amb Android TV i el comandament, que pocs verificadors deuen tenir.
```

**Resum dels suggeriments i com els has recollit** (258) ⚠ *si algún verificador dejó comentarios (en la consola o por correo), menciónalos.*

```
Pocs comentaris escrits dels verificadors; els he recollit des de la consola de Play i GitHub. Les millores han sortit de les meves proves en un televisor amb Android TV i del banc de proves: el comandament, la graella lenta, la lletra gran i el botó enrere.
```

## Informació sobre l'aplicació

**A quin públic objectiu va dirigida?** (185)

```
Persones que volen veure els canals de la TDT que les cadenes emeten en obert per internet, al mòbil, la tauleta o la tele amb Android TV, sense registre ni compte. L'app no té anuncis.
```

**Com proporciona valor als usuaris?** (253)

```
Reuneix en una llista els canals que les cadenes emeten en obert per internet, amb cercador, preferits, logotips i graella de programació, i els reprodueix en directe al mòbil o a la tele amb el comandament. No allotja ni retransmet res ni recull dades.
```

**Instal·lacions esperades el primer any** — Propuesta: **0 - 10.000** (app nueva, sin promoción).

## Preparació per a la producció

**Quins canvis has fet en funció de la prova tancada?** (254) ⚠ *excepto la graella al instante, todo va en el borrador (versionCode 2026093000): envíalo a revisión antes.*

```
La graella s'obre a l'instant, el focus torna al canal en sortir del reproductor, botó enrere i gestor d'errors que no tanquen l'app, capçalera bé amb lletra gran, el comandament ja no s'escapa de la graella, la guia no es perd per un error i 108 proves.
```

**Com has decidit que està preparada per a producció?** (242) ⚠ *comprueba en Qualitat › Android Vitals que no hay fallos; si los hay, quita «sense tancaments a la consola».*

```
Les 108 proves automàtiques passen totes, l'he provada en un mòbil real amb Android 16 sense errors, els verificadors l'han fet servir 14 dies sense tancaments a la consola i la fitxa, la privadesa i la seguretat de les dades estan completes.
```
