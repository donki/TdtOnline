using System.Globalization;

namespace TdtOnline.Localization;

/// <summary>Textos en español e ingles (constitucion general seccion 1, constitucion mobile). Sigue al idioma del sistema.</summary>
public static class Loc
{
    private static readonly Dictionary<string, string> English = new()
    {
        ["AppTitle"] = "TDT Online",
        ["Loading"] = "Loading channels…",
        ["LoadFailed"] = "Could not load the channel list. Check the connection and press refresh.",
        ["ChannelsCount"] = "{0} channels",
        ["PlayFailed"] = "This channel cannot be played right now.",
        ["Trying"] = "Trying another stream…",
        ["Refresh"] = "Refresh",
        ["Favorites"] = "⭐ Favorites",
        ["AllChannels"] = "All",
        ["SearchHint"] = "Search channel…",
        ["NoResults"] = "No channel matches «{0}».",
        ["Guide"] = "TV guide",
        ["Settings"] = "Settings",
        ["ListsTitle"] = "Channel lists",
        ["ListsHint"] = "Downloaded every time the app starts. The first list sets the order; the others add channels and backup streams. Accepted: tdt-canales JSON, TDTChannels JSON and M3U/M3U8.",
        ["ListUrlHint"] = "https://…/list.m3u8",
        ["ListAdded"] = "List added",
        ["ListInvalid"] = "That is not a valid http(s) address.",
        ["ListExists"] = "That list is already there.",
        ["ListRemoved"] = "List removed",
        ["ListsRestored"] = "Default list restored",
        ["RefreshDone"] = "Lists will be downloaded again now.",
        ["DefaultListLabel"] = "default list",
        ["ListsFailed"] = "Could not download: {0}",
        ["ActionsHint"] = "↻ downloads the lists again · ↶ goes back to the default list",
        ["GuideEmpty"] = "No programme guide available right now.",
        ["GuideLoading"] = "Loading the programme guide…",
        ["NowLabel"] = "Now",
        ["FavoriteAdded"] = "Added to favorites: {0}",
        ["FavoriteRemoved"] = "Removed from favorites: {0}",
        ["FavoriteTip"] = "No favorites yet. Tap the star on a channel, or hold OK on the remote, to add it here.",
        ["LastChannel"] = "Last watched: {0}",
        ["About"] = "About",
        ["AboutTitle"] = "About",
        ["AboutDescription"] = "The DTT channels over the internet, on the phone, the tablet and Android TV.",
        ["Publisher"] = "Socratic",
        ["ContactTitle"] = "Contact",
        ["ContactHint"] = "Questions, bugs and ideas are welcome.",
        ["SectionLanguage"] = "Language",
        ["LanguageHint"] = "The language applies right away.",
        ["SpanishButton"] = "🇪🇸 Español",
        ["EnglishButton"] = "🇺🇸 English",
        ["PrivacyTitle"] = "Privacy",
        ["PrivacyText"] = "TDT Online downloads the channel list from {0} (github.com/donki/tdt-canales, built from TDTChannels and Free-TV, community lists with the streams each broadcaster publishes openly) plus any list you add in Settings, and just opens them: it hosts and relays nothing. For DMAX, which publishes no fixed address, it asks the broadcaster's own service for the live stream each time, exactly as its website does, with no account. Only open, unencrypted streams are listed. Favorites and the last channel stay on the device. There are no accounts, no ads and no analytics.",
        ["LicenseTitle"] = "License",
        ["LicenseText"] = "Free software released under the MIT license. The source code can be used, studied and modified by anyone. Third-party components and their licenses are listed in THIRD-PARTY-NOTICES.md.",
        ["LicenseLine"] = "MIT License · Copyright © 2026 Socratic",
        ["LegalTitle"] = "Legal notice",
        ["LegalText1"] = "This software is provided \"as is\", without warranty of any kind, express or implied.",
        ["LegalText2"] = "In no event shall the authors be liable for any claim, damages or other liability arising from the use of this software.",
        ["WarningText"] = "⚠️ Use at your own risk",
        ["Close"] = "Close",
        ["NowPlaying"] = "Now: {0}",
    };

