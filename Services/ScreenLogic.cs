using TdtOnline.Localization;
using TdtOnline.Models;

namespace TdtOnline.Services;

/// <summary>Lo que hace atras en la pantalla principal.</summary>
public enum HomeBack
{
    /// <summary>Borrar lo escrito en el buscador.</summary>
    ClearSearch,

    /// <summary>Quitar el foco al buscador (y cerrar el teclado).</summary>
    LeaveSearch,

    /// <summary>Nada abierto: ocultar la aplicacion sin cerrarla.</summary>
    HideApp,
}

/// <summary>Que hacer con una tecla de la cruceta en una tarjeta de canal.</summary>
public enum CardKeyAction
{
    /// <summary>Que la resuelva Android.</summary>
    Pass,

    /// <summary>Tragarsela: el foco se queda donde esta.</summary>
    Stay,

    /// <summary>Subir a la pastilla de la categoria que se esta viendo.</summary>
    FocusCategory,
}

/// <summary>Teclas de la cruceta que importan en la rejilla.</summary>
public enum DpadKey { Other, Left, Right, Up, Down }

/// <summary>
/// Logica de la pantalla principal (categorias, rejilla, buscador, ultimo canal): lo que no toca
/// vistas de Android, para probarlo sin dispositivo (General 8.6).
/// </summary>
public static class HomeLogic
{
    /// <summary>
    /// Las pastillas que se ven: Favoritos (siempre la primera, aunque este vacia: si no se ve, no se
    /// sabe que existe), Todos (cada canal una vez) y las categorias de las listas.
    /// </summary>
    public static List<Category> DisplayCategories(IReadOnlyList<Category> raw, ISet<string> favorites) =>
    [
        new Category(Loc.Get("Favorites"), ChannelLists.Favorites(raw, favorites)),
        new Category(Loc.Get("AllChannels"), ChannelLists.AllChannels(raw)),
        .. raw,
    ];

    /// <summary>La pastilla a abrir al rehacer la fila: la que se estaba viendo si sigue, si no la primera.</summary>
    public static int TargetIndex(IReadOnlyList<Category> display, bool maintain, string? currentName)
    {
        if (!maintain || currentName is null)
            return 0;
        for (var i = 0; i < display.Count; i++)
        {
            if (display[i].Name == currentName)
                return i;
        }
        return 0;
    }

    public static Channel? FindChannel(IEnumerable<Category> categories, string name) =>
        categories.SelectMany(c => c.Channels).FirstOrDefault(ch => string.Equals(ch.Name, name, StringComparison.OrdinalIgnoreCase));

    /// <summary>Tantas columnas como quepan a ~160 dp: cuatro en un movil, siete u ocho en una tele.</summary>
    public static int Columns(int widthPixels, float density) => Math.Max(2, (int)(widthPixels / density / 160));

    /// <summary>
    /// La cruceta en una tarjeta. A los lados el foco se queda en la rejilla (Android lo mandaba a una
    /// pastilla cualquiera, que abria otra categoria); arriba, desde la primera fila, vuelve a la
    /// pastilla de la categoria que se esta viendo.
    /// </summary>
    public static CardKeyAction CardKey(int position, int span, int count, DpadKey key)
    {
        var column = position % span;
        return key switch
        {
            DpadKey.Right => column == span - 1 || position == count - 1 ? CardKeyAction.Stay : CardKeyAction.Pass,
            DpadKey.Left => column == 0 ? CardKeyAction.Stay : CardKeyAction.Pass,
            DpadKey.Up when position < span => CardKeyAction.FocusCategory,
            _ => CardKeyAction.Pass,
        };
    }

    /// <summary>Atras: primero el buscador (lo escrito, luego el foco); sin nada abierto, se oculta la app.</summary>
    public static HomeBack Back(string searchText, bool searchHasFocus) =>
        searchText.Trim().Length > 0 ? HomeBack.ClearSearch
        : searchHasFocus ? HomeBack.LeaveSearch
        : HomeBack.HideApp;

    /// <summary>Texto del ultimo canal visto, o null si no hay (la fila queda invisible).</summary>
    public static string? LastChannelText(string? last) =>
        string.IsNullOrWhiteSpace(last) ? null : Loc.Format("LastChannel", last);

