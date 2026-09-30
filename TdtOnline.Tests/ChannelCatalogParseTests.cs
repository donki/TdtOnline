using System.Text.Json;
using TdtOnline.Services;

namespace TdtOnline.Tests;

/// <summary>Los tres formatos de lista: JSON propio, JSON de TDTChannels y M3U.</summary>
public class ChannelCatalogParseTests
{
    // ------------------------------------------------------------------ JSON propio

    [Fact]
    public void Own_ReadsCategoriesChannelsAndAllFields()
    {
        const string json = """
        {"categories":[
          {"name":"Generalistas","channels":[
            {"name":"La 1","logo":"https://l/la1.png","web":"https://rtve.es","epg_id":"La 1.TV","urls":["https://a/la1.m3u8","","  ","https://b/la1.mpd"]},
            {"name":"DMAX","resolver":"sonic:es:1"}
          ]},
          {"name":"Vacia"}
        ]}
        """;

        var parsed = ChannelCatalog.Parse(json);

        Assert.Equal(2, parsed.Count);
        Assert.Equal("Generalistas", parsed[0].Name);
        var la1 = parsed[0].Channels[0];
        Assert.Equal("La 1", la1.Name);
        Assert.Equal("https://l/la1.png", la1.LogoUrl);
        Assert.Equal("https://rtve.es", la1.Web);
        Assert.Equal("La 1.TV", la1.EpgId);
        Assert.Equal(["https://a/la1.m3u8", "https://b/la1.mpd"], la1.Urls);
        Assert.Equal("Generalistas", la1.Category);
        Assert.Equal("Spain", la1.Country);
        Assert.Null(la1.Resolver);

        var dmax = parsed[0].Channels[1];
        Assert.Equal("sonic:es:1", dmax.Resolver);
        Assert.Empty(dmax.Urls);
        Assert.Null(dmax.LogoUrl);

        Assert.Equal("Vacia", parsed[1].Name);
        Assert.Empty(parsed[1].Channels);
    }

    [Fact]
    public void Own_NonStringResolverAndMissingNamesAreTolerated()
    {
        const string json = """{"categories":[{"channels":[{"resolver":42,"urls":["https://x/y.m3u8"]}]}]}""";

        var parsed = ChannelCatalog.Parse(json);

        Assert.Equal(string.Empty, parsed[0].Name);
        Assert.Equal(string.Empty, parsed[0].Channels[0].Name);
        Assert.Null(parsed[0].Channels[0].Resolver);
    }

    // ------------------------------------------------------------------ TDTChannels

    [Fact]
    public void TdtChannels_KeepsOnlyHlsAndDashWithoutTemplates()
    {
        const string json = """
        {"countries":[
          {"name":"Spain","ambits":[
            {"name":"Generalistas","channels":[
              {"name":"Antena 3","logo":"https://l/a3.png","web":"https://a3","epg_id":"Antena 3.TV","options":[
                {"format":"m3u8","url":"https://a/a3.m3u8"},
                {"format":"mpd","url":"https://a/a3.mpd"},
                {"format":"youtube","url":"https://youtube.com/x"},
                {"format":"m3u8","url":"https://ads/[CACHEBUSTER]/a3.m3u8"},
                {"format":"m3u8","url":""},
                {"url":"https://sin/formato.m3u8"}
              ]},
              {"name":"Sin opciones"}
            ]},
            {"name":"Sin canales"}
          ]},
          {"name":"Andorra","ambits":[{"name":"Generalistas","channels":[{"name":"ATV","options":[{"format":"m3u8","url":"https://atv/a.m3u8"}]}]}]},
          {"name":"Sin ambitos"}
        ]}
        """;

        var parsed = ChannelCatalog.Parse(json);

        Assert.Equal(["Generalistas", "Andorra · Generalistas"], parsed.Select(c => c.Name));
        var a3 = parsed[0].Channels[0];
        Assert.Equal(["https://a/a3.m3u8", "https://a/a3.mpd"], a3.Urls);
        Assert.Equal("https://l/a3.png", a3.LogoUrl);
        Assert.Equal("https://a3", a3.Web);
        Assert.Equal("Antena 3.TV", a3.EpgId);
        Assert.Equal("Spain", a3.Country);
        Assert.Equal("Generalistas", a3.Category);
        Assert.Empty(parsed[0].Channels[1].Urls);

        var atv = parsed[1].Channels.Single();
        Assert.Equal("Andorra", atv.Country);
        Assert.Equal("Generalistas", atv.Category);
    }

    [Fact]
    public void TdtChannels_CountryWithoutNameThrows()
    {
        // La carga lo recoge y marca la lista como fallida (ver ChannelCatalogLoadTests).
        Assert.Throws<KeyNotFoundException>(() => ChannelCatalog.Parse("""{"countries":[{"ambits":[]}]}"""));
    }

