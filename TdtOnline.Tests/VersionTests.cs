using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace TdtOnline.Tests;

/// <summary>
/// La version de la app (constitucion: fecha del dia y un numero): la visible y el codigo de
/// version de Android dicen lo mismo, y el CHANGELOG tiene su entrada.
/// </summary>
public class VersionTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TdtOnline.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encuentra TdtOnline.csproj");
    }

    private static (string Display, string Code) Versions()
    {
        var project = XDocument.Load(Path.Combine(RepoRoot(), "TdtOnline.csproj"));
        return (project.Descendants("ApplicationDisplayVersion").Single().Value,
                project.Descendants("ApplicationVersion").Single().Value);
    }

    [Fact]
    public void DisplayVersionIsADateAndANumber()
    {
        var (display, _) = Versions();

        var m = Regex.Match(display, @"^(\d{4})\.(\d{2})\.(\d{2})\.(\d+)$");
        Assert.True(m.Success, display);
        Assert.True(DateTime.TryParseExact($"{m.Groups[1]}-{m.Groups[2]}-{m.Groups[3]}", "yyyy-MM-dd",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out _), display);
    }

    [Fact]
    public void VersionCodeMatchesTheDisplayVersion()
    {
        var (display, code) = Versions();

        var parts = display.Split('.');
        Assert.Equal($"{parts[0]}{parts[1]}{parts[2]}{int.Parse(parts[3]):00}", code);
        Assert.InRange(long.Parse(code), 1, int.MaxValue); // Android: versionCode cabe en un int
    }

    [Fact]
    public void ChangelogHasTheCurrentVersion()
    {
        var (display, _) = Versions();

        var changelog = File.ReadAllText(Path.Combine(RepoRoot(), "CHANGELOG.md"));
        var first = Regex.Match(changelog, @"^## (\S+)", RegexOptions.Multiline);
        Assert.Equal(display, first.Groups[1].Value);
    }
}
