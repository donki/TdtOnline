using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TdtOnline.Models;
using TdtOnline.Services;
using TdtOnline.Tests.Fakes;

namespace TdtOnline.Tests;

/// <summary>
/// La carga completa, sin red: las listas son direcciones ftp://, que HttpClient rechaza al momento
/// sin abrir ninguna conexion, y asi la carga cae a la copia guardada (o la da por fallida).
/// </summary>
public class ChannelCatalogLoadTests
{
    private const string ListA = "ftp://tests.invalid/lista-a.json";
    private const string ListB = "ftp://tests.invalid/lista-b.m3u";

    private const string OwnJson = """
        {"categories":[
          {"name":"Generalistas","channels":[
            {"name":"La 1","urls":["https://a/la1.m3u8"]},
            {"name":"DMAX","resolver":"sonic:es:1"}
          ]}
        ]}
        """;

    private const string M3u = "#EXTM3U\n" +
                               "#EXTINF:-1 group-title=\"Generalistas\",LA 1\nhttps://b/la1.m3u8\n" +
                               "#EXTINF:-1 group-title=\"Musica\",Hit TV\nhttps://h/hit.m3u8\n";

    private static string CachePath(string dir, string url) =>
        Path.Combine(dir, $"lista-{Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(url)))[..16].ToLowerInvariant()}.txt");

    private static ChannelCatalog Catalog(TempDir dir, params string[] lists) =>
        new(dir.Path, new UserPreferences(new MemoryPreferenceStore()) { ListUrls = lists });

    private static void WriteVerified(TempDir dir, params string[] resolvers) =>
        File.WriteAllText(dir.File("resolvers-ok.json"), JsonSerializer.Serialize(resolvers));

    [Fact]
    public async Task WithoutNetworkNorCopy_ListIsReportedAsFailed()
    {
        using var dir = new TempDir();
        var catalog = Catalog(dir, ListA);

        var result = await catalog.LoadAsync();

        Assert.Empty(result);
        Assert.Equal([ListA], catalog.FailedLists);
    }

