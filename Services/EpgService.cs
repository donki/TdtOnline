using System.Text.Json;
using TdtOnline.Models;

namespace TdtOnline.Services;

/// <summary>
/// Gestiona la guia de programacion (EPG) de TDTChannels: descarga, guarda en cache y
/// consulta que programa se esta emitiendo en cada canal.
/// </summary>
public sealed class EpgService
{
    public const string EpgUrl = "https://www.tdtchannels.com/epg/TV.json";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(6);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private readonly string _cachePath;
    private readonly Dictionary<string, List<EpgProgram>> _epgData = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    public event Action? EpgLoaded;

    public EpgService(string cacheDirectory)
    {
        _cachePath = Path.Combine(cacheDirectory, "epg.json");
    }

    /// <summary>Descarga o lee de cache el EPG completo.</summary>
    public async Task LoadAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        string? json = null;

        if (!forceRefresh && File.Exists(_cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(_cachePath) < CacheDuration)
        {
            try
            {
                json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                // Fallback a red
            }
        }

        if (json is null)
        {
            try
            {
                json = await _http.GetStringAsync(EpgUrl, cancellationToken).ConfigureAwait(false);
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                await File.WriteAllTextAsync(_cachePath, json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (File.Exists(_cachePath))
            {
                // Sin conexion: reutilizar copia local
                json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            }
        }

        if (!string.IsNullOrEmpty(json))
        {
            ParseEpg(json);
            EpgLoaded?.Invoke();
        }
    }

    private void ParseEpg(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return;

            var parsed = new Dictionary<string, List<EpgProgram>>(StringComparer.OrdinalIgnoreCase);

            foreach (var channelElement in doc.RootElement.EnumerateArray())
            {
                if (!channelElement.TryGetProperty("name", out var nameProp) ||
                    !channelElement.TryGetProperty("events", out var eventsProp) ||
                    eventsProp.ValueKind != JsonValueKind.Array)
                    continue;

                var epgName = nameProp.GetString();
                if (string.IsNullOrWhiteSpace(epgName))
                    continue;

                var list = new List<EpgProgram>();
                foreach (var ev in eventsProp.EnumerateArray())
                {
                    var hi = ev.TryGetProperty("hi", out var hiProp) ? hiProp.GetInt64() : 0;
                    var hf = ev.TryGetProperty("hf", out var hfProp) ? hfProp.GetInt64() : 0;
                    var title = ev.TryGetProperty("t", out var tProp) ? tProp.GetString() ?? string.Empty : string.Empty;
                    var desc = ev.TryGetProperty("d", out var dProp) ? dProp.GetString() ?? string.Empty : string.Empty;

                    if (hi > 0 && hf > 0 && !string.IsNullOrWhiteSpace(title))
                    {
                        list.Add(new EpgProgram(title, desc, hi, hf));
                    }
                }

                if (list.Count > 0)
                {
                    list.Sort((a, b) => a.StartEpochSeconds.CompareTo(b.StartEpochSeconds));
                    parsed[epgName] = list;
                }
            }

            lock (_lock)
            {
                _epgData.Clear();
                foreach (var kvp in parsed)
                    _epgData[kvp.Key] = kvp.Value;
            }
        }
        catch
        {
            // Ignorar errores de parseo puntual en formato corrupto
        }
    }

    /// <summary>¿Hay guia para este canal?</summary>
    public bool Has(string? epgId)
    {
        if (string.IsNullOrWhiteSpace(epgId))
            return false;

        lock (_lock)
            return _epgData.ContainsKey(epgId);
    }

    /// <summary>El programa en curso y los siguientes, hasta <paramref name="max"/>; vacio si no hay guia.</summary>
    public IReadOnlyList<EpgProgram> GetUpcoming(string? epgId, int max)
    {
        if (string.IsNullOrWhiteSpace(epgId))
            return [];

        List<EpgProgram>? list;
        lock (_lock)
        {
            if (!_epgData.TryGetValue(epgId, out list) || list.Count == 0)
                return [];
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var result = new List<EpgProgram>(max);
        foreach (var p in list)
        {
            if (p.EndEpochSeconds <= now)
                continue;

            result.Add(p);
            if (result.Count >= max)
                break;
        }

        return result;
    }

    /// <summary>Obtiene el programa que se emite actualmente (o el siguiente proximo).</summary>
    public EpgProgram? GetCurrentProgram(string? epgId)
    {
        if (string.IsNullOrWhiteSpace(epgId))
            return null;

        List<EpgProgram>? list;
        lock (_lock)
        {
            if (!_epgData.TryGetValue(epgId, out list) || list.Count == 0)
                return null;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // 1. Programa que esta en curso
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p.StartEpochSeconds <= now && now < p.EndEpochSeconds)
                return p;
        }

        // 2. Si no hay en curso, el proximo programa que empieza despues de ahora
        for (int i = 0; i < list.Count; i++)
        {
            var p = list[i];
            if (p.StartEpochSeconds > now)
                return p;
        }

        return null;
    }
}