    public static string ChannelsCountText(IEnumerable<Category> raw) =>
        Loc.Format("ChannelsCount", raw.Sum(c => c.Channels.Count));

    /// <summary>Lo que se ve al abrir una categoria: sus canales y, si es Favoritos vacio, como se llena.</summary>
    public static (IReadOnlyList<Channel> Channels, string? EmptyHint) ShowCategory(IReadOnlyList<Category> display, int index)
    {
        var channels = display[index].Channels;
        return (channels, index == 0 && channels.Count == 0 ? Loc.Get("FavoriteTip") : null);
    }

    /// <summary>Busqueda por nombre entre todos los canales; sin resultados, lo dice.</summary>
    public static (IReadOnlyList<Channel> Channels, string? EmptyHint) Search(IEnumerable<Category> raw, string text)
    {
        var matches = ChannelLists.Search(raw, text);
        return (matches, matches.Count == 0 ? Loc.Format("NoResults", text) : null);
    }

    /// <summary>Aviso de las listas que no se pudieron bajar, o null si bajaron todas.</summary>
    public static string? FailedListsText(IReadOnlyCollection<string> failed) =>
        failed.Count == 0 ? null : Loc.Format("ListsFailed", string.Join(", ", failed.Select(ChannelLists.ShortUrl)));

    public static string FavoriteToggledText(string channel, bool added) =>
        Loc.Format(added ? "FavoriteAdded" : "FavoriteRemoved", channel);
}

/// <summary>Logica de la parrilla: que canales salen, en que orden y como se pinta cada programa.</summary>
public static class GuideLogic
{
    public const int ProgramsPerChannel = 12;

    /// <summary>Los canales con guia, cada uno una vez, con los favoritos delante.</summary>
    public static List<Channel> Rows(IEnumerable<Category> categories, ISet<string> favorites, Func<string?, bool> hasGuide)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = categories.SelectMany(c => c.Channels).Where(ch => hasGuide(ch.EpgId) && seen.Add(ch.Name)).ToList();
        return all.Where(c => favorites.Contains(c.Name)).Concat(all.Where(c => !favorites.Contains(c.Name))).ToList();
    }

    /// <summary>Texto con la parrilla vacia: aun cargando o sin datos.</summary>
    public static string EmptyText(bool guideLoaded) => Loc.Get(guideLoaded ? "GuideEmpty" : "GuideLoading");

    /// <summary>Una tarjeta de programa: su hora (con «Ahora» si se esta emitiendo) y lo que lleva (0..1000).</summary>
    public static (string Time, bool Current, int Progress) ProgramCell(EpgProgram p, long nowEpochSeconds)
    {
        var current = p.StartEpochSeconds <= nowEpochSeconds && nowEpochSeconds < p.EndEpochSeconds;
        if (!current)
            return (p.TimeRange, false, 0);
        var length = Math.Max(1, p.EndEpochSeconds - p.StartEpochSeconds);
        return ($"{Loc.Get("NowLabel")} · {p.TimeRange}", true, (int)(1000 * (nowEpochSeconds - p.StartEpochSeconds) / length));
    }
}

/// <summary>Logica de Ajustes: anadir y quitar listas de canales.</summary>
public static class SettingsLogic
{
    /// <summary>
    /// Anade una lista. Devuelve la clave del aviso y, si se anadio, las listas nuevas (null si no:
    /// direccion no valida o ya estaba).
    /// </summary>
    public static (string MessageKey, List<string>? Urls) Add(string? text, IReadOnlyList<string> urls)
    {
        var url = (text ?? string.Empty).Trim();
        return ChannelLists.CheckNewList(url, urls) switch
        {
            ChannelLists.NewListCheck.Invalid => ("ListInvalid", null),
            ChannelLists.NewListCheck.Exists => ("ListExists", null),
            _ => ("ListAdded", [.. urls, url]),
        };
    }

    public static List<string> Remove(IEnumerable<string> urls, string url) => urls.Where(u => u != url).ToList();

    /// <summary>Etiqueta de la lista por defecto, o null para las demas.</summary>
    public static string? Label(string url) => url == ChannelCatalog.DefaultListUrl ? Loc.Get("DefaultListLabel") : null;
}
