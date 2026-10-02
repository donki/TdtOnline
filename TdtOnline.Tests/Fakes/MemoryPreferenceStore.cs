using TdtOnline.Services;

namespace TdtOnline.Tests.Fakes;

/// <summary>Preferencias en memoria, en lugar de las SharedPreferences de Android.</summary>
public sealed class MemoryPreferenceStore : IPreferenceStore
{
    public Dictionary<string, object> Values { get; } = new();

    public string? GetString(string key, string? defaultValue) => Values.TryGetValue(key, out var v) ? (string)v : defaultValue;

    public void PutString(string key, string value) => Values[key] = value;

    public int GetInt(string key, int defaultValue) => Values.TryGetValue(key, out var v) ? (int)v : defaultValue;

    public void PutInt(string key, int value) => Values[key] = value;

    public ICollection<string>? GetStringSet(string key) => Values.TryGetValue(key, out var v) ? (ICollection<string>)v : null;

    // Como Android: se guarda una copia, no el conjunto que se pasa.
    public void PutStringSet(string key, ICollection<string> values) => Values[key] = values.ToList();

    public void Remove(string key) => Values.Remove(key);
}
