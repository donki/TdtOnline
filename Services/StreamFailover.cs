namespace TdtOnline.Services;

/// <summary>Lo siguiente que tiene que hacer el reproductor.</summary>
public enum StreamStep
{
    /// <summary>Pedir la direccion al servicio de la cadena (DMAX) y llamar a <see cref="StreamFailover.Resolved"/>.</summary>
    Resolve,

    /// <summary>Reproducir <see cref="StreamFailover.CurrentUrl"/>.</summary>
    Play,

    /// <summary>No queda ninguna direccion: avisar de que no se puede ver.</summary>
    Fail,
}

/// <summary>
/// Que direccion reproducir de un canal y que hacer cuando falla: se prueba la siguiente; las
/// cadenas que dan la direccion al ir a verlas (DMAX) se vuelven a pedir una vez si caducan, sin
/// entrar en bucle. Sin reproductor, para probarlo (General 8.6).
/// </summary>
public sealed class StreamFailover(IReadOnlyList<string> urls, string? resolver)
{
    /// <summary>Una direccion pedida hace menos de esto no se vuelve a pedir al fallar.</summary>
    public static readonly TimeSpan ResolveCooldown = TimeSpan.FromSeconds(30);

    private IReadOnlyList<string> _urls = urls;
    private DateTime _resolvedAt = DateTime.MinValue;

    public int Current { get; private set; }

    public string? CurrentUrl => Current < _urls.Count ? _urls[Current] : null;

    public bool NeedsResolver => SonicLive.Handles(resolver);

    /// <summary>Al arrancar el reproductor.</summary>
    public StreamStep Start() => NeedsResolver ? StreamStep.Resolve : Next();

    /// <summary>Llegan las direcciones pedidas al servicio.</summary>
    public StreamStep Resolved(IReadOnlyList<string> resolved, DateTime utcNow)
    {
        _urls = resolved;
        Current = 0;
        _resolvedAt = utcNow;
        return Next();
    }

    /// <summary>
    /// Ha fallado la reproduccion. Devuelve el paso y si hay que avisar de que se prueba otra
    /// direccion («Probando…»).
    /// </summary>
    public (StreamStep Step, bool Trying) Failed(DateTime utcNow)
    {
        // La direccion pedida al servicio caduca a los minutos: se pide otra una vez, no en bucle.
        if (NeedsResolver && utcNow - _resolvedAt > ResolveCooldown)
            return (StreamStep.Resolve, true);

        Current++;
        return (Next(), Current < _urls.Count);
    }

    private StreamStep Next() => Current < _urls.Count ? StreamStep.Play : StreamStep.Fail;
}
