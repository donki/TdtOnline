namespace TdtOnline.Services;

/// <summary>
/// Almacen de preferencias clave-valor. En Android son las SharedPreferences
/// (<see cref="SharedPreferencesStore"/>); en las pruebas, un diccionario.
/// </summary>
public interface IPreferenceStore
{
    string? GetString(string key, string? defaultValue);

    void PutString(string key, string value);

    int GetInt(string key, int defaultValue);

    void PutInt(string key, int value);

    ICollection<string>? GetStringSet(string key);

    void PutStringSet(string key, ICollection<string> values);

    void Remove(string key);
}