    // ------------------------------------------------------------------ M3U

    [Fact]
    public void M3u_GroupsByGroupTitleAndReadsAttributes()
    {
        const string m3u = "#EXTM3U\r\n" +
                           "#EXTINF:-1 tvg-id=\"La1.es\" tvg-logo=\"https://l/la1.png\" group-title=\"Generalistas\",La 1 Ⓢ\r\n" +
                           "#EXTVLCOPT:http-user-agent=x\r\n" +
                           "https://a/la1.m3u8\r\n" +
                           "https://b/la1-backup.m3u8\r\n" +
                           "\r\n" +
                           "#EXTINF:-1 group-title=\"generalistas\" tvg-logo=\"\",Antena 3 Ⓖ Ⓨ\n" +
                           "http://a/a3.m3u8\n" +
                           "#EXTINF:-1,Sin grupo\n" +
                           "https://s/g.m3u8\n";

        var parsed = ChannelCatalog.Parse(m3u);

        Assert.Equal(["Generalistas", "M3U"], parsed.Select(c => c.Name));
        var la1 = parsed[0].Channels[0];
        Assert.Equal("La 1", la1.Name);
        Assert.Equal("La1.es", la1.EpgId);
        Assert.Equal("https://l/la1.png", la1.LogoUrl);
        Assert.Equal(["https://a/la1.m3u8", "https://b/la1-backup.m3u8"], la1.Urls);

        // group-title sin mirar mayusculas: va a la misma categoria.
        var a3 = parsed[0].Channels[1];
        Assert.Equal("Antena 3", a3.Name);
        Assert.Null(a3.LogoUrl);
        Assert.Equal("generalistas", a3.Category);

        Assert.Equal("Sin grupo", parsed[1].Channels.Single().Name);
    }

    [Fact]
    public void M3u_NameMayContainCommasAndAttributesToo()
    {
        // Fallo real (2026-09-29): el nombre se cortaba por la ultima coma («Canal 4, Madrid» → «Madrid»).
        const string m3u = "#EXTM3U\n" +
                           "#EXTINF:-1 group-title=\"Cine,Series\" tvg-name=\"C4\",Canal 4, Madrid\n" +
                           "https://c4/a.m3u8\n" +
                           "#EXTINF:-1,Telemadrid, la de todos\n" +
                           "https://tm/a.m3u8\n";

        var parsed = ChannelCatalog.Parse(m3u);

        Assert.Equal(["Cine,Series", "M3U"], parsed.Select(c => c.Name));
        Assert.Equal("Canal 4, Madrid", parsed[0].Channels.Single().Name);
        Assert.Equal("Telemadrid, la de todos", parsed[1].Channels.Single().Name);
    }

    [Fact]
    public void M3u_WithoutCommaUsesTvgName()
    {
        const string m3u = "#EXTINF:-1 tvg-name=\"Trece\"\nhttps://t/13.m3u8\n";

        var parsed = ChannelCatalog.Parse(m3u);

        Assert.Equal("Trece", parsed.Single().Channels.Single().Name);
    }

    [Fact]
    public void M3u_SkipsEmptyNamesYoutubeNonHttpAndOrphanUrls()
    {
        const string m3u = "#EXTM3U\n" +
                           "https://huerfana/sin-extinf.m3u8\n" +
                           "#EXTINF:-1,Ⓢ\n" +                          // nombre vacio tras quitar la marca
                           "https://vacio/a.m3u8\n" +
                           "#EXTINF:-1,Solo YouTube\n" +
                           "https://www.youtube.com/watch?v=1\n" +
                           "https://youtu.be/1\n" +
                           "rtmp://no/soportado\n" +
                           "#EXTINF:-1\n" +                            // ni coma ni tvg-name
                           "https://anonimo/a.m3u8\n" +
                           "#EXTINF:-1,Bueno\n" +
                           "HTTPS://bueno/a.m3u8\n";

        var parsed = ChannelCatalog.Parse(m3u);

        var channel = parsed.Single().Channels.Single();
        Assert.Equal("Bueno", channel.Name);
        Assert.Equal(["HTTPS://bueno/a.m3u8"], channel.Urls);
    }

    [Fact]
    public void M3u_IsRecognisedByExtinfAndLeadingWhitespace()
    {
        var parsed = ChannelCatalog.Parse("  \n#extinf:-1,Uno\nhttps://u/1.m3u8");

        Assert.Equal("Uno", parsed.Single().Channels.Single().Name);
    }

    // ------------------------------------------------------------------ Errores

    [Fact]
    public void UnknownJsonThrows()
    {
        Assert.Throws<InvalidDataException>(() => ChannelCatalog.Parse("""{"otra":1}"""));
    }

    [Fact]
    public void GarbageThrows()
    {
        Assert.ThrowsAny<JsonException>(() => ChannelCatalog.Parse("esto no es una lista"));
    }
}
