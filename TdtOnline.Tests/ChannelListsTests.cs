using TdtOnline.Models;
using TdtOnline.Services;

namespace TdtOnline.Tests;

/// <summary>Busqueda, favoritos, «Todos» y las listas de Ajustes.</summary>
public class ChannelListsTests
{
    private static Channel Ch(string name) => new() { Name = name, StreamUrls = ["https://x/" + name] };

    private static readonly Category[] Categories =
    [
        new("Generalistas", [Ch("La 1"), Ch("Antena 3"), Ch("Aragón TV")]),
        new("Autonómicas", [Ch("ARAGÓN TV"), Ch("À Punt"), Ch("Telemadrid")]),
        new("Vacía", []),
    ];

    [Theory]
    [InlineData("Aragón TV", "aragontv")]
    [InlineData("À Punt", "apunt")]
    [InlineData("  ¡Cuatro!  ", "cuatro")]
    [InlineData("", "")]
    public void Normalize_DropsAccentsCaseSpacesAndSigns(string text, string expected)
    {
        Assert.Equal(expected, ChannelLists.Normalize(text));
    }

    [Fact]
    public void AllChannels_EachNameOnceInListOrder()
    {
        var all = ChannelLists.AllChannels(Categories);

        Assert.Equal(["La 1", "Antena 3", "Aragón TV", "À Punt", "Telemadrid"], all.Select(c => c.Name));
    }

    [Fact]
    public void AllChannels_EmptyListGivesEmpty()
    {
        Assert.Empty(ChannelLists.AllChannels([]));
    }

    [Theory]
    [InlineData("aragon", new[] { "Aragón TV" })]
    [InlineData("a p", new[] { "À Punt" })]
    [InlineData("ANTENA3", new[] { "Antena 3" })]
    [InlineData("a", new[] { "La 1", "Antena 3", "Aragón TV", "À Punt", "Telemadrid" })]
    [InlineData("zzz", new string[0])]
    public void Search_IgnoresAccentsCaseAndSpaces(string text, string[] expected)
    {
        Assert.Equal(expected, ChannelLists.Search(Categories, text).Select(c => c.Name));
    }

    [Fact]
    public void Search_WithOnlySignsMatchesEverything()
    {
        // El buscador recorta espacios; un texto de solo signos queda vacio y lo contiene todo.
        Assert.Equal(5, ChannelLists.Search(Categories, "¿?").Count);
    }

    [Fact]
    public void Favorites_InListOrderOnceAndCaseInsensitive()
    {
        var favorites = new HashSet<string>(["telemadrid", "ARAGÓN TV", "No existe"], StringComparer.OrdinalIgnoreCase);

        var result = ChannelLists.Favorites(Categories, favorites);

        Assert.Equal(["Aragón TV", "Telemadrid"], result.Select(c => c.Name));
    }

    [Fact]
    public void Favorites_NoneGivesEmpty()
    {
        Assert.Empty(ChannelLists.Favorites(Categories, new HashSet<string>()));
    }

    [Fact]
    public void ToggleFavorite_AddsRemovesAndIgnoresBlank()
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        Assert.True(ChannelLists.ToggleFavorite(set, "La 1"));
        Assert.Contains("la 1", set);
        Assert.False(ChannelLists.ToggleFavorite(set, "LA 1"));
        Assert.Empty(set);
        Assert.False(ChannelLists.ToggleFavorite(set, "  "));
        Assert.False(ChannelLists.ToggleFavorite(set, ""));
        Assert.Empty(set);
    }

    [Theory]
    [InlineData("https://raw.githubusercontent.com/a/b/main/lista.m3u?token=1", "raw.githubusercontent.com/a/b/main/lista.m3u")]
    [InlineData("http://host", "host/")]
    [InlineData("no es una direccion", "no es una direccion")]
    public void ShortUrl_HostAndPathWithoutQuery(string url, string expected)
    {
        Assert.Equal(expected, ChannelLists.ShortUrl(url));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \n \n")]
    public void ParseListUrls_NeverEmpty(string? raw)
    {
        Assert.Equal([ChannelCatalog.DefaultListUrl], ChannelLists.ParseListUrls(raw));
    }

    [Fact]
    public void ParseListUrls_OnePerLineTrimmedInOrder()
    {
        Assert.Equal(["https://b/2.m3u", "https://a/1.json"], ChannelLists.ParseListUrls(" https://b/2.m3u \r\n\nhttps://a/1.json\n"));
    }

    [Theory]
    [InlineData("https://x/lista.m3u", ChannelLists.NewListCheck.Ok)]
    [InlineData("http://x/lista.m3u", ChannelLists.NewListCheck.Ok)]
    [InlineData("HTTPS://A/1.JSON", ChannelLists.NewListCheck.Exists)]
    [InlineData("ftp://x/lista.m3u", ChannelLists.NewListCheck.Invalid)]
    [InlineData("file:///c:/lista.m3u", ChannelLists.NewListCheck.Invalid)]
    [InlineData("lista.m3u", ChannelLists.NewListCheck.Invalid)]
    [InlineData("", ChannelLists.NewListCheck.Invalid)]
    public void CheckNewList_OnlyNewHttpAddresses(string text, ChannelLists.NewListCheck expected)
    {
        Assert.Equal(expected, ChannelLists.CheckNewList(text, ["https://a/1.json"]));
    }
}
