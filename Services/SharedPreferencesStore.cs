using Android.Content;

namespace TdtOnline.Services;

/// <inheritdoc cref="IPreferenceStore"/>
public sealed class SharedPreferencesStore(Context context, string name) : IPreferenceStore
{
    private readonly ISharedPreferences _prefs = context.GetSharedPreferences(name, FileCreationMode.Private)!;

    public string? GetString(string key, string? defaultValue) => _prefs.GetString(key, defaultValue);

    public void PutString(string key, string value) => _prefs.Edit()!.PutString(key, value)!.Apply();

    public int GetInt(string key, int defaultValue) => _prefs.GetInt(key, defaultValue);

    public void PutInt(string key, int value) => _prefs.Edit()!.PutInt(key, value)!.Apply();

    public ICollection<string>? GetStringSet(string key) => _prefs.GetStringSet(key, null);

    public void PutStringSet(string key, ICollection<string> values) => _prefs.Edit()!.PutStringSet(key, values)!.Apply();

    public void Remove(string key) => _prefs.Edit()!.Remove(key)!.Apply();
}
