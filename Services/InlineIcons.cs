using System.Text.RegularExpressions;

namespace TdtOnline.Services;

/// <summary>
/// Iconos dentro de un texto. Los textos no llevan emoji (General 6.2: cambian de dibujo segun el
/// telefono, se cortan con la letra grande y no siguen el tema); donde hace falta un dibujo llevan
/// la marca <c>{icon:nombre}</c>, que la pantalla cambia por el icono plano
/// de <see cref="Drawables"/>. Aqui solo se trocea el texto, sin Android, para probarlo.
/// </summary>
public static partial class InlineIcons
{
    /// <summary>
    /// Los iconos que se pueden poner en un texto y su dibujo en <c>Resources/drawable</c>
    /// (el sufijo <c>_w</c> es el blanco, para fondos rellenos).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> Drawables = new Dictionary<string, string>
    {
        ["star"] = "ic_star",
        ["flag_es"] = "ic_flag_es",
        ["flag_us"] = "ic_flag_us",
        ["warning"] = "ic_warning_w",
        ["refresh"] = "ic_refresh",
        ["restore"] = "ic_restore",
    };

    /// <summary>Un trozo del texto: o letras, o un icono.</summary>
    public readonly record struct Part(string Text, string? Icon)
    {
        public bool IsIcon => Icon is not null;
    }

    [GeneratedRegex(@"\{icon:([a-z0-9_]+)\}")]
    private static partial Regex Marker();

    /// <summary>
    /// Trocea el texto en letras e iconos, en orden. Una marca con un icono que no existe se queda
    /// como texto, para que se vea el fallo y no desaparezca nada.
    /// </summary>
    public static List<Part> Split(string text)
    {
        var parts = new List<Part>();
        var last = 0;
        foreach (Match m in Marker().Matches(text))
        {
            var name = m.Groups[1].Value;
            if (!Drawables.ContainsKey(name))
                continue;
            if (m.Index > last)
                parts.Add(new Part(text[last..m.Index], null));
            parts.Add(new Part(m.Value, name));
            last = m.Index + m.Length;
        }
        if (last < text.Length)
            parts.Add(new Part(text[last..], null));
        return parts;
    }

    /// <summary>Los nombres de icono que lleva el texto, en orden.</summary>
    public static List<string> Icons(string text) =>
        Split(text).Where(p => p.IsIcon).Select(p => p.Icon!).ToList();

    /// <summary>El texto sin las marcas (para lectores de pantalla y descripciones).</summary>
    public static string Plain(string text) =>
        string.Concat(Split(text).Where(p => !p.IsIcon).Select(p => p.Text)).Trim();

    /// <summary>Si el texto lleva algun emoji o pictograma de color (lo que General 6.2 prohibe).</summary>
    public static bool HasEmoji(string text) => text.EnumerateRunes().Any(r => IsEmoji(r.Value));

    private static bool IsEmoji(int cp) =>
        cp is >= 0x1F000 and <= 0x1FAFF   // emoji, banderas (indicadores regionales), simbolos
        || cp is >= 0x2600 and <= 0x27BF  // simbolos varios y dingbats (⚠ ✔ ✖ …)
        || cp is >= 0x2B00 and <= 0x2BFF  // ⭐ ⬆ …
        || cp is >= 0x2190 and <= 0x21FF  // flechas usadas como dibujo (↻ ↶)
        || cp is >= 0x2300 and <= 0x23FF  // ⌚ ⏰ ⏯ …
        || cp is >= 0x25A0 and <= 0x25FF  // ▶ ◀ ■ …
        || cp == 0xFE0F || cp == 0x200D   // selector de emoji y union de emoji
        || cp == 0x2122 || cp == 0x2139;  // ™ ℹ
}
