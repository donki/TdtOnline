using TdtOnline.Localization;
using TdtOnline.Models;
using TdtOnline.Services;
using TdtOnline.Tests.Fakes;

namespace TdtOnline.Tests;

/// <summary>
/// Lo que antes vivia en las actividades (pantalla principal, parrilla, ajustes, reproductor) y las
/// preferencias, probado sin Android. Usa Loc en ingles, asi que va en la coleccion de Loc.
/// </summary>
[Collection(nameof(LocCollection))]
public sealed class ScreenLogicTests : IDisposable
{
    private readonly string _override = Loc.Override;

    public ScreenLogicTests() => Loc.Override = "en";

    public void Dispose() => Loc.Override = _override;

    private static Channel Ch(string name, string? epg = null, params string[] urls) =>
        new() { Name = name, EpgId = epg, StreamUrls = urls };

    private static readonly IReadOnlyList<Category> Raw =
    [
        new Category("Generalistas", [Ch("La 1", "la1"), Ch("Antena 3", "a3"), Ch("Cuatro")]),
        new Category("Noticias", [Ch("24h", "24h"), Ch("La 1", "la1")]),
    ];

    // ---------- Pantalla principal ----------

    [Fact]
    public void Pastillas_favoritos_primero_aunque_vacio_luego_todos_y_las_categorias()
    {
        var empty = HomeLogic.DisplayCategories(Raw, new HashSet<string>());
        Assert.Equal(new[] { Loc.Get("Favorites"), Loc.Get("AllChannels"), "Generalistas", "Noticias" }, empty.Select(c => c.Name));
        Assert.Empty(empty[0].Channels);
        Assert.Equal(4, empty[1].Channels.Count);

        var withFav = HomeLogic.DisplayCategories(Raw, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "cuatro" });
        Assert.Equal("Cuatro", Assert.Single(withFav[0].Channels).Name);
    }

    [Fact]
    public void Al_rehacer_se_queda_en_la_categoria_que_se_veia_si_sigue()
    {
        var display = HomeLogic.DisplayCategories(Raw, new HashSet<string>());
        Assert.Equal(3, HomeLogic.TargetIndex(display, true, "Noticias"));
        Assert.Equal(0, HomeLogic.TargetIndex(display, true, "Ya no existe"));
        Assert.Equal(0, HomeLogic.TargetIndex(display, false, "Noticias"));
        Assert.Equal(0, HomeLogic.TargetIndex(display, true, null));
    }

    [Fact]
    public void Buscar_canal_por_nombre_sin_mayusculas()
    {
        Assert.Equal("Antena 3", HomeLogic.FindChannel(Raw, "ANTENA 3")!.Name);
        Assert.Null(HomeLogic.FindChannel(Raw, "Telecinco"));
    }

    [Theory]
    [InlineData(1080, 2.75f, 2)]     // movil estrecho: minimo dos
    [InlineData(1440, 2f, 4)]
    [InlineData(1920, 1f, 12)]
    [InlineData(1920, 1.5f, 8)]      // tele
    public void Columnas_segun_el_ancho(int width, float density, int columns) =>
        Assert.Equal(columns, HomeLogic.Columns(width, density));

    [Theory]
    [InlineData(3, 4, 10, DpadKey.Right, CardKeyAction.Stay)]          // ultima columna
    [InlineData(9, 4, 10, DpadKey.Right, CardKeyAction.Stay)]          // ultima tarjeta
    [InlineData(1, 4, 10, DpadKey.Right, CardKeyAction.Pass)]
    [InlineData(4, 4, 10, DpadKey.Left, CardKeyAction.Stay)]           // primera columna
    [InlineData(5, 4, 10, DpadKey.Left, CardKeyAction.Pass)]
    [InlineData(2, 4, 10, DpadKey.Up, CardKeyAction.FocusCategory)]    // primera fila
    [InlineData(6, 4, 10, DpadKey.Up, CardKeyAction.Pass)]
    [InlineData(2, 4, 10, DpadKey.Down, CardKeyAction.Pass)]
    [InlineData(2, 4, 10, DpadKey.Other, CardKeyAction.Pass)]
    public void Cruceta_en_la_rejilla(int position, int span, int count, DpadKey key, CardKeyAction expected) =>
        Assert.Equal(expected, HomeLogic.CardKey(position, span, count, key));

    [Theory]
    [InlineData(" la ", false, HomeBack.ClearSearch)]
    [InlineData("la", true, HomeBack.ClearSearch)]
    [InlineData("   ", true, HomeBack.LeaveSearch)]
    [InlineData("", false, HomeBack.HideApp)]
    public void Atras_primero_el_buscador(string text, bool focus, HomeBack expected) =>
        Assert.Equal(expected, HomeLogic.Back(text, focus));

    [Fact]
    public void Ultimo_canal_recuento_y_avisos()
    {
        Assert.Null(HomeLogic.LastChannelText(" "));
        Assert.Null(HomeLogic.LastChannelText(null));
        Assert.Equal("Last watched: La 1", HomeLogic.LastChannelText("La 1"));
        Assert.Equal("5 channels", HomeLogic.ChannelsCountText(Raw));
        Assert.Null(HomeLogic.FailedListsText([]));
        Assert.Equal("Could not download: example.org/a.m3u, b", HomeLogic.FailedListsText(["https://example.org/a.m3u", "b"]));
        Assert.Equal(Loc.Format("FavoriteAdded", "La 1"), HomeLogic.FavoriteToggledText("La 1", true));
        Assert.Equal(Loc.Format("FavoriteRemoved", "La 1"), HomeLogic.FavoriteToggledText("La 1", false));
    }

    [Fact]
    public void Favoritos_vacio_explica_como_se_llena_y_las_demas_no()
    {
        var display = HomeLogic.DisplayCategories(Raw, new HashSet<string>());
        var (fav, favHint) = HomeLogic.ShowCategory(display, 0);
        Assert.Empty(fav);
        Assert.Equal(Loc.Get("FavoriteTip"), favHint);

        var (news, newsHint) = HomeLogic.ShowCategory(display, 3);
        Assert.Equal(2, news.Count);
        Assert.Null(newsHint);

        var withFav = HomeLogic.DisplayCategories(Raw, new HashSet<string> { "La 1" });
        Assert.Null(HomeLogic.ShowCategory(withFav, 0).EmptyHint);
    }

    [Fact]
    public void Busqueda_con_y_sin_resultados()
    {
        var (found, hint) = HomeLogic.Search(Raw, "la 1");
        Assert.Equal("La 1", Assert.Single(found).Name);
        Assert.Null(hint);

        var (none, noneHint) = HomeLogic.Search(Raw, "zzz");
        Assert.Empty(none);
        Assert.Equal("No channel matches «zzz».", noneHint);
    }

    // ---------- Parrilla ----------

    [Fact]
    public void Parrilla_solo_canales_con_guia_una_vez_y_favoritos_delante()
    {
        var rows = GuideLogic.Rows(Raw, new HashSet<string> { "24h" }, id => id is "la1" or "24h" or "a3");
        Assert.Equal(new[] { "24h", "La 1", "Antena 3" }, rows.Select(c => c.Name));
    }

    [Fact]
    public void Parrilla_vacia_dice_si_carga_o_no_hay_datos()
    {
        Assert.Equal(Loc.Get("GuideLoading"), GuideLogic.EmptyText(false));
        Assert.Equal(Loc.Get("GuideEmpty"), GuideLogic.EmptyText(true));
    }

    [Fact]
    public void Tarjeta_de_programa_en_emision_y_fuera()
    {
        var p = new EpgProgram("Telediario", "", 1_000, 2_000);

        var before = GuideLogic.ProgramCell(p, 999);
        Assert.Equal((p.TimeRange, false, 0), before);

        var (time, current, progress) = GuideLogic.ProgramCell(p, 1_250);
        Assert.Equal($"Now · {p.TimeRange}", time);
        Assert.True(current);
        Assert.Equal(250, progress);

        Assert.False(GuideLogic.ProgramCell(p, 2_000).Current);
        Assert.Equal(0, GuideLogic.ProgramCell(new EpgProgram("x", "", 5, 5), 5).Progress);
    }

    // ---------- Ajustes ----------

    [Fact]
    public void Anadir_lista_valida_repetida_o_mala()
    {
        IReadOnlyList<string> urls = [ChannelCatalog.DefaultListUrl];

        var (ok, added) = SettingsLogic.Add("  https://example.org/mi.m3u ", urls);
        Assert.Equal("ListAdded", ok);
        Assert.Equal(new[] { ChannelCatalog.DefaultListUrl, "https://example.org/mi.m3u" }, added);

        Assert.Equal(("ListExists", (List<string>?)null), SettingsLogic.Add(ChannelCatalog.DefaultListUrl.ToUpperInvariant(), urls));
        Assert.Equal(("ListInvalid", (List<string>?)null), SettingsLogic.Add("ftp://x", urls));
        Assert.Equal(("ListInvalid", (List<string>?)null), SettingsLogic.Add(null, urls));
    }

    [Fact]
    public void Quitar_lista_y_etiqueta_de_la_de_siempre()
    {
        Assert.Equal(new[] { "b" }, SettingsLogic.Remove(["a", "b"], "a"));
        Assert.Equal(Loc.Get("DefaultListLabel"), SettingsLogic.Label(ChannelCatalog.DefaultListUrl));
        Assert.Null(SettingsLogic.Label("https://example.org/otra.m3u"));
    }

    // ---------- Preferencias ----------

    [Fact]
    public void Preferencias_idioma_listas_y_version()
    {
        var store = new MemoryPreferenceStore();
        var prefs = new UserPreferences(store);

        Assert.Equal(string.Empty, prefs.Language);
        prefs.Language = "es";
        Assert.Equal("es", new UserPreferences(store).Language);

        Assert.Equal(new[] { ChannelCatalog.DefaultListUrl }, prefs.ListUrls);
        Assert.Equal(0, prefs.ListsVersion);
        prefs.ListUrls = ["https://a", "https://b"];
        Assert.Equal(new[] { "https://a", "https://b" }, prefs.ListUrls);
        Assert.Equal(1, prefs.ListsVersion);
        prefs.BumpListsVersion();
        Assert.Equal(2, prefs.ListsVersion);
    }

    [Fact]
    public void Preferencias_favoritos_con_aviso_y_sin_mayusculas()
    {
        var prefs = new UserPreferences(new MemoryPreferenceStore());
        var changes = new List<(string, bool)>();
        prefs.FavoriteChanged += (name, fav) => changes.Add((name, fav));

        Assert.Empty(prefs.GetFavorites());
        Assert.True(prefs.ToggleFavorite("La 1"));
        Assert.True(prefs.IsFavorite("la 1"));
        Assert.False(prefs.IsFavorite(" "));
        Assert.False(prefs.ToggleFavorite("LA 1"));
        Assert.False(prefs.IsFavorite("La 1"));
        Assert.False(prefs.ToggleFavorite(""));
        Assert.Equal(new[] { ("La 1", true), ("LA 1", false) }, changes);
    }

    [Fact]
    public void Preferencias_ultimo_canal_se_borra_con_vacio()
    {
        var store = new MemoryPreferenceStore();
        var prefs = new UserPreferences(store) { LastChannel = "Cuatro" };
        Assert.Equal("Cuatro", prefs.LastChannel);
        prefs.LastChannel = "  ";
        Assert.Null(prefs.LastChannel);
        Assert.Empty(store.Values);
    }

    // ---------- Reproductor ----------

    [Fact]
    public void Reproductor_prueba_cada_direccion_y_al_final_falla()
    {
        var streams = new StreamFailover(["u1", "u2"], resolver: null);
        Assert.False(streams.NeedsResolver);
        Assert.Equal(StreamStep.Play, streams.Start());
        Assert.Equal("u1", streams.CurrentUrl);

        Assert.Equal((StreamStep.Play, true), streams.Failed(DateTime.UtcNow));
        Assert.Equal("u2", streams.CurrentUrl);

        Assert.Equal((StreamStep.Fail, false), streams.Failed(DateTime.UtcNow));
        Assert.Null(streams.CurrentUrl);
    }

    [Fact]
    public void Reproductor_sin_direcciones_falla_enseguida() =>
        Assert.Equal(StreamStep.Fail, new StreamFailover([], null).Start());

    [Fact]
    public void Reproductor_DMAX_pide_la_direccion_y_la_vuelve_a_pedir_una_vez_si_caduca()
    {
        var resolver = "sonic:dmax";
        Assert.True(SonicLive.Handles(resolver));
        var streams = new StreamFailover([], resolver);
        var t0 = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(StreamStep.Resolve, streams.Start());
        Assert.Equal(StreamStep.Play, streams.Resolved(["d1", "d2"], t0));
        Assert.Equal("d1", streams.CurrentUrl);

        // Falla enseguida: no se vuelve a pedir, se prueba la siguiente.
        Assert.Equal((StreamStep.Play, true), streams.Failed(t0.AddSeconds(5)));
        Assert.Equal("d2", streams.CurrentUrl);

        // Falla pasado el plazo: se pide otra direccion.
        Assert.Equal((StreamStep.Resolve, true), streams.Failed(t0.AddMinutes(5)));
        Assert.Equal(StreamStep.Fail, streams.Resolved([], t0.AddMinutes(5)));
    }

    // ---------- Registro de errores ----------

    [Fact]
    public void Registro_de_errores_escribe_y_aparta_el_grande()
    {
        using var dir = new TempDir();
        var log = new ErrorLog(Path.Combine(dir.Path, "errors.log"));

        log.Write(new InvalidOperationException("uno"), "task", new DateTime(2026, 10, 1, 10, 0, 0));
        var text = File.ReadAllText(log.Path);
        Assert.StartsWith("[2026-10-01 10:00:00] (task) System.InvalidOperationException: uno", text);

        File.WriteAllText(log.Path, new string('x', (int)ErrorLog.MaxBytes + 1));
        log.Write(new Exception("dos"), "android", DateTime.Now);
        Assert.True(File.Exists(log.Path + ".old"));
        Assert.Contains("dos", File.ReadAllText(log.Path));
        Assert.True(new FileInfo(log.Path).Length < 1000);
    }

    [Fact]
    public void Avisos_como_mucho_uno_cada_tres_segundos()
    {
        var log = new ErrorLog("no-se-usa.log");
        var t = new DateTime(2026, 10, 1, 10, 0, 0, DateTimeKind.Utc);
        Assert.True(log.ShouldNotify(t));
        Assert.False(log.ShouldNotify(t.AddSeconds(1)));
        Assert.True(log.ShouldNotify(t.AddSeconds(4)));
    }
}