    private static readonly Dictionary<string, string> Spanish = new()
    {
        ["AppTitle"] = "TDT Online",
        ["Loading"] = "Cargando canales…",
        ["LoadFailed"] = "No se ha podido cargar la lista de canales. Comprueba la conexión y pulsa actualizar.",
        ["ChannelsCount"] = "{0} canales",
        ["PlayFailed"] = "Este canal no se puede ver ahora mismo.",
        ["Trying"] = "Probando otra emisión…",
        ["Refresh"] = "Actualizar",
        ["Favorites"] = "⭐ Favoritos",
        ["AllChannels"] = "Todos",
        ["SearchHint"] = "Buscar canal…",
        ["NoResults"] = "Ningún canal coincide con «{0}».",
        ["Guide"] = "Parrilla",
        ["Settings"] = "Ajustes",
        ["ListsTitle"] = "Listas de canales",
        ["ListsHint"] = "Se descargan cada vez que arranca la aplicación. La primera manda en el orden; las demás añaden canales y emisiones de repuesto. Se admiten JSON de tdt-canales, JSON de TDTChannels y M3U/M3U8.",
        ["ListUrlHint"] = "https://…/lista.m3u8",
        ["ListAdded"] = "Lista añadida",
        ["ListInvalid"] = "Eso no es una dirección http(s) válida.",
        ["ListExists"] = "Esa lista ya está.",
        ["ListRemoved"] = "Lista quitada",
        ["ListsRestored"] = "Lista por defecto restaurada",
        ["RefreshDone"] = "Las listas se descargan de nuevo ahora.",
        ["DefaultListLabel"] = "lista por defecto",
        ["ListsFailed"] = "No se ha podido descargar: {0}",
        ["ActionsHint"] = "↻ vuelve a descargar las listas · ↶ vuelve a la lista por defecto",
        ["GuideEmpty"] = "Ahora mismo no hay guía de programación.",
        ["GuideLoading"] = "Cargando la guía de programación…",
        ["NowLabel"] = "Ahora",
        ["FavoriteAdded"] = "Añadido a favoritos: {0}",
        ["FavoriteRemoved"] = "Eliminado de favoritos: {0}",
        ["FavoriteTip"] = "Aún no hay favoritos. Toca la estrella de un canal, o mantén pulsado OK en el mando, para traerlo aquí.",
        ["LastChannel"] = "Último canal: {0}",
        ["About"] = "Acerca de",
        ["AboutTitle"] = "Acerca de",
        ["AboutDescription"] = "Los canales de la TDT por internet, en el móvil, la tablet y Android TV.",
        ["Publisher"] = "Socratic",
        ["ContactTitle"] = "Contacto",
        ["ContactHint"] = "Dudas, fallos e ideas son bienvenidos.",
        ["SectionLanguage"] = "Idioma",
        ["LanguageHint"] = "El idioma se aplica de inmediato.",
        ["SpanishButton"] = "🇪🇸 Español",
        ["EnglishButton"] = "🇺🇸 English",
        ["PrivacyTitle"] = "Privacidad",
        ["PrivacyText"] = "TDT Online descarga la lista de canales de {0} (github.com/donki/tdt-canales, generada a partir de TDTChannels y Free-TV, listas de la comunidad con las emisiones que cada cadena publica en abierto) y las listas que añadas en Ajustes, y se limita a abrirlas: no aloja ni reemite nada. Para DMAX, que no publica dirección fija, pide la emisión al servicio de la propia cadena cada vez, igual que hace su web, sin cuenta alguna. Solo se listan emisiones abiertas y sin cifrar. Los favoritos y el último canal se quedan en el dispositivo. No hay cuentas, ni anuncios, ni analítica.",
        ["LicenseTitle"] = "Licencia",
        ["LicenseText"] = "Software libre publicado bajo la licencia MIT. Cualquiera puede usar, estudiar y modificar el código fuente. Los componentes de terceros y sus licencias están en THIRD-PARTY-NOTICES.md.",
        ["LicenseLine"] = "MIT License · Copyright © 2026 Socratic",
        ["LegalTitle"] = "Aviso legal",
        ["LegalText1"] = "Este software se entrega «tal cual», sin garantías de ningún tipo, expresas o implícitas.",
        ["LegalText2"] = "En ningún caso los autores serán responsables de reclamaciones, daños u otras responsabilidades derivadas del uso de este software.",
        ["WarningText"] = "⚠️ Uso bajo su propio riesgo",
        ["Close"] = "Cerrar",
        ["NowPlaying"] = "Ahora: {0}",
    };

    /// <summary>Idioma elegido en «Acerca de» («es» / «en»); vacio = el del sistema.</summary>
    public static string Override { get; set; } = string.Empty;

    public static string Language =>
        Override.Length > 0 ? Override : CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? "es" : "en";

    public static string Get(string key)
    {
        var table = Language == "es" ? Spanish : English;
        return table.TryGetValue(key, out var value) ? value : English.GetValueOrDefault(key, string.Empty);
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
