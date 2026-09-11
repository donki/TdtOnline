using System.Globalization;

namespace TdtOnline.Localization;

/// <summary>Textos en español e ingles (constitucion, seccion 7). Sigue al idioma del sistema.</summary>
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
    };

    public static string Get(string key)
    {
        var table = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "es" ? Spanish : English;
        return table.TryGetValue(key, out var value) ? value : English.GetValueOrDefault(key, string.Empty);
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
