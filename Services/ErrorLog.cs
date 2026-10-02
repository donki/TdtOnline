namespace TdtOnline.Services;

/// <summary>
/// Registro de errores no controlados (constitucion General 6.12): un fichero que no pasa de
/// <see cref="MaxBytes"/> (al pasarse se aparta a <c>.old</c>) y un limite de avisos para que un error
/// en bucle no llene la pantalla.
/// </summary>
public sealed class ErrorLog(string path)
{
    public const long MaxBytes = 256 * 1024;

    /// <summary>Entre dos avisos al usuario pasan al menos estos segundos.</summary>
    public static readonly TimeSpan NoticeInterval = TimeSpan.FromSeconds(3);

    private readonly object _lock = new();
    private DateTime _lastNotice = DateTime.MinValue;

    public string Path { get; } = path;

    public void Write(Exception ex, string source, DateTime now)
    {
        lock (_lock)
        {
            var info = new FileInfo(Path);
            if (info.Exists && info.Length > MaxBytes)
                File.Move(Path, Path + ".old", overwrite: true);

            File.AppendAllText(Path, $"[{now:yyyy-MM-dd HH:mm:ss}] ({source}) {ex}{Environment.NewLine}{Environment.NewLine}");
        }
    }

    /// <summary>True si toca avisar al usuario ahora (y apunta que se ha avisado).</summary>
    public bool ShouldNotify(DateTime utcNow)
    {
        lock (_lock)
        {
            if (utcNow - _lastNotice < NoticeInterval)
                return false;
            _lastNotice = utcNow;
            return true;
        }
    }
}
