using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TdtOnline.Models;

namespace TdtOnline.Services;

/// <summary>
/// La lista de canales: se baja de una o varias listas cada vez que arranca la aplicacion, se
/// guarda una copia de cada una y se lee de ahi si no hay red.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Por defecto, de <c>github.com/donki/tdt-canales</c>: una lista
/// propia, generada a partir de TDTChannels (Apache 2.0) y de Free-TV (comprobada), en la que ya
/// vienen las categorias, los logotipos, el <c>epg_id</c> y las direcciones en orden de preferencia.
/// En Ajustes se pueden añadir mas listas; se admiten tres formatos, que se reconocen por el
/// contenido: el JSON propio (<c>categories</c>), el JSON de TDTChannels (<c>countries</c>) y
/// M3U/M3U8 (<c>group-title</c> hace de categoria).</para>
///
/// <para><b>Como se juntan.</b> La primera lista manda en el orden de categorias. De las
/// siguientes, un canal con el mismo nombre (sin acentos, espacios ni mayusculas) que uno ya
/// cargado le añade sus direcciones detras; los demas van a la categoria del mismo nombre si la
/// hay y, si no, a una nueva al final. Los que se quedan sin direccion no se enseñan.</para>
///
/// <para><b>Resolvedores.</b> Algun canal (DMAX) no trae direccion sino un <c>resolver</c>: la
/// direccion se pide a su servicio al ir a verlo (<see cref="SonicLive"/>). Entra en la lista solo
/// si la comprobacion en segundo plano ve el directo encendido; cuando termina, <see cref="Updated"/>
/// avisa con la lista completa. El resultado se guarda un dia.</para>
///
/// <para><b>Que se guarda.</b> Cada lista descargada, en la carpeta de cache, para seguir viendo
/// la lista sin conexion (los canales, no; esos son en directo). Siempre se intenta la red primero:
/// asi la lista esta al dia en cuanto se publica.</para>
/// </remarks>
public sealed partial class ChannelCatalog
{
    public const string DefaultListUrl = "https://raw.githubusercontent.com/donki/tdt-canales/main/canales.json";
    public const string SourceName = "tdt-canales";

    private static readonly TimeSpan VerifiedMaxAge = TimeSpan.FromDays(1);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _cacheDirectory;
    private readonly string _verifiedPath;
    private readonly UserPreferences _prefs;

