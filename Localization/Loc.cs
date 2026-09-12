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
        ["Source"] = "Channel list by {0} (Apache 2.0) · official broadcaster streams · no ads, no trackers",
        ["PlayFailed"] = "This channel cannot be played right now.",
        ["Trying"] = "Trying another stream…",
        ["Refresh"] = "Refresh",
        ["Favorites"] = "⭐ Favorites",
        ["FavoriteAdded"] = "Added to favorites: {0}",
        ["FavoriteRemoved"] = "Removed from favorites: {0}",
        ["FavoriteTip"] = "Long-press to add/remove favorites",
        ["LastChannel"] = "Last watched: {0}",
        ["About"] = "About",
        ["AboutAuthor"] = "Developed by Socratic · Josep Solà\n(jsoladelarosa@gmail.com)",
        ["AboutLicense"] = "MIT License · Open Source Project",
        ["AboutDescription"] = "Official live DTT channels over the internet. Channel list maintained by the community at TDTChannels (Apache 2.0).\n\n• Privacy first: 100% local processing, no accounts, no app ads, and no tracking or telemetry.\n• Broadcaster policy: Only open, unencrypted streams (HLS/DASH) are included. Channels with proprietary DRM or closed paywalls are not supported.",
        ["Close"] = "Close",
        ["NowPlaying"] = "Now: {0}",
    };

    private static readonly Dictionary<string, string> Spanish = new()
    {
        ["AppTitle"] = "TDT Online",
        ["Loading"] = "Cargando canales…",
        ["LoadFailed"] = "No se ha podido cargar la lista de canales. Comprueba la conexión y pulsa actualizar.",
        ["ChannelsCount"] = "{0} canales",
        ["Source"] = "Lista de canales de {0} (Apache 2.0) · emisiones oficiales de cada cadena · sin anuncios ni rastreadores",
        ["PlayFailed"] = "Este canal no se puede ver ahora mismo.",
        ["Trying"] = "Probando otra emisión…",
        ["Refresh"] = "Actualizar",
        ["Favorites"] = "⭐ Favoritos",
        ["FavoriteAdded"] = "Añadido a favoritos: {0}",
        ["FavoriteRemoved"] = "Eliminado de favoritos: {0}",
        ["FavoriteTip"] = "Mantén pulsado para añadir/quitar de favoritos",
        ["LastChannel"] = "Último canal: {0}",
        ["About"] = "Acerca de",
        ["AboutAuthor"] = "Desarrollado por Socratic · Josep Solà\n(jsoladelarosa@gmail.com)",
        ["AboutLicense"] = "Licencia MIT · Código Abierto",
        ["AboutDescription"] = "Canales oficiales de la TDT en directo por internet. Lista mantenida por la comunidad en TDTChannels (Apache 2.0).\n\n• Privacidad: Procesamiento 100% local, sin cuentas de usuario, sin anuncios propios y sin herramientas de rastreo ni analítica.\n• Política de emisiones: Solo se incluyen canales con emisión abierta y libre (HLS/DASH). Cadenas comerciales con DRM cerrado o plataformas propietarias quedan excluidas.",
        ["Close"] = "Cerrar",
        ["NowPlaying"] = "Ahora: {0}",
    };

    public static string Get(string key)
    {
        var table = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Spanish : English;
        return table.TryGetValue(key, out var value) ? value : English.GetValueOrDefault(key, string.Empty);
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
