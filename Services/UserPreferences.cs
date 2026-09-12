using Android.Content;

namespace TdtOnline.Services;

/// <summary>
/// Guarda las preferencias locales del usuario: canales favoritos y ultimo canal visto.
/// Procesamiento 100% local en el dispositivo (sin servidores ni cuentas).
/// </summary>
public sealed class UserPreferences
{
    private const string PrefsName = "tdt_preferences";
    private const string KeyFavorites = "favorites_list";
    private const string KeyLastChannel = "last_channel_name";
    private const string KeyLanguage = "language";
    private const string KeyLists = "list_urls";
    private const string KeyListsVersion = "list_urls_version";

    private readonly ISharedPreferences _prefs;

    public event Action<string, bool>? FavoriteChanged;

    public UserPreferences(Context context)
    {
        _prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }

    /// <summary>Idioma elegido en «Acerca de»; vacio = el del sistema.</summary>
    public string Language
    {
        get => _prefs.GetString(KeyLanguage, string.Empty) ?? string.Empty;
        set => _prefs.Edit()!.PutString(KeyLanguage, value)!.Apply();
    }

    /// <summary>
    /// Direcciones de las listas de canales, en orden (la primera manda). Vacio nunca: si el usuario
    /// las borra todas, vuelve la de tdt-canales.
    /// </summary>
    public IReadOnlyList<string> ListUrls
    {
        get
        {
            var raw = _prefs.GetString(KeyLists, null);
            var urls = string.IsNullOrWhiteSpace(raw)
                ? []
                : raw.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            return urls.Count > 0 ? urls : [ChannelCatalog.DefaultListUrl];
        }
        set
        {
            _prefs.Edit()!.PutString(KeyLists, string.Join('\n', value))!.Apply();
            BumpListsVersion();
        }
    }

    /// <summary>Sube cada vez que cambian las listas o se pide un refresco: la pantalla principal recarga si lo ve distinto.</summary>
    public int ListsVersion => _prefs.GetInt(KeyListsVersion, 0);

    public void BumpListsVersion() => _prefs.Edit()!.PutInt(KeyListsVersion, ListsVersion + 1)!.Apply();

    public HashSet<string> GetFavorites()
    {
        var set = _prefs.GetStringSet(KeyFavorites, null);
        return set is not null ? new HashSet<string>(set, StringComparer.OrdinalIgnoreCase) : [];
    }

    public bool IsFavorite(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            return false;

        return GetFavorites().Contains(channelName);
    }

    public bool ToggleFavorite(string channelName)
    {
        if (string.IsNullOrWhiteSpace(channelName))
            return false;

        var set = GetFavorites();
        bool isNowFavorite;

        if (set.Contains(channelName))
        {
            set.Remove(channelName);
            isNowFavorite = false;
        }
        else
        {
            set.Add(channelName);
            isNowFavorite = true;
        }

        _prefs.Edit()!.PutStringSet(KeyFavorites, set)!.Apply();
        FavoriteChanged?.Invoke(channelName, isNowFavorite);
        return isNowFavorite;
    }

    public string? LastChannel
    {
        get => _prefs.GetString(KeyLastChannel, null);
        set
        {
            if (string.IsNullOrWhiteSpace(value))
                _prefs.Edit()!.Remove(KeyLastChannel)!.Apply();
            else
                _prefs.Edit()!.PutString(KeyLastChannel, value)!.Apply();
        }
    }
}
