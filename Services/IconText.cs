using Android.Graphics.Drawables;
using Android.Text;
using Android.Text.Style;
using Android.Widget;

namespace TdtOnline.Services;

/// <summary>
/// Pone en un <see cref="TextView"/> un texto con marcas <c>{icon:nombre}</c> (ver
/// <see cref="InlineIcons"/>), cambiando cada marca por su icono plano dibujado a la altura de la
/// letra. Asi el icono crece con la letra grande del sistema y nunca depende de los emoji del
/// telefono (General 6.2).
/// </summary>
public static class IconText
{
    private static int DrawableFor(string icon) => icon switch
    {
        "star" => Resource.Drawable.ic_star,
        "flag_es" => Resource.Drawable.ic_flag_es,
        "flag_us" => Resource.Drawable.ic_flag_us,
        "warning" => Resource.Drawable.ic_warning_w,
        "refresh" => Resource.Drawable.ic_refresh,
        "restore" => Resource.Drawable.ic_restore,
        _ => 0,
    };

    /// <summary>Pone el texto con sus iconos; sin marcas, igual que <c>view.Text = text</c>.</summary>
    public static void Set(TextView view, string text)
    {
        var parts = InlineIcons.Split(text);
        if (!parts.Any(p => p.IsIcon))
        {
            view.Text = text;
            view.ContentDescription = null; // la vista puede venir reciclada de una con iconos
            return;
        }

        var builder = new SpannableStringBuilder();
        var size = (int)Math.Round(view.TextSize * 1.15);
        foreach (var part in parts)
        {
            var start = builder.Length();
            builder.Append(part.Text);
            if (!part.IsIcon)
                continue;

            var drawable = view.Context!.GetDrawable(DrawableFor(part.Icon!));
            if (drawable is null)
                continue;
            // Las banderas son 3:2; el resto, cuadrados.
            var width = drawable.IntrinsicHeight > 0
                ? (int)Math.Round(size * (double)drawable.IntrinsicWidth / drawable.IntrinsicHeight)
                : size;
            drawable.SetBounds(0, 0, width, size);
            builder.SetSpan(NewSpan(drawable), start, builder.Length(), SpanTypes.ExclusiveExclusive);
        }
        view.TextFormatted = builder;
        view.ContentDescription = InlineIcons.Plain(text);
    }

    /// <summary>Texto con un icono delante (la pastilla de Favoritos).</summary>
    public static void SetWithIcon(TextView view, string icon, string text) =>
        Set(view, $"{{icon:{icon}}} {text}");

    private static ImageSpan NewSpan(Drawable drawable) =>
        OperatingSystem.IsAndroidVersionAtLeast(29) // centrado vertical: Android 10 o mas
            ? new ImageSpan(drawable, SpanAlign.Center)
            : new ImageSpan(drawable, SpanAlign.Baseline);
}
