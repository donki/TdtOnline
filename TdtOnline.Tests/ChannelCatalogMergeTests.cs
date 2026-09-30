using TdtOnline.Services;
using Draft = TdtOnline.Services.ChannelCatalog.Draft;

namespace TdtOnline.Tests;

/// <summary>Como se juntan varias listas y que canales llegan a la pantalla.</summary>
public class ChannelCatalogMergeTests
{
    private static Draft D(string name, string category, params string[] urls) =>
        new() { Name = name, Category = category, Urls = [.. urls] };

    private static List<(string Name, List<Draft> Channels)> Cats(params (string Name, Draft[] Channels)[] cats) =>
        cats.Select(c => (c.Name, c.Channels.ToList())).ToList();

    [Theory]
    [InlineData("Aragón TV", "aragontv")]
    [InlineData("  LA 1 HD! ", "la1hd")]
    [InlineData("Señal Ñ", "senaln")]
    [InlineData("", "")]
    public void Key_IgnoresCaseAccentsSpacesAndSigns(string name, string expected)
    {
        Assert.Equal(expected, ChannelCatalog.Key(name));
    }

    [Fact]
    public void Merge_IntoEmptyTakesTheFirstListAsIs()
    {
        var categories = new List<(string Name, List<Draft> Channels)>();
        var first = Cats(("A", [D("Uno", "A", "u1")]));

        ChannelCatalog.Merge(categories, first);

        Assert.Same(first[0].Channels, categories[0].Channels);
    }

    [Fact]
    public void Merge_SameChannelAddsBackupUrlsAndFillsMissingData()
    {
        var la1 = D("La 1", "Generalistas", "https://a/la1.m3u8");
        la1.LogoUrl = "logo-original";
        var categories = Cats(("Generalistas", [la1]));

        var other = D("LA 1", "Otra", "HTTPS://A/LA1.M3U8", "https://b/la1.m3u8");
        other.LogoUrl = "logo-otro";
        other.EpgId = "La 1.TV";
        other.Resolver = "sonic:es:9";

        ChannelCatalog.Merge(categories, Cats(("Otra", [other])));

        // No se crea la categoria «Otra»: el canal ya existia.
        Assert.Single(categories);
        Assert.Equal(["https://a/la1.m3u8", "https://b/la1.m3u8"], la1.Urls);
        Assert.Equal("logo-original", la1.LogoUrl);
        Assert.Equal("La 1.TV", la1.EpgId);
        Assert.Equal("sonic:es:9", la1.Resolver);
    }

    [Fact]
    public void Merge_NewChannelsGoToSameNamedCategoryOrToANewOneAtTheEnd()
    {
        var categories = Cats(("Generalistas", [D("La 1", "Generalistas", "u")]), ("Deportes", []));

        ChannelCatalog.Merge(categories, Cats(
            ("GENERALISTAS", [D("Trece", "GENERALISTAS", "t")]),
            ("Musica", [D("Hit TV", "Musica", "h"), D("Sol Musica", "Musica", "s")])));

        Assert.Equal(["Generalistas", "Deportes", "Musica"], categories.Select(c => c.Name));
        Assert.Equal(["La 1", "Trece"], categories[0].Channels.Select(c => c.Name));
        Assert.Equal(["Hit TV", "Sol Musica"], categories[2].Channels.Select(c => c.Name));
    }

    [Fact]
    public void Merge_DuplicatesInsideTheExtraListAreJoinedToo()
    {
        var categories = Cats(("A", [D("Uno", "A", "1")]));

        ChannelCatalog.Merge(categories, Cats(("B", [D("Dos", "B", "2a"), D("DOS", "B", "2b")])));

        var dos = categories[1].Channels.Single();
        Assert.Equal(["2a", "2b"], dos.Urls);
    }

    [Fact]
    public void Finish_HidesChannelsWithoutUrlsAndEmptyCategories()
    {
        var withUrl = D("Uno", "A", "u");
        withUrl.LogoUrl = "l";
        withUrl.Web = "w";
        withUrl.EpgId = "e";
        withUrl.Country = "Spain";
        var categories = Cats(("A", [withUrl, D("Sin", "A")]), ("Vacia", [D("Nada", "Vacia")]));

        var result = ChannelCatalog.Finish(categories, verified: null);

        var cat = Assert.Single(result);
        Assert.Equal("A", cat.Name);
        var ch = Assert.Single(cat.Channels);
        Assert.Equal("Uno", ch.Name);
        Assert.Equal("l", ch.LogoUrl);
        Assert.Equal("w", ch.Web);
        Assert.Equal("e", ch.EpgId);
        Assert.Equal("A", ch.Category);
        Assert.Equal("Spain", ch.Country);
        Assert.Equal(["u"], ch.StreamUrls);
    }

    [Fact]
    public void Finish_ResolverChannelsOnlyWhenVerified()
    {
        var dmax = D("DMAX", "A");
        dmax.Resolver = "sonic:es:1";
        var otro = D("Otro", "A");
        otro.Resolver = "sonic:es:2";

        Assert.Empty(ChannelCatalog.Finish(Cats(("A", [dmax, otro])), verified: null));
        Assert.Empty(ChannelCatalog.Finish(Cats(("A", [dmax, otro])), verified: []));

        var result = ChannelCatalog.Finish(Cats(("A", [dmax, otro])), new HashSet<string>(["SONIC:ES:1"], StringComparer.OrdinalIgnoreCase));

        var ch = Assert.Single(Assert.Single(result).Channels);
        Assert.Equal("DMAX", ch.Name);
        Assert.Equal("sonic:es:1", ch.Resolver);
        Assert.Empty(ch.StreamUrls);
    }
}
