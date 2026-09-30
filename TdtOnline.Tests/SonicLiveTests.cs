using System.Text.Json;
using TdtOnline.Services;

namespace TdtOnline.Tests;

/// <summary>
/// DMAX: lo que no sale a la red (la especificacion, leer las respuestas del servicio y la variante HLS).
/// Pedir el token y la emision a public.aurora.enhanced.live es red real y no se prueba aqui.
/// </summary>
public class SonicLiveTests
{
    [Theory]
    [InlineData("sonic:es:1", true)]
    [InlineData("sonic:", true)]
    [InlineData("SONIC:es:1", false)]
    [InlineData("otro:es:1", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Handles_OnlyTheSonicPrefix(string? resolver, bool expected)
    {
        Assert.Equal(expected, SonicLive.Handles(resolver));
    }

    [Theory]
    [InlineData("sonic:es")]
    [InlineData("sonic:es:1:extra")]
    [InlineData("sonic")]
    public async Task Resolve_BadSpecGivesNothingWithoutNetwork(string resolver)
    {
        Assert.Empty(await SonicLive.ResolveAsync(resolver));
    }

    [Fact]
    public async Task Probe_BadSpecIsNotLive()
    {
        Assert.False(await SonicLive.ProbeAsync("sonic:es"));
    }

    // ------------------------------------------------------------------ Respuestas del servicio

    [Fact]
    public void StreamUrls_HlsFirstDashAfterAndDrmOut()
    {
        using var doc = JsonDocument.Parse("""
            {"data":{"attributes":{"streaming":[
              {"type":"dash","url":"https://d/1.mpd"},
              {"type":"hls","url":"https://h/1.m3u8","protection":{"drmEnabled":false}},
              {"type":"hls","url":"https://h/drm.m3u8","protection":{"drmEnabled":true}},
              {"type":"hls","url":""},
              {"type":"hls"},
              {"url":"https://sin-tipo/x"},
              {"type":"hls","url":"https://h/2.m3u8","protection":{}}
            ]}}}
            """);

        Assert.Equal(["https://h/1.m3u8", "https://h/2.m3u8", "https://d/1.mpd", "https://sin-tipo/x"], SonicLive.StreamUrls(doc.RootElement));
    }

    [Fact]
    public void StreamUrls_EmptyAndMalformed()
    {
        using var empty = JsonDocument.Parse("""{"data":{"attributes":{"streaming":[]}}}""");
        Assert.Empty(SonicLive.StreamUrls(empty.RootElement));

        using var bad = JsonDocument.Parse("""{"errors":[{"status":"401"}]}""");
        Assert.Throws<KeyNotFoundException>(() => SonicLive.StreamUrls(bad.RootElement));
    }

    [Fact]
    public void TokenFrom_ReadsTheTokenOrFails()
    {
        using var ok = JsonDocument.Parse("""{"data":{"attributes":{"token":"abc.def"}}}""");
        Assert.Equal("abc.def", SonicLive.TokenFrom(ok.RootElement));

        using var nul = JsonDocument.Parse("""{"data":{"attributes":{"token":null}}}""");
        Assert.Throws<InvalidOperationException>(() => SonicLive.TokenFrom(nul.RootElement));

        using var missing = JsonDocument.Parse("""{"data":{}}""");
        Assert.Throws<KeyNotFoundException>(() => SonicLive.TokenFrom(missing.RootElement));
    }

    [Fact]
    public void FirstVariant_RelativeAndAbsolute()
    {
        const string master = "https://cdn.example/live/master.m3u8?hdnts=token";

        Assert.Equal(new Uri("https://cdn.example/live/720p/index.m3u8"),
            SonicLive.FirstVariant(master, "#EXTM3U\r\n#EXT-X-STREAM-INF:BANDWIDTH=1\r\n\r\n  720p/index.m3u8  \r\n1080p/index.m3u8\r\n"));
        Assert.Equal(new Uri("https://otro/v.m3u8"), SonicLive.FirstVariant(master, "#EXTM3U\nhttps://otro/v.m3u8"));
        Assert.Null(SonicLive.FirstVariant(master, "#EXTM3U\n#EXT-X-ENDLIST\n"));
        Assert.Null(SonicLive.FirstVariant(master, string.Empty));
    }
}
