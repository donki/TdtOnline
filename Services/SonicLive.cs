using System.Text.Json;

namespace TdtOnline.Services;

/// <summary>
/// Pide la emision en directo de una cadena que no publica direccion fija sino que la da su propio
/// servicio en cada reproduccion: hoy, DMAX (dmax.marca.com).
/// </summary>
/// <remarks>
/// <para>La web de DMAX corre sobre la plataforma «Sonic» de Warner Bros. Discovery
/// (<c>public.aurora.enhanced.live</c>). Para el directo hace dos peticiones publicas, sin cuenta ni
/// contraseña: un token anonimo (<c>/token?realm=es</c>, dura un mes) y la informacion de
/// reproduccion del canal (<c>/playback/channelPlaybackInfo/{id}</c>), que devuelve la direccion HLS y
/// la DASH. Aqui se hacen esas dos mismas peticiones, ni una mas. La emision va sin DRM.</para>
///
/// <para>La direccion que devuelven lleva un token firmado con el pais y la IP de quien la pide y
/// caduca a los seis minutos: por eso no se puede guardar en la lista y hay que pedirla al ir a
/// ver el canal; y por eso el reproductor, si falla a media emision, vuelve a pedirla una vez
/// antes de darse por vencido. La restriccion geografica la aplica la cadena, no la
/// aplicacion.</para>
///
/// <para>La especificacion de un canal es <c>sonic:{realm}:{channelId}</c>, p. ej. <c>sonic:es:1</c>.</para>
/// </remarks>
public static class SonicLive
{
    public const string Prefix = "sonic:";

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private static readonly Dictionary<string, string> Tokens = new(StringComparer.Ordinal);

    static SonicLive()
    {
        Http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Linux; Android) TdtOnline");
    }

    public static bool Handles(string? resolver) =>
        resolver is not null && resolver.StartsWith(Prefix, StringComparison.Ordinal);

    /// <summary>
    /// ¿Se ve ahora mismo? Pide la direccion, baja la lista maestra y la primera variante. La cadena
    /// puede tener el directo apagado en la web (DMAX España lo tiene asi en 2026-09: contesta la
    /// maestra pero las variantes dan 404) y entonces no se enseña, para no ofrecer un canal que falla.
    /// </summary>
    public static async Task<bool> ProbeAsync(string resolver, CancellationToken cancellationToken = default)
    {
        try
        {
            var urls = await ResolveAsync(resolver, cancellationToken).ConfigureAwait(false);
            var master = urls.FirstOrDefault(u => u.Contains(".m3u8", StringComparison.OrdinalIgnoreCase));
            if (master is null)
                return false;

            var text = await Http.GetStringAsync(master, cancellationToken).ConfigureAwait(false);
            var variant = text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0 && !l.StartsWith('#'));
            if (variant is null)
                return false;

            var variantUrl = new Uri(new Uri(master), variant);
            using var response = await Http.GetAsync(variantUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            return response.IsSuccessStatusCode;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Direcciones de la emision ahora mismo (HLS primero); vacio si no se consigue.</summary>
    public static async Task<string[]> ResolveAsync(string resolver, CancellationToken cancellationToken = default)
    {
        var parts = resolver.Split(':');
        if (parts.Length != 3)
            return [];

        var realm = parts[1];
        var channelId = parts[2];

        try
        {
            var token = await TokenAsync(realm, cancellationToken).ConfigureAwait(false);
            var urls = await PlaybackAsync(token, channelId, cancellationToken).ConfigureAwait(false);
            if (urls.Length > 0)
                return urls;

            // Token caducado o revocado: uno nuevo y otro intento.
            lock (Tokens)
                Tokens.Remove(realm);

            token = await TokenAsync(realm, cancellationToken).ConfigureAwait(false);
            return await PlaybackAsync(token, channelId, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static async Task<string> TokenAsync(string realm, CancellationToken cancellationToken)
    {
        lock (Tokens)
        {
            if (Tokens.TryGetValue(realm, out var cached))
                return cached;
        }

        var url = $"https://public.aurora.enhanced.live/token?realm={Uri.EscapeDataString(realm)}&deviceId={Guid.NewGuid():N}&shortlived=true";
        using var stream = await Http.GetStreamAsync(url, cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        var token = document.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("token").GetString()
                    ?? throw new InvalidOperationException("Sin token");

        lock (Tokens)
            Tokens[realm] = token;

        return token;
    }

    private static async Task<string[]> PlaybackAsync(string token, string channelId, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"https://public.aurora.enhanced.live/playback/channelPlaybackInfo/{Uri.EscapeDataString(channelId)}?usePreAuth=true");
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
            return [];

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);

        var hls = new List<string>();
        var dash = new List<string>();
        foreach (var item in document.RootElement.GetProperty("data").GetProperty("attributes").GetProperty("streaming").EnumerateArray())
        {
            var url = item.TryGetProperty("url", out var u) ? u.GetString() : null;
            if (string.IsNullOrWhiteSpace(url))
                continue;

            // Con DRM no se puede: la constitucion solo admite emisiones abiertas.
            if (item.TryGetProperty("protection", out var protection) &&
                protection.TryGetProperty("drmEnabled", out var drm) && drm.ValueKind == JsonValueKind.True)
                continue;

            var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
            (type == "hls" ? hls : dash).Add(url);
        }

        return [.. hls, .. dash];
    }
}
