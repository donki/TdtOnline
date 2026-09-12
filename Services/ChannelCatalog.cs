using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TdtOnline.Models;

namespace TdtOnline.Services;

/// <summary>
/// La lista de canales: se baja de TDTChannels y de Free-TV, se guarda una copia y se lee de ahi si no hay red.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> De dos listas mantenidas por la comunidad:</para>
/// <list type="bullet">
/// <item>TDTChannels (<c>github.com/LaQuay/TDTChannels</c>, Apache 2.0): las emisiones
/// <b>oficiales</b> de cada cadena; da las categorias, los logotipos y el EPG. Es la base.</item>
/// <item>Free-TV/IPTV (<c>github.com/Free-TV/IPTV</c>, sin licencia declarada): de ella solo se
/// toma el grupo «Spain». Añade algun canal que TDTChannels no tiene (Paramount Network, Negocios,
/// 3/24, TVE Internacional…) y direcciones de repuesto para otros. No arregla lo de Mediaset,
/// Atresmedia ni DMAX: esas cadenas no publican emision abierta en ninguna lista.</item>
/// </list>
/// <para>Aparte, DMAX: no esta en ninguna lista porque su web no publica direccion fija, sino
/// que la da su propio servicio en cada reproduccion (ver <see cref="SonicLive"/>). Se conserva la
/// ficha de TDTChannels (nombre, logotipo, EPG) y se le pone el <c>Resolver</c> que sabe pedirla.
/// Entra en la lista solo si la comprobacion en segundo plano ve que el directo esta encendido.</para>
///
/// <para>La aplicacion no aloja ni reemite nada; solo abre lo que hay publicado.</para>
///
/// <para><b>Como se juntan.</b> Un canal de Free-TV con el mismo nombre (sin acentos, espacios ni
/// mayusculas) que uno de TDTChannels le añade sus direcciones detras de las oficiales, que van
/// primero; el reproductor ya prueba la siguiente si una falla. Los que no casan con ninguno van a
/// una categoria propia, «Free-TV». Los que se quedan sin ninguna direccion no se enseñan.</para>
///
/// <para><b>Comprobacion de Free-TV.</b> Esa lista trae bastantes direcciones muertas o que exigen
/// sesion (las de Mediaset y Atresmedia devuelven 400/403 y no se ven). Enseñarlas seria ofrecer
/// canales que fallan al abrirlos, asi que antes de mezclarlas se pide la cabecera de cada una y
/// solo entran las que contestan bien. Se hace en segundo plano al arrancar: la lista sale al
/// momento con lo ya comprobado (o solo con TDTChannels la primera vez) y, cuando acaba la
/// comprobacion, <see cref="Updated"/> avisa con la lista completa. El resultado se guarda un dia.</para>
///
/// <para><b>Que se guarda.</b> Las dos descargas enteras y la lista de direcciones comprobadas, en
/// la carpeta de cache, para arrancar al momento y para seguir viendo la lista sin conexion (los
/// canales, no; esos son en directo). Se renuevan cuando tienen mas de un dia. Si Free-TV no baja
/// y no hay copia, se sigue solo con TDTChannels.</para>
/// </remarks>
public sealed partial class ChannelCatalog
{
    public const string SourceUrl = "https://www.tdtchannels.com/lists/tv.json";
    public const string SourceName = "TDTChannels";

    public const string FreeTvUrl = "https://raw.githubusercontent.com/Free-TV/IPTV/master/playlist.m3u8";
    public const string FreeTvName = "Free-TV";

    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _cachePath;
    private readonly string _freeTvCachePath;
    private readonly string _verifiedPath;

