using Android.Graphics;
using Android.Widget;

namespace TdtOnline.Services;

/// <summary>
/// Carga los logotipos de los canales en segundo plano y los guarda en memoria y en disco.
/// </summary>
/// <remarks>
/// Sin biblioteca de terceros: son unas decenas de imagenes pequeñas. En disco van en la cache de
/// la aplicacion con el nombre sacado de la direccion, para que la segunda apertura no baje nada.
/// Cada <see cref="ImageView"/> recuerda que direccion espera, y asi una tarjeta reciclada no se
/// queda con el logotipo de otro canal que llego tarde.
/// </remarks>
public sealed class LogoLoader
{
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly Dictionary<string, Bitmap> _memory = new();
    private readonly string _cacheDirectory;

    public LogoLoader(string cacheDirectory)
    {
        _cacheDirectory = System.IO.Path.Combine(cacheDirectory, "logos");
        Directory.CreateDirectory(_cacheDirectory);
    }

    public void Load(ImageView target, string? url)
    {
        target.SetImageDrawable(null);
        target.Tag = url;
        if (string.IsNullOrEmpty(url))
            return;

        lock (_memory)
        {
            if (_memory.TryGetValue(url, out var cached))
            {
                target.SetImageBitmap(cached);
                return;
            }
        }

        _ = LoadAsync(target, url);
    }

    private async Task LoadAsync(ImageView target, string url)
    {
        try
        {
            var file = System.IO.Path.Combine(_cacheDirectory, Hash(url));
            byte[] bytes;
            if (File.Exists(file))
            {
                bytes = await File.ReadAllBytesAsync(file).ConfigureAwait(false);
            }
            else
            {
                bytes = await _http.GetByteArrayAsync(url).ConfigureAwait(false);
                await File.WriteAllBytesAsync(file, bytes).ConfigureAwait(false);
            }

            var bitmap = await BitmapFactory.DecodeByteArrayAsync(bytes, 0, bytes.Length).ConfigureAwait(false);
            if (bitmap is null)
                return;

            lock (_memory)
                _memory[url] = bitmap;

            target.Post(() =>
            {
                if (Equals(target.Tag?.ToString(), url))
                    target.SetImageBitmap(bitmap);
            });
        }
        catch (Exception)
        {
            // Sin logotipo se ve el nombre; un logotipo que no baja no es un error que enseñar.
        }
    }

    private static string Hash(string url)
    {
        var bytes = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(url));
        return Convert.ToHexString(bytes)[..24];
    }
}
