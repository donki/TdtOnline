namespace TdtOnline.Models;

/// <summary>Un canal de la lista: nombre, logotipo y las direcciones por las que se puede ver.</summary>
public sealed class Channel
{
    public string Name { get; init; } = string.Empty;

    public string? LogoUrl { get; init; }

    public string? Web { get; init; }

    /// <summary>Direcciones HLS/DASH en orden de preferencia; si una falla se prueba la siguiente.</summary>
    public IReadOnlyList<string> StreamUrls { get; init; } = [];

    /// <summary>Categoria (ambito) de la lista: Generalistas, Deportivos, Cataluña…</summary>
    public string Category { get; init; } = string.Empty;

    public string Country { get; init; } = string.Empty;
}

/// <summary>Una categoria con sus canales, tal como se enseña: una pastilla y una rejilla.</summary>
public sealed record Category(string Name, IReadOnlyList<Channel> Channels);