    [Fact]
    public async Task WithoutNetwork_ReadsTheSavedCopiesAndMergesThem()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OwnJson);
        File.WriteAllText(CachePath(dir.Path, ListB), M3u);
        WriteVerified(dir, "sonic:es:1");
        var catalog = Catalog(dir, ListA, ListB);

        var result = await catalog.LoadAsync();

        Assert.Empty(catalog.FailedLists);
        Assert.Equal(["Generalistas", "Musica"], result.Select(c => c.Name));
        var la1 = result[0].Channels[0];
        Assert.Equal(["https://a/la1.m3u8", "https://b/la1.m3u8"], la1.StreamUrls);
        // DMAX entra porque su directo quedo comprobado hace menos de un dia.
        Assert.Equal(["La 1", "DMAX"], result[0].Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task UnreadableList_IsReportedAndTheOthersStillLoad()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), "{\"desconocido\":true}");
        File.WriteAllText(CachePath(dir.Path, ListB), M3u);
        var catalog = Catalog(dir, ListA, ListB);

        var result = await catalog.LoadAsync();

        Assert.Equal([ListA], catalog.FailedLists);
        Assert.Equal(["Generalistas", "Musica"], result.Select(c => c.Name));
    }

    [Fact]
    public async Task UnverifiedResolver_IsHiddenAndCheckedInTheBackground()
    {
        using var dir = new TempDir();
        // Un resolvedor que SonicLive no atiende: se comprueba sin red y sale como no disponible.
        File.WriteAllText(CachePath(dir.Path, ListA), """
            {"categories":[{"name":"G","channels":[
              {"name":"Uno","urls":["https://u/1.m3u8"]},
              {"name":"Raro","resolver":"otro:es:1"}
            ]}]}
            """);
        var catalog = Catalog(dir, ListA);
        var updated = new TaskCompletionSource<IReadOnlyList<Category>>(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Updated += list => updated.TrySetResult(list);

        var result = await catalog.LoadAsync();
        var afterCheck = await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(["Uno"], result.Single().Channels.Select(c => c.Name));
        Assert.Equal(["Uno"], afterCheck.Single().Channels.Select(c => c.Name));
        Assert.True(File.Exists(dir.File("resolvers-ok.json")));
        Assert.Empty(JsonSerializer.Deserialize<string[]>(File.ReadAllText(dir.File("resolvers-ok.json")))!);
    }

    private const string OtherResolverList = """{"categories":[{"name":"G","channels":[{"name":"X","resolver":"otro:9"},{"name":"Y","urls":["u"]}]}]}""";

    /// <summary>Carga y espera a que termine la comprobacion en segundo plano (sin red: «otro:» no lo atiende nadie).</summary>
    private static async Task<IReadOnlyList<Category>> LoadAndWaitCheckAsync(ChannelCatalog catalog, bool forceRefresh = false)
    {
        var updated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        catalog.Updated += _ => updated.TrySetResult();
        var result = await catalog.LoadAsync(forceRefresh);
        await updated.Task.WaitAsync(TimeSpan.FromSeconds(10));
        return result;
    }

    [Fact]
    public async Task FreshVerification_ShowsTheResolverChannel()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OtherResolverList);
        WriteVerified(dir, "OTRO:9");

        var result = await Catalog(dir, ListA).LoadAsync();

        Assert.Equal(["X", "Y"], result.Single().Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task ForceRefresh_IgnoresTheVerification()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OtherResolverList);
        WriteVerified(dir, "otro:9");

        var result = await LoadAndWaitCheckAsync(Catalog(dir, ListA), forceRefresh: true);

        Assert.Equal(["Y"], result.Single().Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task VerificationOlderThanADay_IsIgnored()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OtherResolverList);
        WriteVerified(dir, "otro:9");
        File.SetLastWriteTimeUtc(dir.File("resolvers-ok.json"), DateTime.UtcNow.AddDays(-2));

        var result = await LoadAndWaitCheckAsync(Catalog(dir, ListA));

        Assert.Equal(["Y"], result.Single().Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task CorruptVerification_IsIgnored()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OtherResolverList);
        File.WriteAllText(dir.File("resolvers-ok.json"), "no es json");

        var result = await LoadAndWaitCheckAsync(Catalog(dir, ListA));

        Assert.Equal(["Y"], result.Single().Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task NullVerificationFile_IsIgnored()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OtherResolverList);
        File.WriteAllText(dir.File("resolvers-ok.json"), "null");

        var result = await LoadAndWaitCheckAsync(Catalog(dir, ListA));

        Assert.Equal(["Y"], result.Single().Channels.Select(c => c.Name));
    }

    [Fact]
    public async Task WithoutResolvers_NoBackgroundCheckIsStarted()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListB), M3u);
        var catalog = Catalog(dir, ListB);
        var updates = 0;
        catalog.Updated += _ => updates++;

        await catalog.LoadAsync();
        await Task.Delay(200);

        Assert.Equal(0, updates);
        Assert.False(File.Exists(dir.File("resolvers-ok.json")));
    }

    // ------------------------------------------------------------------ Con descarga (solo 127.0.0.1)

    [Fact]
    public async Task Download_ReplacesTheSavedCopyAndCreatesTheFolder()
    {
        using var dir = new TempDir();
        using var server = new LoopbackServer(M3u);
        var cacheDir = Path.Combine(dir.Path, "cache", "listas");
        var catalog = new ChannelCatalog(cacheDir, new UserPreferences(new MemoryPreferenceStore()) { ListUrls = [server.Url] });

        var result = await catalog.LoadAsync();

        Assert.Equal(1, server.Requests);
        Assert.Empty(catalog.FailedLists);
        Assert.Equal(["Generalistas", "Musica"], result.Select(c => c.Name));
        Assert.Equal(M3u, File.ReadAllText(CachePath(cacheDir, server.Url)));
    }

    [Theory]
    [InlineData("   ", 200)]
    [InlineData("no esta", 404)]
    public async Task EmptyOrFailedDownload_FallsBackToTheSavedCopy(string body, int status)
    {
        using var dir = new TempDir();
        using var server = new LoopbackServer(body, status);
        File.WriteAllText(CachePath(dir.Path, server.Url), M3u);
        var catalog = Catalog(dir, server.Url);

        var result = await catalog.LoadAsync();

        Assert.Equal(1, server.Requests);
        Assert.Equal(["Generalistas", "Musica"], result.Select(c => c.Name));
        Assert.Equal(M3u, File.ReadAllText(CachePath(dir.Path, server.Url)));
    }

    [Fact]
    public async Task ClearCache_RemovesSavedListsAndVerification()
    {
        using var dir = new TempDir();
        File.WriteAllText(CachePath(dir.Path, ListA), OwnJson);
        WriteVerified(dir, "sonic:es:1");
        File.WriteAllText(dir.File("otra-cosa.txt"), "se queda");
        var catalog = Catalog(dir, ListA);

        catalog.ClearCache();

        Assert.False(File.Exists(CachePath(dir.Path, ListA)));
        Assert.False(File.Exists(dir.File("resolvers-ok.json")));
        Assert.True(File.Exists(dir.File("otra-cosa.txt")));
        Assert.Empty(await catalog.LoadAsync());
        Assert.Equal([ListA], catalog.FailedLists);
    }

    [Fact]
    public void ClearCache_OnMissingFolderDoesNotThrow()
    {
        var catalog = new ChannelCatalog(Path.Combine(Path.GetTempPath(), "tdtonline-tests", "no-existe-" + Guid.NewGuid()), new UserPreferences(new MemoryPreferenceStore()));

        catalog.ClearCache();
    }
}
