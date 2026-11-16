using System.Reflection;
using System.Text.RegularExpressions;
using TdtOnline.Localization;
using TdtOnline.Services;

namespace TdtOnline.Tests;

/// <summary>
/// Nada de emoji en la interfaz (General 6.2): los textos llevan marcas <c>{icon:nombre}</c> que la
/// pantalla cambia por iconos planos, y ni los textos ni los diseños ni el codigo de la app llevan
/// emoji. Antes la pastilla era «⭐ Favoritos», los botones de idioma «🇪🇸 Español» / «🇺🇸 English»,
/// el aviso legal «⚠️ …» y la ayuda de Ajustes «↻ … · ↶ …».
/// </summary>
public class InlineIconsTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "TdtOnline.csproj")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("No se encuentra TdtOnline.csproj");
    }

    private static Dictionary<string, string> Table(string name) =>
        (Dictionary<string, string>)typeof(Loc).GetField(name, BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;

    // ---------- Trocear ----------

    [Fact]
    public void Split_iconos_en_medio_y_texto_alrededor_en_orden()
    {
        var parts = InlineIcons.Split("{icon:refresh} descarga · {icon:restore} restaura");

        Assert.Equal(["refresh", null, "restore", null], parts.Select(p => p.Icon));
        Assert.Equal(" descarga · ", parts[1].Text);
        Assert.Equal(" restaura", parts[3].Text);
        Assert.Equal("{icon:refresh}", parts[0].Text); // el ImageSpan tapa justo la marca
    }

    [Fact]
    public void Split_sin_marcas_es_un_solo_trozo_de_texto()
    {
        var part = Assert.Single(InlineIcons.Split("Generalistas"));
        Assert.False(part.IsIcon);
        Assert.Equal("Generalistas", part.Text);
        Assert.Empty(InlineIcons.Split(string.Empty));
    }

    [Fact]
    public void Split_icono_que_no_existe_se_queda_como_texto()
    {
        var part = Assert.Single(InlineIcons.Split("{icon:nada} hola"));
        Assert.False(part.IsIcon);
        Assert.Equal("{icon:nada} hola", part.Text);
    }

    [Fact]
    public void Split_icono_al_final_y_dos_seguidos()
    {
        Assert.Equal(["flag_es", "flag_us"], InlineIcons.Split("{icon:flag_es}{icon:flag_us}").Select(p => p.Icon));
        Assert.Equal([null, "star"], InlineIcons.Split("Favoritos {icon:star}").Select(p => p.Icon));
    }

    [Fact]
    public void Plain_quita_las_marcas_para_el_lector_de_pantalla()
    {
        Assert.Equal("Español", InlineIcons.Plain("{icon:flag_es} Español"));
        Assert.Equal("Favoritos", InlineIcons.Plain("{icon:star} Favoritos"));
        Assert.Equal("a · b", InlineIcons.Plain("{icon:refresh} a · {icon:restore} b").Replace("  ", " "));
    }

    // ---------- Deteccion de emoji ----------

    [Theory]
    [InlineData("⭐ Favoritos")]
    [InlineData("🇪🇸 Español")]
    [InlineData("🇺🇸 English")]
    [InlineData("⚠️ Uso bajo su propio riesgo")]
    [InlineData("↻ vuelve a descargar")]
    [InlineData("↶ restaura")]
    [InlineData("★")]
    [InlineData("📺 Tele")]
    [InlineData("▶ Ver")]
    public void HasEmoji_ve_los_emoji_y_pictogramas(string text) => Assert.True(InlineIcons.HasEmoji(text));

    [Theory]
    [InlineData("Favoritos")]
    [InlineData("MIT License · Copyright © 2026 Socratic")]
    [InlineData("Cargando canales…")]
    [InlineData("Ningún canal coincide con «xyz».")]
    [InlineData("{icon:star} Favoritos")]
    public void HasEmoji_no_salta_con_la_tipografia_normal(string text) => Assert.False(InlineIcons.HasEmoji(text));

    // ---------- Textos de la app ----------

    [Theory]
    [InlineData("Spanish")]
    [InlineData("English")]
    public void Ningun_texto_lleva_emoji(string language)
    {
        foreach (var (key, text) in Table(language))
            Assert.False(InlineIcons.HasEmoji(text), $"«{key}» ({language}) lleva emoji: {text}");
    }

    [Fact]
    public void Las_marcas_son_iconos_que_existen_y_las_mismas_en_los_dos_idiomas()
    {
        var es = Table("Spanish");
        var en = Table("English");
        foreach (var key in en.Keys)
        {
            foreach (Match m in Regex.Matches(en[key] + es[key], @"\{icon:([a-z0-9_]+)\}"))
                Assert.True(InlineIcons.Drawables.ContainsKey(m.Groups[1].Value), $"«{key}»: icono desconocido {m.Value}");
            Assert.Equal(InlineIcons.Icons(en[key]), InlineIcons.Icons(es[key]));
        }
    }

    [Fact]
    public void Idioma_aviso_y_ayuda_de_ajustes_llevan_su_icono()
    {
        var en = Table("English");
        Assert.Equal(["flag_es"], InlineIcons.Icons(en["SpanishButton"]));
        Assert.Equal(["flag_us"], InlineIcons.Icons(en["EnglishButton"]));
        Assert.Equal(["warning"], InlineIcons.Icons(en["WarningText"]));
        Assert.Equal(["refresh", "restore"], InlineIcons.Icons(en["ActionsHint"]));
        Assert.Empty(InlineIcons.Icons(en["Favorites"])); // la estrella la pone la pastilla
    }

    // ---------- Dibujos, diseños y codigo ----------

    [Fact]
    public void Cada_icono_tiene_su_dibujo_vectorial_y_la_pantalla_lo_conoce()
    {
        var root = RepoRoot();
        var iconText = File.ReadAllText(Path.Combine(root, "Services", "IconText.cs"));
        foreach (var (name, drawable) in InlineIcons.Drawables)
        {
            var file = Path.Combine(root, "Resources", "drawable", drawable + ".xml");
            Assert.True(File.Exists(file), $"Falta {drawable}.xml para «{name}»");
            Assert.Contains("<vector", File.ReadAllText(file));
            Assert.Contains($"\"{name}\" => Resource.Drawable.{drawable}", iconText);
        }
    }

    [Fact]
    public void La_estrella_de_favoritos_es_de_linea_y_de_un_color()
    {
        var star = File.ReadAllText(Path.Combine(RepoRoot(), "Resources", "drawable", "ic_star.xml"));
        Assert.Contains("android:strokeWidth=\"1.8\"", star);
        Assert.Contains("android:fillColor=\"#00000000\"", star);
        Assert.Contains("android:strokeLineCap=\"round\"", star);
    }

    [Fact]
    public void Ni_los_disenos_ni_el_codigo_de_la_app_llevan_emoji()
    {
        var root = RepoRoot();
        var files = Directory.EnumerateFiles(Path.Combine(root, "Resources"), "*.xml", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}constitution{Path.DirectorySeparatorChar}")
                     && !f.Contains($"{Path.DirectorySeparatorChar}TdtOnline.Tests{Path.DirectorySeparatorChar}"));

        var offenders = new List<string>();
        foreach (var file in files)
        {
            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                // Las flechas de los comentarios («→») no se ven en pantalla: solo cuenta el codigo.
                var line = lines[i];
                var comment = line.IndexOf("//", StringComparison.Ordinal);
                var code = comment >= 0 && !line[..comment].Contains('"') ? line[..comment] : line;
                if (code.TrimStart().StartsWith("///", StringComparison.Ordinal) || code.TrimStart().StartsWith("<!--", StringComparison.Ordinal))
                    continue;
                if (InlineIcons.HasEmoji(code))
                    offenders.Add($"{Path.GetRelativePath(root, file)}:{i + 1}: {line.Trim()}");
            }
        }
        Assert.Empty(offenders);
    }
}
