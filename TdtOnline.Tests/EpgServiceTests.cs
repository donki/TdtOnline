using System.Text.Json;
using TdtOnline.Models;
using TdtOnline.Services;

namespace TdtOnline.Tests;

/// <summary>La guia de programacion (TV.json de TDTChannels): lectura, cache y consultas.</summary>
public class EpgServiceTests
{
    private static readonly long Now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

    private static object Ev(string title, long start, long end, string? desc = null) =>
        desc is null ? new { t = title, hi = start, hf = end } : new { t = title, d = desc, hi = start, hf = end };

    private static string Guide(params (string Name, object[] Events)[] channels) =>
        JsonSerializer.Serialize(channels.Select(c => new { name = c.Name, events = c.Events }));

    private static EpgService Parsed(string json)
    {
        var epg = new EpgService(Path.GetTempPath());
        epg.ParseEpg(json);
        return epg;
    }

    [Fact]
    public void Parse_SortsEventsAndIsCaseInsensitive()
    {
        var epg = Parsed(Guide(("La 1.TV", [
            Ev("Despues", Now + 3600, Now + 7200),
            Ev("Ahora", Now - 600, Now + 3600, "Informativo"),
            Ev("Antes", Now - 7200, Now - 600),
        ])));

        Assert.True(epg.Has("la 1.tv"));
        var current = epg.GetCurrentProgram("LA 1.TV");
        Assert.NotNull(current);
        Assert.Equal("Ahora", current.Title);
        Assert.Equal("Informativo", current.Description);

        var upcoming = epg.GetUpcoming("La 1.TV", 5);
        Assert.Equal(["Ahora", "Despues"], upcoming.Select(p => p.Title));
        Assert.Equal(["Ahora"], epg.GetUpcoming("La 1.TV", 1).Select(p => p.Title));
    }

    [Fact]
    public void Parse_SkipsInvalidEventsAndChannels()
    {
        var json = """
            [
              {"name":"Bueno","events":[
                {"t":"Valido","hi":NOW1,"hf":NOW2},
                {"t":"","hi":NOW1,"hf":NOW2},
                {"t":"Sin inicio","hf":NOW2},
                {"t":"Sin fin","hi":NOW1},
                {"t":"Cero","hi":0,"hf":NOW2},
                {"t":null,"d":null,"hi":NOW1,"hf":NOW2},
                {"hi":NOW1,"hf":NOW2}
              ]},
              {"name":"","events":[{"t":"x","hi":1,"hf":2}]},
              {"name":"Sin eventos"},
              {"name":"Eventos no lista","events":{}},
              {"events":[]},
              {"name":"Todos malos","events":[{"t":"","hi":1,"hf":2}]}
            ]
            """.Replace("NOW1", (Now - 10).ToString()).Replace("NOW2", (Now + 10).ToString());

        var epg = Parsed(json);

        Assert.True(epg.Has("Bueno"));
        Assert.Equal(["Valido"], epg.GetUpcoming("Bueno", 10).Select(p => p.Title));
        Assert.Equal(string.Empty, epg.GetCurrentProgram("Bueno")!.Description);
        Assert.False(epg.Has("Sin eventos"));
        Assert.False(epg.Has("Eventos no lista"));
        Assert.False(epg.Has("Todos malos"));
    }

    [Fact]
    public void Parse_OneEventWithABadTimeDoesNotLoseTheWholeGuide()
    {
        // Fallo real (2026-09-29): GetInt64 lanzaba con una hora en texto o con decimales y se
        // perdia la guia de todos los canales.
        var json = $$"""
            [
              {"name":"A","events":[{"t":"Texto","hi":"{{Now}}","hf":{{Now + 60}}},{"t":"Decimal","hi":{{Now}}.5,"hf":{{Now + 60}}},{"t":"Bien","hi":{{Now - 1}},"hf":{{Now + 60}}}]},
              {"name":"B","events":[{"t":"Otro","hi":{{Now - 1}},"hf":{{Now + 60}}}]}
            ]
            """;

        var epg = Parsed(json);

        Assert.Equal(["Bien"], epg.GetUpcoming("A", 10).Select(p => p.Title));
        Assert.True(epg.Has("B"));
    }

    [Theory]
    [InlineData("{\"no\":\"es una lista\"}")]
    [InlineData("esto no es json")]
    public void Parse_NonArrayOrCorruptKeepsThePreviousGuide(string bad)
    {
        var epg = Parsed(Guide(("A", [Ev("Uno", Now - 1, Now + 60)])));

        epg.ParseEpg(bad);

        Assert.True(epg.Has("A"));
    }