    public ChannelCatalog(string cacheDirectory, UserPreferences prefs)
    {
        _cacheDirectory = cacheDirectory;
        _prefs = prefs;
        _verifiedPath = Path.Combine(cacheDirectory, "resolvers-ok.json");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android) TdtOnline");
    }

    /// <summary>Lista completa, cuando la comprobacion de resolvedores en segundo plano ha terminado (hilo cualquiera).</summary>
    public event Action<IReadOnlyList<Category>>? Updated;

    /// <summary>Listas que ni se bajaron ni tenian copia guardada en la ultima carga.</summary>
    public IReadOnlyList<string> FailedLists { get; private set; } = [];

    /// <summary>Categorias con sus canales, juntando todas las listas de Ajustes.</summary>
    public async Task<IReadOnlyList<Category>> LoadAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var categories = new List<(string Name, List<Draft> Channels)>();
        var failed = new List<string>();

        foreach (var url in _prefs.ListUrls)
        {
            var text = await LoadTextAsync(url, cancellationToken).ConfigureAwait(false);
            if (text is null)
            {
                failed.Add(url);
                continue;
            }

            List<(string, List<Draft>)> parsed;
            try
            {
                parsed = Parse(text);
            }
            catch (Exception)
            {
                failed.Add(url);
                continue;
            }

            Merge(categories, parsed);
        }

        FailedLists = failed;

        var verified = forceRefresh ? null : ReadVerified();
        if (verified is null && categories.Any(c => c.Channels.Any(d => d.Resolver is not null)))
        {
            // Sin comprobacion reciente: se devuelve lo que hay y se comprueba aparte.
            _ = Task.Run(() => VerifyAndNotifyAsync(categories, cancellationToken), cancellationToken);
        }

        return Finish(categories, verified);
    }

    /// <summary>Borra las copias guardadas: la proxima carga vuelve a bajarlo todo.</summary>
    public void ClearCache()
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(_cacheDirectory, "lista-*.txt"))
                File.Delete(file);
            if (File.Exists(_verifiedPath))
                File.Delete(_verifiedPath);
        }
        catch (Exception)
        {
            // Si algo no se deja borrar, se sobrescribira en la siguiente descarga.
        }
    }

    private string CachePathFor(string url)
    {
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..16].ToLowerInvariant();
        return Path.Combine(_cacheDirectory, $"lista-{hash}.txt");
    }

    /// <summary>La red primero; si falla, la copia guardada; si tampoco, nulo.</summary>
    private async Task<string?> LoadTextAsync(string url, CancellationToken cancellationToken)
    {
        var cachePath = CachePathFor(url);
        try
        {
            var text = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidDataException("Lista vacia");

            Directory.CreateDirectory(_cacheDirectory);
            await File.WriteAllTextAsync(cachePath, text, cancellationToken).ConfigureAwait(false);
            return text;
        }
        catch (Exception) when (File.Exists(cachePath))
        {
            return await File.ReadAllTextAsync(cachePath, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return null;
        }
    }

    // ---------------------------------------------------------------------
    //  Formatos
    // ---------------------------------------------------------------------

    /// <summary>Canal en construccion: se le pueden añadir direcciones de otra lista antes de cerrarlo.</summary>
    private sealed class Draft
    {
        public string Name = string.Empty;
        public string? LogoUrl;
        public string? Web;
        public string? EpgId;
        public string Category = string.Empty;
        public string Country = string.Empty;
        public List<string> Urls = [];
        public string? Resolver;
    }

    private static List<(string Name, List<Draft> Channels)> Parse(string text)
    {
        var head = text.TrimStart();
        if (head.StartsWith("#EXTM3U", StringComparison.OrdinalIgnoreCase) || head.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            return ParseM3u(text);

        using var document = JsonDocument.Parse(text);
        var root = document.RootElement;
        if (root.TryGetProperty("categories", out var categories))
            return ParseOwn(categories);
        if (root.TryGetProperty("countries", out var countries))
            return ParseTdtChannels(countries);

        throw new InvalidDataException("Formato de lista desconocido");
    }

    /// <summary>El formato propio de tdt-canales: categorias con canales ya resueltos.</summary>
    private static List<(string Name, List<Draft> Channels)> ParseOwn(JsonElement categories)
    {
        var result = new List<(string, List<Draft>)>();
        foreach (var category in categories.EnumerateArray())
        {
            var name = category.TryGetProperty("name", out var n) ? n.GetString() ?? string.Empty : string.Empty;
            var channels = new List<Draft>();
            if (category.TryGetProperty("channels", out var list))
            {
                foreach (var item in list.EnumerateArray())
                {
                    var urls = new List<string>();
                    if (item.TryGetProperty("urls", out var u))
                    {
                        foreach (var url in u.EnumerateArray())
                        {
                            var s = url.GetString();
                            if (!string.IsNullOrWhiteSpace(s))
                                urls.Add(s);
                        }
                    }

                    channels.Add(new Draft
                    {
                        Name = item.TryGetProperty("name", out var cn) ? cn.GetString() ?? string.Empty : string.Empty,
                        LogoUrl = item.TryGetProperty("logo", out var logo) ? logo.GetString() : null,
                        Web = item.TryGetProperty("web", out var web) ? web.GetString() : null,
                        EpgId = item.TryGetProperty("epg_id", out var epg) ? epg.GetString() : null,
                        Resolver = item.TryGetProperty("resolver", out var r) && r.ValueKind == JsonValueKind.String ? r.GetString() : null,
                        Urls = urls,
                        Category = name,
                        Country = "Spain",
                    });
                }
            }

            result.Add((name, channels));
        }

        return result;
    }

    /// <summary>El JSON de TDTChannels (paises → ambitos → canales con opciones).</summary>
    private static List<(string Name, List<Draft> Channels)> ParseTdtChannels(JsonElement countries)
    {
        var categories = new List<(string, List<Draft>)>();
        foreach (var country in countries.EnumerateArray())
        {
            var countryName = country.GetProperty("name").GetString() ?? string.Empty;
            if (!country.TryGetProperty("ambits", out var ambits))
                continue;

            foreach (var ambit in ambits.EnumerateArray())
            {
                var ambitName = ambit.GetProperty("name").GetString() ?? string.Empty;
                var channels = new List<Draft>();
                if (!ambit.TryGetProperty("channels", out var list))
                    continue;

                foreach (var item in list.EnumerateArray())
                {
                    var urls = new List<string>();
                    if (item.TryGetProperty("options", out var options))
                    {
                        foreach (var option in options.EnumerateArray())
                        {
                            var url = option.TryGetProperty("url", out var u) ? u.GetString() : null;
                            var format = option.TryGetProperty("format", out var f) ? f.GetString() : null;

                            // Las direcciones con marcadores entre corchetes son plantillas para
                            // servidores de anuncios, no emisiones; y solo HLS/DASH se reproducen.
                            if (string.IsNullOrWhiteSpace(url) || url.Contains('[') || format is not ("m3u8" or "mpd"))
                                continue;

                            urls.Add(url);
                        }
                    }

                    channels.Add(new Draft
                    {
                        Name = item.GetProperty("name").GetString() ?? string.Empty,
                        LogoUrl = item.TryGetProperty("logo", out var logo) ? logo.GetString() : null,
                        Web = item.TryGetProperty("web", out var web) ? web.GetString() : null,
                        EpgId = item.TryGetProperty("epg_id", out var epgId) ? epgId.GetString() : null,
                        Urls = urls,
                        Category = ambitName,
                        Country = countryName,
                    });
                }

                categories.Add((countryName == "Spain" ? ambitName : $"{countryName} · {ambitName}", channels));
            }
        }

        return categories;
    }

    [GeneratedRegex(@"([\w-]+)=""([^""]*)""")]
    private static partial Regex ExtInfAttribute();

    /// <summary>M3U/M3U8: <c>group-title</c> hace de categoria. Se quitan las marcas Ⓢ/Ⓖ/Ⓨ que usan algunas listas.</summary>
    private static List<(string Name, List<Draft> Channels)> ParseM3u(string m3u)
    {
        var byCategory = new Dictionary<string, List<Draft>>(StringComparer.OrdinalIgnoreCase);
        var order = new List<string>();
        Draft? pending = null;

        foreach (var raw in m3u.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTINF", StringComparison.OrdinalIgnoreCase))
            {
                pending = null;
                string? group = null, logo = null, epgId = null, tvgName = null;
                foreach (Match m in ExtInfAttribute().Matches(line))
                {
                    switch (m.Groups[1].Value)
                    {
                        case "group-title": group = m.Groups[2].Value; break;
                        case "tvg-logo": logo = m.Groups[2].Value; break;
                        case "tvg-id": epgId = m.Groups[2].Value; break;
                        case "tvg-name": tvgName = m.Groups[2].Value; break;
                    }
                }

                var comma = line.LastIndexOf(',');
                var name = comma >= 0 ? line[(comma + 1)..].Trim() : tvgName ?? string.Empty;
                name = name.Replace("Ⓢ", "").Replace("Ⓖ", "").Replace("Ⓨ", "").Trim();
                if (name.Length == 0)
                    continue;

                pending = new Draft
                {
                    Name = name,
                    LogoUrl = string.IsNullOrWhiteSpace(logo) ? null : logo,
                    EpgId = epgId,
                    Category = string.IsNullOrWhiteSpace(group) ? "M3U" : group,
                    Country = "Spain",
                };
                continue;
            }

            if (line.StartsWith('#') || pending is null)
                continue;

            // Linea de direccion. Los enlaces a YouTube y similares no los abre el reproductor.
            if ((line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || line.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                !line.Contains("youtube.com", StringComparison.OrdinalIgnoreCase) && !line.Contains("youtu.be", StringComparison.OrdinalIgnoreCase))
            {
                if (pending.Urls.Count == 0)
                {
                    if (!byCategory.TryGetValue(pending.Category, out var list))
                    {
                        list = [];
                        byCategory[pending.Category] = list;
                        order.Add(pending.Category);
                    }

                    list.Add(pending);
                }

                pending.Urls.Add(line);
            }
        }

        return order.Select(c => (c, byCategory[c])).ToList();
    }

    // ---------------------------------------------------------------------
    //  Union de listas
    // ---------------------------------------------------------------------

    /// <summary>Clave de comparacion: minusculas, sin acentos, sin espacios ni signos.</summary>
    private static string Key(string name)
    {
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name.Normalize(NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
    }

    private static void Merge(List<(string Name, List<Draft> Channels)> categories, List<(string Name, List<Draft> Channels)> extra)
    {
        if (categories.Count == 0)
        {
            categories.AddRange(extra);
            return;
        }

        var byKey = new Dictionary<string, Draft>(StringComparer.Ordinal);
        foreach (var (_, channels) in categories)
        {
            foreach (var ch in channels)
                byKey.TryAdd(Key(ch.Name), ch);
        }

        foreach (var (name, channels) in extra)
        {
            foreach (var ch in channels)
            {
                var key = Key(ch.Name);
                if (byKey.TryGetValue(key, out var existing))
                {
                    foreach (var url in ch.Urls)
                    {
                        if (!existing.Urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                            existing.Urls.Add(url);
                    }

                    existing.LogoUrl ??= ch.LogoUrl;
                    existing.EpgId ??= ch.EpgId;
                    existing.Resolver ??= ch.Resolver;
                    continue;
                }

                byKey[key] = ch;
                var index = categories.FindIndex(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));
                if (index < 0)
                {
                    categories.Add((name, []));
                    index = categories.Count - 1;
                }

                categories[index].Channels.Add(ch);
            }
        }
    }

    private static IReadOnlyList<Category> Finish(List<(string Name, List<Draft> Channels)> categories, HashSet<string>? verified)
    {
        var result = new List<Category>();
        foreach (var (name, drafts) in categories)
        {
            var channels = drafts
                .Where(d => d.Urls.Count > 0 || (d.Resolver is not null && verified is not null && verified.Contains(d.Resolver)))
                .Select(d => new Channel
                {
                    Name = d.Name,
                    LogoUrl = d.LogoUrl,
                    Web = d.Web,
                    EpgId = d.EpgId,
                    StreamUrls = d.Urls,
                    Resolver = d.Resolver,
                    Category = d.Category,
                    Country = d.Country,
                })
                .ToList();

            if (channels.Count > 0)
                result.Add(new Category(name, channels));
        }

        return result;
    }

    // ---------------------------------------------------------------------
    //  Resolvedores: solo entran si su directo esta encendido
    // ---------------------------------------------------------------------

    private HashSet<string>? ReadVerified()
    {
        try
        {
            if (!File.Exists(_verifiedPath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(_verifiedPath) >= VerifiedMaxAge)
                return null;

            var urls = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_verifiedPath));
            return urls is null ? null : new HashSet<string>(urls, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return null;
        }
    }

    private async Task VerifyAndNotifyAsync(List<(string Name, List<Draft> Channels)> categories, CancellationToken cancellationToken)
    {
        var ok = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var resolvers = categories.SelectMany(c => c.Channels).Select(d => d.Resolver).OfType<string>().Distinct();
        foreach (var resolver in resolvers)
        {
            if (SonicLive.Handles(resolver) && await SonicLive.ProbeAsync(resolver, cancellationToken).ConfigureAwait(false))
                ok.Add(resolver);
        }

        if (cancellationToken.IsCancellationRequested)
            return;

        try
        {
            File.WriteAllText(_verifiedPath, JsonSerializer.Serialize(ok.ToArray()));
        }
        catch (Exception)
        {
            // Sin cache se volvera a comprobar la proxima vez; no pasa nada.
        }

        Updated?.Invoke(Finish(categories, ok));
    }
}