    public ChannelCatalog(string cacheDirectory)
    {
        _cachePath = Path.Combine(cacheDirectory, "tv.json");
        _freeTvCachePath = Path.Combine(cacheDirectory, "freetv.m3u8");
        _verifiedPath = Path.Combine(cacheDirectory, "freetv-ok.json");
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android) TdtOnline");
    }

    /// <summary>Lista completa, cuando la comprobacion de Free-TV en segundo plano ha terminado (hilo cualquiera).</summary>
    public event Action<IReadOnlyList<Category>>? Updated;

    /// <summary>Categorias con sus canales. Primero la copia guardada; si esta vieja o no hay, la red.</summary>
    public async Task<IReadOnlyList<Category>> LoadAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        var json = await LoadTextAsync(SourceUrl, _cachePath, forceRefresh, cancellationToken).ConfigureAwait(false);

        string? m3u = null;
        try
        {
            m3u = await LoadTextAsync(FreeTvUrl, _freeTvCachePath, forceRefresh, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // La segunda lista es un complemento: sin ella se sigue con la primera.
        }

        var extras = m3u is null ? [] : ParseFreeTv(m3u);
        var verified = ReadVerified();

        if (forceRefresh || verified is null)
        {
            // Sin comprobacion reciente: se devuelve lo que hay y se comprueba aparte.
            _ = Task.Run(() => VerifyAndNotifyAsync(json, extras, cancellationToken), cancellationToken);
        }

        return Build(json, extras, verified);
    }

    private IReadOnlyList<Category> Build(string json, List<Draft> extras, HashSet<string>? verified)
    {
        var categories = ParseTdtChannels(json);
        if (verified is not null && extras.Count > 0)
            Merge(categories, KeepVerified(extras, verified));

        return Finish(categories, verified);
    }

    private static List<Draft> KeepVerified(List<Draft> extras, HashSet<string> verified)
    {
        var kept = new List<Draft>();
        foreach (var ch in extras)
        {
            var urls = ch.Urls.Where(verified.Contains).ToList();
            if (urls.Count == 0)
                continue;

            // Copia: el original se queda entero para poder volver a comprobarlo mas adelante.
            kept.Add(new Draft
            {
                Name = ch.Name, LogoUrl = ch.LogoUrl, Web = ch.Web, EpgId = ch.EpgId,
                Category = ch.Category, Country = ch.Country, Urls = urls, Resolver = ch.Resolver,
            });
        }

        return kept;
    }

    private HashSet<string>? ReadVerified()
    {
        try
        {
            if (!File.Exists(_verifiedPath) || DateTime.UtcNow - File.GetLastWriteTimeUtc(_verifiedPath) >= MaxAge)
                return null;

            var urls = JsonSerializer.Deserialize<string[]>(File.ReadAllText(_verifiedPath));
            return urls is null ? null : new HashSet<string>(urls, StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>Pide la cabecera de cada direccion de Free-TV (ocho a la vez) y se queda con las que contestan bien.</summary>
    private async Task VerifyAndNotifyAsync(string json, List<Draft> extras, CancellationToken cancellationToken)
    {
        var ok = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var gate = new SemaphoreSlim(8);

        var tasks = extras.SelectMany(ch => ch.Urls).Distinct(StringComparer.OrdinalIgnoreCase).Select(async url =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(8));
                using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeout.Token).ConfigureAwait(false);
                if (response.IsSuccessStatusCode)
                {
                    lock (ok)
                        ok.Add(url);
                }
            }
            catch (Exception)
            {
                // No contesta o falla: fuera.
            }
            finally
            {
                gate.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Las cadenas con resolvedor (DMAX): entran si su directo esta encendido ahora.
        foreach (var resolver in Resolvers.Values)
        {
            if (await SonicLive.ProbeAsync(resolver, cancellationToken).ConfigureAwait(false))
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

        Updated?.Invoke(Build(json, extras, ok));
    }

    private async Task<string> LoadTextAsync(string url, string cachePath, bool forceRefresh, CancellationToken cancellationToken)
    {
        if (!forceRefresh && File.Exists(cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(cachePath) < MaxAge)
            return await File.ReadAllTextAsync(cachePath, cancellationToken).ConfigureAwait(false);

        try
        {
            var text = await _http.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
            Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
            await File.WriteAllTextAsync(cachePath, text, cancellationToken).ConfigureAwait(false);
            return text;
        }
        catch (Exception) when (File.Exists(cachePath))
        {
            // Sin red: vale la copia aunque sea vieja.
            return await File.ReadAllTextAsync(cachePath, cancellationToken).ConfigureAwait(false);
        }
    }

    // ---------------------------------------------------------------------
    //  TDTChannels (JSON)
    // ---------------------------------------------------------------------

    /// <summary>Canal en construccion: se le pueden añadir direcciones de la otra lista antes de cerrarlo.</summary>
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

    /// <summary>Cadenas cuya emision se pide a su servicio al reproducir (clave del nombre → especificacion).</summary>
    private static readonly Dictionary<string, string> Resolvers = new(StringComparer.Ordinal)
    {
        ["dmax"] = SonicLive.Prefix + "es:1",
    };

    private static List<(string Name, List<Draft> Channels)> ParseTdtChannels(string json)
    {
        var categories = new List<(string, List<Draft>)>();
        using var document = JsonDocument.Parse(json);

        if (!document.RootElement.TryGetProperty("countries", out var countries))
            return categories;

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
                            if (string.IsNullOrWhiteSpace(url) || url.Contains('[') ||
                                format is not ("m3u8" or "mpd"))
                                continue;

                            urls.Add(url);
                        }
                    }

                    // Los que vienen sin direccion se guardan igual: puede que la otra lista la traiga.
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

    // ---------------------------------------------------------------------
    //  Free-TV (M3U8)
    // ---------------------------------------------------------------------

    [GeneratedRegex(@"([\w-]+)=""([^""]*)""")]
    private static partial Regex ExtInfAttribute();

    /// <summary>Solo el grupo «Spain». Los nombres vienen con marcas (Ⓢ no HD, Ⓖ geobloqueado, Ⓨ YouTube) que se quitan.</summary>
    private static List<Draft> ParseFreeTv(string m3u)
    {
        var result = new List<Draft>();
        Draft? pending = null;

        foreach (var raw in m3u.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0)
                continue;

            if (line.StartsWith("#EXTINF", StringComparison.Ordinal))
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

                if (group != "Spain")
                    continue;

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
                    Category = FreeTvName,
                    Country = "Spain",
                };
                continue;
            }

            if (line.StartsWith('#') || pending is null)
                continue;

            // Linea de direccion. Solo HLS/DASH: los enlaces a YouTube y demas no los abre el reproductor.
            if ((line.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                 line.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) &&
                (line.Contains(".m3u8", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains(".mpd", StringComparison.OrdinalIgnoreCase)))
            {
                if (pending.Urls.Count == 0)
                    result.Add(pending);
                pending.Urls.Add(line);
            }
        }

        return result;
    }

    // ---------------------------------------------------------------------
    //  Union de las dos listas
    // ---------------------------------------------------------------------

    /// <summary>Nombres de Free-TV que TDTChannels escribe distinto (clave de Free-TV → clave de TDTChannels).</summary>
    private static readonly Dictionary<string, string> Aliases = new(StringComparer.Ordinal)
    {
        ["tdp"] = "teledeporte",
        ["tv3cat"] = "tv3",
        ["el33"] = "33",
        ["apunttv"] = "apunt",
        ["7regiondemurcia"] = "la7",
        ["televisioncanaria"] = "tvcanariartvc",
    };

    /// <summary>Canales que solo trae Free-TV pero que tienen sitio claro en las categorias de TDTChannels.</summary>
    private static readonly Dictionary<string, string> OrphanCategories = new(StringComparer.Ordinal)
    {
        ["antena3"] = "Generalistas",
        ["lasexta"] = "Generalistas",
        ["neox"] = "Generalistas",
        ["nova"] = "Generalistas",
        ["mega"] = "Generalistas",
        ["atreseries"] = "Generalistas",
        ["paramountnetwork"] = "Generalistas",
        ["negocios"] = "Informativos",
        ["324"] = "Cataluña",
        ["bondia"] = "Cataluña",
        ["la2catalunya"] = "Cataluña",
    };

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

    private static void Merge(List<(string Name, List<Draft> Channels)> categories, List<Draft> extra)
    {
        var byKey = new Dictionary<string, Draft>(StringComparer.Ordinal);
        foreach (var (_, channels) in categories)
        {
            foreach (var ch in channels)
                byKey.TryAdd(Key(ch.Name), ch);
        }

        var orphans = new List<Draft>();
        foreach (var ch in extra)
        {
            var key = Key(ch.Name);
            if (Aliases.TryGetValue(key, out var alias))
                key = alias;

            if (byKey.TryGetValue(key, out var existing))
            {
                foreach (var url in ch.Urls)
                {
                    if (!existing.Urls.Contains(url, StringComparer.OrdinalIgnoreCase))
                        existing.Urls.Add(url);
                }

                existing.LogoUrl ??= ch.LogoUrl;
                continue;
            }

            byKey[key] = ch;

            // Con sitio conocido, a su categoria; el resto, al grupo de Free-TV.
            if (OrphanCategories.TryGetValue(key, out var home))
            {
                var index = categories.FindIndex(c => c.Name == home);
                if (index >= 0)
                {
                    ch.Category = home;
                    categories[index].Channels.Add(ch);
                    continue;
                }
            }

            orphans.Add(ch);
        }

        if (orphans.Count > 0)
            categories.Add((FreeTvName, orphans));
    }

    private static IReadOnlyList<Category> Finish(List<(string Name, List<Draft> Channels)> categories, HashSet<string>? verified)
    {
        var result = new List<Category>();
        foreach (var (name, drafts) in categories)
        {
            foreach (var d in drafts)
            {
                if (d.Resolver is null && d.Country == "Spain" && Resolvers.TryGetValue(Key(d.Name), out var resolver))
                    d.Resolver = resolver;
            }

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
}