    [Fact]
    public void Parse_NewGuideReplacesTheOldOne()
    {
        var epg = Parsed(Guide(("A", [Ev("Uno", Now - 1, Now + 60)])));

        epg.ParseEpg(Guide(("B", [Ev("Dos", Now - 1, Now + 60)])));

        Assert.False(epg.Has("A"));
        Assert.True(epg.Has("B"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Desconocido")]
    public void Queries_WithoutGuideAreEmpty(string? id)
    {
        var epg = Parsed(Guide(("A", [Ev("Uno", Now - 1, Now + 60)])));

        Assert.False(epg.Has(id));
        Assert.Empty(epg.GetUpcoming(id, 3));
        Assert.Null(epg.GetCurrentProgram(id));
    }

    [Fact]
    public void CurrentProgram_WhenNothingIsOnReturnsTheNextOrNull()
    {
        var epg = Parsed(Guide(
            ("Hueco", [Ev("Pasado", Now - 7200, Now - 3600), Ev("Luego", Now + 600, Now + 1200), Ev("Mas tarde", Now + 1200, Now + 1800)]),
            ("Acabado", [Ev("Pasado", Now - 7200, Now - 3600)])));

        Assert.Equal("Luego", epg.GetCurrentProgram("Hueco")!.Title);
        Assert.Null(epg.GetCurrentProgram("Acabado"));
        Assert.Empty(epg.GetUpcoming("Acabado", 5));
        Assert.True(epg.Has("Acabado"));
    }

    [Fact]
    public void CurrentProgram_EndIsExclusive()
    {
        var epg = Parsed(Guide(("A", [Ev("Termina ya", Now - 3600, Now - 1), Ev("Empieza", Now - 1, Now + 3600)])));

        Assert.Equal("Empieza", epg.GetCurrentProgram("A")!.Title);
    }

    [Fact]
    public async Task Load_ReadsAFreshCacheWithoutNetworkAndNotifies()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("epg.json"), Guide(("A", [Ev("Uno", Now - 1, Now + 60)])));
        var epg = new EpgService(dir.Path);
        var loaded = 0;
        epg.EpgLoaded += () => loaded++;

        Assert.False(epg.IsLoaded);
        await epg.LoadAsync();

        Assert.True(epg.IsLoaded);
        Assert.True(epg.Has("A"));
        Assert.Equal(1, loaded);

        // Ya en memoria: no vuelve a leer (aunque el fichero cambie) pero avisa igual.
        File.WriteAllText(dir.File("epg.json"), Guide(("B", [Ev("Dos", Now - 1, Now + 60)])));
        await epg.LoadAsync();

        Assert.Equal(2, loaded);
        Assert.True(epg.Has("A"));
        Assert.False(epg.Has("B"));
    }

    [Fact]
    public async Task Load_EmptyCacheFileLeavesTheGuideUnloaded()
    {
        using var dir = new TempDir();
        File.WriteAllText(dir.File("epg.json"), string.Empty);
        var epg = new EpgService(dir.Path);
        var loaded = 0;
        epg.EpgLoaded += () => loaded++;

        await epg.LoadAsync();

        Assert.False(epg.IsLoaded);
        Assert.Equal(0, loaded);
    }

    [Fact]
    public void Shared_IsOneInstancePerProcess()
    {
        var a = EpgService.Shared(Path.GetTempPath());
        var b = EpgService.Shared(Path.Combine(Path.GetTempPath(), "otra"));

        Assert.Same(a, b);
    }

    [Fact]
    public void Program_TimesAreLocalAndFormatted()
    {
        var start = new DateTimeOffset(2026, 9, 29, 20, 5, 0, TimeSpan.Zero);
        var p = new EpgProgram("T", "D", start.ToUnixTimeSeconds(), start.AddMinutes(95).ToUnixTimeSeconds());

        Assert.Equal(start.ToLocalTime(), p.StartTime);
        Assert.Equal(start.AddMinutes(95).ToLocalTime(), p.EndTime);
        Assert.Equal($"{start.ToLocalTime():HH:mm} - {start.AddMinutes(95).ToLocalTime():HH:mm}", p.TimeRange);
        Assert.Matches(@"^\d\d:\d\d - \d\d:\d\d$", p.TimeRange);
    }
}
