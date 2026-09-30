namespace TdtOnline.Services;

/// <summary>
/// Doble de prueba de las preferencias de la app (la de verdad va sobre SharedPreferences de
/// Android y no compila en net10.0). ChannelCatalog solo le pide las listas.
/// </summary>
public sealed class UserPreferences
{
    public IReadOnlyList<string> ListUrls { get; set; } = [ChannelCatalog.DefaultListUrl];
}
