using System.Text.Json;
using TdtOnline.Models;

namespace TdtOnline.Services;

/// <summary>
/// La lista de canales: se baja de TDTChannels, se guarda una copia y se lee de ahi si no hay red.
/// </summary>
/// <remarks>
/// <para><b>De donde sale.</b> Del proyecto TDTChannels (<c>github.com/LaQuay/TDTChannels</c>,
/// Apache 2.0), una lista mantenida por la comunidad con las emisiones <b>oficiales</b> de cada
/// cadena en internet: las direcciones son las de RTVE, Atresmedia, Mediaset, las autonomicas…
/// La aplicacion no aloja ni reemite nada; solo abre lo que cada cadena publica.</para>
///
/// <para><b>Que se guarda.</b> El JSON entero en la carpeta de cache, para arrancar al momento y
/// para seguir viendo la lista sin conexion (los canales, no; esos son en directo). Se renueva
/// cuando tiene mas de un dia.</para>
///
/// <para>Solo se leen los canales con al menos una direccion; los que traen opciones vacias no se
/// pueden ver y no tiene sentido enseñarlos.</para>
/// </remarks>
public sealed class ChannelCatalog
{
    public const string SourceUrl = "https://www.tdtchannels.com/lists/tv.json";
    public const string SourceName = "TDTChannels";

    private static readonly TimeSpan MaxAge = TimeSpan.FromDays(1);

    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly string _cachePath;

    public ChannelCatalog(string cacheDirectory)
    {
        _cachePath = Path.Combine(cacheDirectory, "tv.json");
    }

    /// <summary>Categorias con sus canales. Primero la copia guardada; si esta vieja o no hay, la red.</summary>
    public async Task<IReadOnlyList<Category>> LoadAsync(bool forceRefresh = false, CancellationToken cancellationToken = default)
    {
        string? json = null;

        if (!forceRefresh && File.Exists(_cachePath) && DateTime.UtcNow - File.GetLastWriteTimeUtc(_cachePath) < MaxAge)
            json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);

        if (json is null)
        {
            try
            {
                json = await _http.GetStringAsync(SourceUrl, cancellationToken).ConfigureAwait(false);
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath)!);
                await File.WriteAllTextAsync(_cachePath, json, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception) when (File.Exists(_cachePath))
            {
                // Sin red: vale la copia aunque sea vieja.
                json = await File.ReadAllTextAsync(_cachePath, cancellationToken).ConfigureAwait(false);
            }
        }

        return Parse(json);
    }

    private static IReadOnlyList<Category> Parse(string json)
    {
        var categories = new List<Category>();
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
                var channels = new List<Channel>();

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

                    if (urls.Count == 0)
                        continue;

                    channels.Add(new Channel
                    {
                        Name = item.GetProperty("name").GetString() ?? string.Empty,
                        LogoUrl = item.TryGetProperty("logo", out var logo) ? logo.GetString() : null,
                        Web = item.TryGetProperty("web", out var web) ? web.GetString() : null,
                        StreamUrls = urls,
                        Category = ambitName,
                        Country = countryName,
                    });
                }

                if (channels.Count > 0)
                    categories.Add(new Category(countryName == "Spain" ? ambitName : $"{countryName} · {ambitName}", channels));
            }
        }

        return categories;
    }
}
