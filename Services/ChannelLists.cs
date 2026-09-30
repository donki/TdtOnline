using TdtOnline.Models;

namespace TdtOnline.Services;

/// <summary>
/// Reglas puras de la pantalla principal y de Ajustes: buscar, juntar favoritos, recorrer todos los
/// canales una vez y validar las direcciones de las listas. Sin nada de Android, para poder probarlas.
/// </summary>
public static class ChannelLists
{
    /// <summary>Resultado de comprobar una lista nueva en Ajustes.</summary>
    public enum NewListCheck
    {
        Ok,
        Invalid,
        Exists,
    }

    /// <summary>Minusculas y sin acentos, para que «aragon» encuentre «Aragón TV».</summary>
    public static string Normalize(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }

    /// <summary>Todos los canales sin repetir (un canal puede estar en varias categorias).</summary>
    public static List<Channel> AllChannels(IEnumerable<Category> categories)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<Channel>();
        foreach (var cat in categories)
        {
            foreach (var ch in cat.Channels)
            {
                if (seen.Add(ch.Name))
                    all.Add(ch);
            }
        }

        return all;
    }

    /// <summary>Los favoritos, en el orden de la lista y cada uno una vez.</summary>
    public static List<Channel> Favorites(IEnumerable<Category> categories, ISet<string> favorites)
    {
        var favChannels = new List<Channel>();
        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cat in categories)
        {
            foreach (var ch in cat.Channels)
            {
                if (favorites.Contains(ch.Name) && addedNames.Add(ch.Name))
                    favChannels.Add(ch);
            }
        }

        return favChannels;
    }

    /// <summary>Canales cuyo nombre contiene el texto, sin mirar acentos, espacios ni mayusculas.</summary>
    public static List<Channel> Search(IEnumerable<Category> categories, string text)
    {
        var key = Normalize(text);
        return AllChannels(categories).Where(ch => Normalize(ch.Name).Contains(key, StringComparison.Ordinal)).ToList();
    }

    /// <summary>Servidor y ruta de una direccion, para los avisos (sin parametros).</summary>
    public static string ShortUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host + u.AbsolutePath : url;

    /// <summary>
    /// Las listas guardadas (una por linea), en orden. Vacio nunca: sin ninguna vuelve la de tdt-canales.
    /// </summary>
    public static IReadOnlyList<string> ParseListUrls(string? raw)
    {
        var urls = string.IsNullOrWhiteSpace(raw)
            ? []
            : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        return urls.Count > 0 ? urls : [ChannelCatalog.DefaultListUrl];
    }

    /// <summary>Una direccion nueva vale si es http(s) absoluta y no esta ya (sin mirar mayusculas).</summary>
    public static NewListCheck CheckNewList(string text, IEnumerable<string> existing)
    {
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            return NewListCheck.Invalid;

        return existing.Contains(text, StringComparer.OrdinalIgnoreCase) ? NewListCheck.Exists : NewListCheck.Ok;
    }

    /// <summary>Pone o quita un favorito; devuelve si queda como favorito. Un nombre vacio no cambia nada.</summary>
    public static bool ToggleFavorite(ISet<string> favorites, string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            return false;

        if (favorites.Remove(channelName))
            return false;

        favorites.Add(channelName);
        return true;
    }
}
