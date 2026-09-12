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

    private readonly ISharedPreferences _prefs;

    public event Action<string, bool>? FavoriteChanged;

    public UserPreferences(Context context)
    {
        _prefs = context.GetSharedPreferences(PrefsName, FileCreationMode.Private)!;
    }

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
