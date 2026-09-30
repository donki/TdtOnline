using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using TdtOnline.Localization;

namespace TdtOnline.Tests;

/// <summary>Textos en español e ingles: mismas claves, mismos huecos y el idioma elegido manda.</summary>
[Collection(nameof(LocCollection))]
public class LocTests : IDisposable
{
    private readonly string _override = Loc.Override;
    private readonly CultureInfo _ui = CultureInfo.CurrentUICulture;
    private readonly CultureInfo _culture = CultureInfo.CurrentCulture;

    public void Dispose()
    {
        Loc.Override = _override;
        CultureInfo.CurrentUICulture = _ui;
        CultureInfo.CurrentCulture = _culture;
    }

    private static Dictionary<string, string> Table(string name) =>
        (Dictionary<string, string>)typeof(Loc).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    [Fact]
    public void SpanishAndEnglishHaveTheSameKeys()
    {
        var es = Table("Spanish").Keys.Order().ToList();
        var en = Table("English").Keys.Order().ToList();

        Assert.Equal(en, es);
        Assert.NotEmpty(es);
    }

    [Fact]
    public void NoTextIsEmptyAndPlaceholdersMatch()
    {
        var es = Table("Spanish");
        var en = Table("English");
        foreach (var key in en.Keys)
        {
            Assert.False(string.IsNullOrWhiteSpace(en[key]), key);
            Assert.False(string.IsNullOrWhiteSpace(es[key]), key);
            var holesEn = Regex.Matches(en[key], @"\{\d+\}").Select(m => m.Value).Order();
            var holesEs = Regex.Matches(es[key], @"\{\d+\}").Select(m => m.Value).Order();
            Assert.True(holesEn.SequenceEqual(holesEs), $"Huecos distintos en «{key}»");
        }
    }

    [Fact]
    public void Override_WinsOverTheSystemLanguage()
    {
        CultureInfo.CurrentUICulture = new CultureInfo("en-US");
        Loc.Override = "es";
        Assert.Equal("es", Loc.Language);
        Assert.Equal("Ajustes", Loc.Get("Settings"));

        CultureInfo.CurrentUICulture = new CultureInfo("es-ES");
        Loc.Override = "en";
        Assert.Equal("en", Loc.Language);
        Assert.Equal("Settings", Loc.Get("Settings"));
    }

    [Theory]
    [InlineData("es-ES", "es", "Parrilla")]
    [InlineData("es-MX", "es", "Parrilla")]
    [InlineData("ca-ES", "en", "TV guide")]
    [InlineData("en-GB", "en", "TV guide")]
    [InlineData("fr-FR", "en", "TV guide")]
    public void WithoutOverride_FollowsTheSystemAndFallsBackToEnglish(string culture, string language, string guide)
    {
        Loc.Override = string.Empty;
        CultureInfo.CurrentUICulture = new CultureInfo(culture);

        Assert.Equal(language, Loc.Language);
        Assert.Equal(guide, Loc.Get("Guide"));
    }

    [Fact]
    public void UnknownKeyGivesEmpty()
    {
        Loc.Override = "es";
        Assert.Equal(string.Empty, Loc.Get("NoExiste"));
    }

    [Fact]
    public void Format_FillsThePlaceholders()
    {
        Loc.Override = "es";
        CultureInfo.CurrentCulture = new CultureInfo("es-ES");
        Assert.Equal("1234 canales", Loc.Format("ChannelsCount", 1234));
        Assert.Equal("1,5 canales", Loc.Format("ChannelsCount", 1.5));
        Assert.Equal("Ningún canal coincide con «xyz».", Loc.Format("NoResults", "xyz"));

        Loc.Override = "en";
        Assert.Equal("Now: Telediario", Loc.Format("NowPlaying", "Telediario"));
    }
}

[CollectionDefinition(nameof(LocCollection), DisableParallelization = true)]
public class LocCollection;
