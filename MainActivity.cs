using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.RecyclerView.Widget;
using TdtOnline.Localization;
using TdtOnline.Models;
using TdtOnline.Services;

namespace TdtOnline;

/// <summary>
/// La pantalla principal: una fila de categorias y la rejilla de canales de la elegida.
/// </summary>
/// <remarks>
/// Es la misma pantalla en el movil y en la tele. En la tele se navega con la cruceta: las
/// pastillas y las tarjetas son <c>focusable</c> y el foco se pinta con el borde de marca
/// (<c>card_background.xml</c>). En el movil se toca. No hay dos interfaces que mantener.
///
/// El <c>LEANBACK_LAUNCHER</c> es lo que hace que la aplicacion salga en el lanzador de Android
/// TV; el <c>LAUNCHER</c> normal, en el del movil.
/// </remarks>
[Activity(
    Label = "TDT Online",
    MainLauncher = true,
    Exported = true,
    LaunchMode = LaunchMode.SingleTop,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden)]
[IntentFilter([Intent.ActionMain], Categories = [Intent.CategoryLauncher, Intent.CategoryLeanbackLauncher])]
public sealed class MainActivity : AppCompatActivity
{
    private ChannelCatalog _catalog = null!;
    private LogoLoader _logos = null!;
    private UserPreferences _prefs = null!;
    private EpgService _epg = null!;

    private RecyclerView _categoriesView = null!;
    private RecyclerView _channelsView = null!;
    private TextView _status = null!;
    private TextView _emptyHint = null!;
    private EditText _search = null!;
    private TextView _lastChannelView = null!;
    private ImageButton _btnAbout = null!;

    private ChannelAdapter _channels = null!;
    private CategoryAdapter _categories = null!;

    private IReadOnlyList<Category> _rawCategories = [];
    private int _loadedListsVersion;
    private List<Category> _displayCategories = [];
    private int _selectedCategoryIndex;

    /// <summary>El canal que se abrio en el reproductor, para devolverle el foco al volver.</summary>
    private Channel? _lastPlayed;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var cache = CacheDir!.AbsolutePath;
        _prefs = new UserPreferences(this);
        _catalog = new ChannelCatalog(cache, _prefs);
        _logos = new LogoLoader(cache);
        _epg = EpgService.Shared(cache);
        Loc.Override = _prefs.Language;

        FindViewById<TextView>(Resource.Id.title)!.Text = Loc.Get("AppTitle");
        _status = FindViewById<TextView>(Resource.Id.status)!;
        _emptyHint = FindViewById<TextView>(Resource.Id.empty_hint)!;
        _search = FindViewById<EditText>(Resource.Id.search)!;
        _search.Hint = Loc.Get("SearchHint");
        _search.TextChanged += (_, _) => ApplySearch();
        _search.EditorAction += (_, args) =>
        {
            // «Buscar» en el teclado: cerrar el teclado y pasar el foco a los resultados.
            if (args.ActionId == Android.Views.InputMethods.ImeAction.Search)
            {
                HideKeyboard();
                _channelsView.RequestFocus();
                args.Handled = true;
            }
        };
        _lastChannelView = FindViewById<TextView>(Resource.Id.last_channel)!;
        _btnAbout = FindViewById<ImageButton>(Resource.Id.btn_about)!;

        _btnAbout.Click += (_, _) => StartActivity(new Intent(this, typeof(AboutActivity)));
        FindViewById<ImageButton>(Resource.Id.btn_guide)!.Click += (_, _) => StartActivity(new Intent(this, typeof(GuideActivity)));
        FindViewById<ImageButton>(Resource.Id.btn_settings)!.Click += (_, _) => StartActivity(new Intent(this, typeof(SettingsActivity)));
        _loadedListsVersion = _prefs.ListsVersion;

        _lastChannelView.Click += (_, _) =>
        {
            var last = _prefs.LastChannel;
            if (string.IsNullOrWhiteSpace(last))
                return;

            var channel = FindChannelByName(last);
            if (channel is not null)
                Play(channel);
        };

        _categoriesView = FindViewById<RecyclerView>(Resource.Id.categories)!;
        _categoriesView.SetLayoutManager(new LinearLayoutManager(this, LinearLayoutManager.Horizontal, false));
        _categories = new CategoryAdapter(ShowCategory, FocusFirstVisibleCard);
        _categoriesView.SetAdapter(_categories);

        // Sin la animacion de «cambiado»: con ella, al marcar la pastilla elegida la RecyclerView
        // pinta una copia nueva de la pastilla y la que tenia el foco desaparece; en la tele el foco
        // saltaba solo a la rejilla y no se podia recorrer la fila de categorias con la cruceta.
        if (_categoriesView.GetItemAnimator() is SimpleItemAnimator animator)
            animator.SupportsChangeAnimations = false;

        _channelsView = FindViewById<RecyclerView>(Resource.Id.channels)!;
        _channelsView.SetLayoutManager(new GridLayoutManager(this, ColumnsForWidth()));
        _channels = new ChannelAdapter(_logos, _prefs, _epg, Play, ToggleFavorite, CardKey);
        _channelsView.SetAdapter(_channels);

        _epg.EpgLoaded += () => RunOnUiThread(() => _channels.NotifyDataSetChanged());
        _catalog.Updated += categories => RunOnUiThread(() =>
        {
            // Ha terminado la comprobacion de Free-TV: entran los canales que faltaban.
            _rawCategories = categories;
            RebuildDisplayCategories(maintainCategory: true);
            _status.Text = Loc.Format("ChannelsCount", _rawCategories.Sum(c => c.Channels.Count));
        });

        OnBackPressedDispatcher.AddCallback(this, new BackCallback(this));

        _ = LoadAsync(forceRefresh: false);
        _ = _epg.LoadAsync();
    }

    /// <summary>
    /// Atras en la pantalla de inicio (constitucion mobile 7), con el gesto, el boton o el mando:
    /// primero cierra el buscador (borra lo escrito o le quita el foco) y, si no hay nada abierto,
    /// oculta la aplicacion sin cerrarla, para que al volver siga donde estaba.
    /// </summary>
    private void OnBack()
    {
        if (SearchText.Length > 0)
        {
            _search.Text = string.Empty;
            HideKeyboard();
            _search.ClearFocus();
            _channelsView.RequestFocus();
            return;
        }

        if (_search.HasFocus)
        {
            HideKeyboard();
            _search.ClearFocus();
            _channelsView.RequestFocus();
            return;
        }

        MoveTaskToBack(true);
    }

    private sealed class BackCallback(MainActivity owner) : AndroidX.Activity.OnBackPressedCallback(true)
    {
        public override void HandleOnBackPressed() => owner.OnBack();
    }

    protected override void OnResume()
    {
        base.OnResume();
        ApplyLanguage();
        UpdateLastChannelUi();

        // Si en Ajustes cambiaron las listas o se pidio refrescar, se vuelve a cargar todo.
        if (_prefs.ListsVersion != _loadedListsVersion)
        {
            _loadedListsVersion = _prefs.ListsVersion;
            _ = LoadAsync(forceRefresh: true);
            return;
        }

        // Si cambiaron los favoritos desde el reproductor, reconstruir categorias
        if (_rawCategories.Count > 0)
        {
            RebuildDisplayCategories(maintainCategory: true);
        }

        RestoreFocusToLastPlayed();
    }

    /// <summary>
    /// Devuelve el foco a la tarjeta del canal que se acaba de ver.
    /// </summary>
    /// <remarks>
    /// Al volver del reproductor la rejilla se reconstruye (por si cambiaron los favoritos) y eso
    /// deja el foco en la primera tarjeta: en la tele habia que volver a buscar con la cruceta el
    /// canal en el que se estaba (2026-09-13). Se hace con Post, despues de que la lista haya
    /// colocado sus tarjetas, y con un segundo intento por si aun no estaba la de ese canal.
    /// </remarks>
    private void RestoreFocusToLastPlayed()
    {
        if (_lastPlayed is null)
            return;

        var position = _channels.IndexOf(_lastPlayed.Name);
        if (position < 0)
            return;

        _channelsView.ScrollToPosition(position);
        _channelsView.Post(() =>
        {
            if (!FocusCard(position))
                _channelsView.PostDelayed(() => FocusCard(position), 150);
        });
    }

    /// <summary>
    /// Abajo desde una pastilla de categoria: a la primera tarjeta que se ve. Sin esto, la cruceta
    /// solo bajaba si justo debajo de la pastilla habia una tarjeta; desde una pastilla de la
    /// derecha con pocos canales (Cantabria, cuatro) no pasaba nada (2026-09-29).
    /// </summary>
    private bool FocusFirstVisibleCard()
    {
        if (_channels.ItemCount == 0)
            return false;

        var layout = (GridLayoutManager)_channelsView.GetLayoutManager()!;
        var first = Math.Max(0, layout.FindFirstCompletelyVisibleItemPosition());
        if (FocusCard(first))
            return true;

        _channelsView.ScrollToPosition(0);
        _channelsView.Post(() => FocusCard(0));
        return true;
    }

    /// <summary>
    /// La cruceta en una tarjeta. En los bordes de la rejilla Android buscaba el foco fuera de ella y
    /// lo mandaba a una pastilla cualquiera, que al recibirlo abria otra categoria: derecha en la
    /// ultima columna saltaba de Andalucia a Canarias (2026-09-29). Ahora a los lados el foco se
    /// queda en la rejilla y arriba, desde la primera fila, vuelve a la pastilla de la categoria
    /// que se esta viendo.
    /// </summary>
    private bool CardKey(int position, Keycode key)
    {
        var span = ((GridLayoutManager)_channelsView.GetLayoutManager()!).SpanCount;
        var column = position % span;
        switch (key)
        {
            case Keycode.DpadRight:
                return column == span - 1 || position == _channels.ItemCount - 1;
            case Keycode.DpadLeft:
                return column == 0;
            case Keycode.DpadUp when position < span:
                var chip = _categoriesView.GetLayoutManager()?.FindViewByPosition(_selectedCategoryIndex);
                if (chip is not null)
                    return chip.RequestFocus();
                _categoriesView.ScrollToPosition(_selectedCategoryIndex);
                return false;
            default:
                return false;
        }
    }

    private bool FocusCard(int position)
    {
        var holder = _channelsView.FindViewHolderForAdapterPosition(position);
        return holder is not null && holder.ItemView.RequestFocus();
    }

    /// <summary>
    /// Vuelve a poner los textos fijos de la pantalla en el idioma elegido. Al cambiar de idioma en
    /// «Acerca de» y volver, las pastillas ya salian traducidas pero el buscador y el recuento se
    /// quedaban en el idioma anterior (2026-09-29).
    /// </summary>
    private void ApplyLanguage()
    {
        Loc.Override = _prefs.Language;
        FindViewById<TextView>(Resource.Id.title)!.Text = Loc.Get("AppTitle");
        _search.Hint = Loc.Get("SearchHint");
        if (_rawCategories.Count > 0)
            _status.Text = Loc.Format("ChannelsCount", _rawCategories.Sum(c => c.Channels.Count));
    }

    private void UpdateLastChannelUi()
    {
        var last = _prefs.LastChannel;
        if (!string.IsNullOrWhiteSpace(last))
        {
            _lastChannelView.Text = Loc.Format("LastChannel", last);
            _lastChannelView.Visibility = ViewStates.Visible;
        }
        else
        {
            // Invisible y no Gone: asi el recuento de canales se queda a la derecha de su fila.
            _lastChannelView.Visibility = ViewStates.Invisible;
        }
    }

    private Channel? FindChannelByName(string name)
    {
        foreach (var cat in _rawCategories)
        {
            foreach (var ch in cat.Channels)
            {
                if (string.Equals(ch.Name, name, StringComparison.OrdinalIgnoreCase))
                    return ch;
            }
        }

        return null;
    }

    /// <summary>Tantas columnas como quepan a ~160 dp: cuatro en un movil, siete u ocho en una tele.</summary>
    private int ColumnsForWidth()
    {
        var metrics = Resources!.DisplayMetrics!;
        var widthDp = metrics.WidthPixels / metrics.Density;
        return Math.Max(2, (int)(widthDp / 160));
    }

    private async Task LoadAsync(bool forceRefresh)
    {
        _status.Text = Loc.Get("Loading");
        try
        {
            _rawCategories = await _catalog.LoadAsync(forceRefresh);
            RebuildDisplayCategories(maintainCategory: false);
            _status.Text = Loc.Format("ChannelsCount", _rawCategories.Sum(c => c.Channels.Count));

            if (_catalog.FailedLists.Count > 0)
                Toast.MakeText(this, Loc.Format("ListsFailed", string.Join(", ", _catalog.FailedLists.Select(ShortUrl))), ToastLength.Long)?.Show();
            if (_rawCategories.Count == 0)
                _status.Text = Loc.Get("LoadFailed");
        }
        catch (Exception)
        {
            _status.Text = Loc.Get("LoadFailed");
        }
    }

    private static string ShortUrl(string url) => ChannelLists.ShortUrl(url);

    private void RebuildDisplayCategories(bool maintainCategory)
    {
        var currentSelectedName = _selectedCategoryIndex >= 0 && _selectedCategoryIndex < _displayCategories.Count
            ? _displayCategories[_selectedCategoryIndex].Name
            : null;

        var list = new List<Category>();
        // 1. Favoritos, siempre el primero aunque este vacio: es el grupo del usuario, y si no se
        //    ve no hay forma de saber que existe ni de meter nada en el.
        var favChannels = ChannelLists.Favorites(_rawCategories, _prefs.GetFavorites());

        list.Add(new Category(Loc.Get("Favorites"), favChannels));

        // 2. Todos: cada canal una vez, en el orden de la lista, para verlos sin saber la categoria.
        list.Add(new Category(Loc.Get("AllChannels"), AllChannels()));

        // 3. Resto de categorias
        list.AddRange(_rawCategories);

        _displayCategories = list;
        _categories.Submit(_displayCategories);

        if (_displayCategories.Count == 0)
            return;

        int targetIndex = 0;
        if (maintainCategory && currentSelectedName is not null)
        {
            for (int i = 0; i < _displayCategories.Count; i++)
            {
                if (_displayCategories[i].Name == currentSelectedName)
                {
                    targetIndex = i;
                    break;
                }
            }
        }

        ShowCategory(targetIndex);
    }

    /// <summary>Todos los canales sin repetir (un canal puede estar en varias categorias).</summary>
    private List<Channel> AllChannels() => ChannelLists.AllChannels(_rawCategories);

    private void ShowCategory(int index)
    {
        if (index < 0 || index >= _displayCategories.Count)
            return;

        // Si alguna de las dos listas esta en un pase de layout, se vuelve a intentar en el
        // siguiente ciclo: notificar cambios ahora es justo lo que tumba la aplicacion.
        if (_categoriesView.IsComputingLayout || _channelsView.IsComputingLayout)
        {
            _categoriesView.Post(() => ShowCategory(index));
            return;
        }

        _selectedCategoryIndex = index;
        _categories.Select(index);

        if (SearchText.Length > 0)
        {
            // Con texto en el buscador manda la busqueda; la categoria queda marcada para cuando se borre.
            ApplySearch();
            return;
        }

        _channels.Submit(_displayCategories[index].Channels);
        _channelsView.ScrollToPosition(0);

        // Favoritos vacio: se explica como se llena, en vez de dejar la rejilla en blanco.
        var emptyFavorites = index == 0 && _displayCategories[index].Channels.Count == 0;
        _emptyHint.Visibility = emptyFavorites ? ViewStates.Visible : ViewStates.Gone;
        if (emptyFavorites)
            _emptyHint.Text = Loc.Get("FavoriteTip");
    }

    private string SearchText => (_search.Text ?? string.Empty).Trim();

    /// <summary>Filtra por nombre entre todos los canales; sin texto, vuelve a la categoria elegida.</summary>
    private void ApplySearch()
    {
        var text = SearchText;
        if (text.Length == 0)
        {
            ShowCategory(_selectedCategoryIndex);
            return;
        }

        var matches = ChannelLists.Search(_rawCategories, text);
        _channels.Submit(matches);
        _channelsView.ScrollToPosition(0);

        _emptyHint.Visibility = matches.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        if (matches.Count == 0)
            _emptyHint.Text = Loc.Format("NoResults", text);
    }

    private void HideKeyboard()
    {
        var imm = (Android.Views.InputMethods.InputMethodManager?)GetSystemService(InputMethodService);
        imm?.HideSoftInputFromWindow(_search.WindowToken, 0);
    }

    private void ToggleFavorite(Channel channel)
    {
        var isFav = _prefs.ToggleFavorite(channel.Name);
        var msg = isFav ? Loc.Format("FavoriteAdded", channel.Name) : Loc.Format("FavoriteRemoved", channel.Name);
        Toast.MakeText(this, msg, ToastLength.Short)?.Show();

        RebuildDisplayCategories(maintainCategory: true);
    }

    private void Play(Channel channel)
    {
        _lastPlayed = channel;
        _prefs.LastChannel = channel.Name;
        UpdateLastChannelUi();
        StartActivity(PlayerActivity.IntentFor(this, channel));
    }

    public override bool OnKeyDown([Android.Runtime.GeneratedEnum] Keycode keyCode, KeyEvent? e)
    {
        if (keyCode == Keycode.Info)
        {
            StartActivity(new Intent(this, typeof(AboutActivity)));
            return true;
        }

        // El boton rojo del mando abre la parrilla (el de «guia» se lo queda el sistema en Android TV).
        if (keyCode == Keycode.ProgRed)
        {
            StartActivity(new Intent(this, typeof(GuideActivity)));
            return true;
        }

        // En la tele, el boton de «menu» o el de refrescar del mando vuelve a bajar la lista.
        if (keyCode == Keycode.Menu || ((int)Build.VERSION.SdkInt >= 28 && keyCode == Keycode.Refresh))
        {
            _ = LoadAsync(forceRefresh: true);
            _ = _epg.LoadAsync(forceRefresh: true);
            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

    /// <summary>Escucha las teclas al pulsarlas (no al soltarlas); true = tecla atendida.</summary>
    private sealed class KeyListener(Func<Keycode, KeyEvent, bool> onKeyDown) : Java.Lang.Object, View.IOnKeyListener
    {
        public bool OnKey(View? v, [Android.Runtime.GeneratedEnum] Keycode keyCode, KeyEvent? e) =>
            e is not null && e.Action == KeyEventActions.Down && onKeyDown(keyCode, e);
    }

    // =====================================================================
    //  Adaptadores
    // =====================================================================

    private sealed class CategoryAdapter(Action<int> onSelect, Func<bool> onDown) : RecyclerView.Adapter
    {
        private IReadOnlyList<Category> _items = [];
        private int _selected;

        public void Submit(IReadOnlyList<Category> items)
        {
            _items = items;
            _selected = 0;
            NotifyDataSetChanged();
        }

        public void Select(int index)
        {
            var previous = _selected;
            _selected = index;
            NotifyItemChanged(previous);
            NotifyItemChanged(index);
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.item_category, parent, false)!;
            var holder = new Holder(view);
            view.SetOnKeyListener(new KeyListener((key, _) => key == Keycode.DpadDown && onDown()));
            view.Click += (_, _) => onSelect(holder.BindingAdapterPosition);

            // En la tele basta con posarse encima: pasar por las categorias ya las abre.
            //
            // APLAZADO con Post: el cambio de foco llega en mitad del recorrido de foco de la
            // propia RecyclerView (que es un pase de layout/scroll), y abrir la categoria ahi
            // dentro llama a NotifyItemChanged sobre esa misma lista. Android lo prohibe y tira
            // la aplicacion: «Cannot call this method while RecyclerView is computing a layout
            // or scrolling». Era el «peta al navegar con el mando» de la tele Xiaomi (2026-09-13).
            view.FocusChange += (_, args) =>
            {
                if (!args.HasFocus)
                    return;

                view.Post(() =>
                {
                    var position = holder.BindingAdapterPosition;
                    if (view.HasFocus && position != RecyclerView.NoPosition)
                        onSelect(position);
                });
            };
            return holder;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var chip = (TextView)holder.ItemView;
            chip.Text = _items[position].Name;
            chip.Selected = position == _selected;
        }

        private sealed class Holder(View view) : RecyclerView.ViewHolder(view);
    }

    private sealed class ChannelAdapter(
        LogoLoader logos,
        UserPreferences prefs,
        EpgService epg,
        Action<Channel> onPlay,
        Action<Channel> onToggleFavorite,
        Func<int, Keycode, bool> onKey) : RecyclerView.Adapter
    {
        private IReadOnlyList<Channel> _items = [];

        public void Submit(IReadOnlyList<Channel> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        /// <summary>Posicion del canal con ese nombre en lo que se ve, o -1.</summary>
        public int IndexOf(string name)
        {
            for (var i = 0; i < _items.Count; i++)
            {
                if (string.Equals(_items[i].Name, name, StringComparison.OrdinalIgnoreCase))
                    return i;
            }

            return -1;
        }

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.item_channel, parent, false)!;
            var holder = new Holder(view);
            view.SetOnKeyListener(new KeyListener((key, _) =>
                holder.BindingAdapterPosition != RecyclerView.NoPosition && onKey(holder.BindingAdapterPosition, key)));

            view.Click += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                    onPlay(_items[holder.BindingAdapterPosition]);
            };

            view.LongClick += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                {
                    onToggleFavorite(_items[holder.BindingAdapterPosition]);
                }
            };

            holder.FavoriteBadge.Click += (_, _) =>
            {
                if (holder.BindingAdapterPosition != RecyclerView.NoPosition)
                {
                    onToggleFavorite(_items[holder.BindingAdapterPosition]);
                }
            };

            return holder;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var h = (Holder)holder;
            var channel = _items[position];
            h.Name.Text = channel.Name;
            logos.Load(h.Logo, channel.LogoUrl);

            // Estado de favorito
            var isFav = prefs.IsFavorite(channel.Name);
            h.FavoriteBadge.SetImageResource(isFav ? Resource.Drawable.ic_star_filled : Resource.Drawable.ic_star_outline);
            h.FavoriteBadge.Alpha = isFav ? 1f : 0.55f;

            // Informacion del programa actual en emision segun EPG
            var prog = epg.GetCurrentProgram(channel.EpgId);
            if (prog is not null)
            {
                h.EpgProgram.Text = $"{prog.StartTime:HH:mm} {prog.Title}";
                h.EpgProgram.Visibility = ViewStates.Visible;
            }
            else
            {
                h.EpgProgram.Visibility = ViewStates.Gone;
            }
        }

        private sealed class Holder : RecyclerView.ViewHolder
        {
            public Holder(View view) : base(view)
            {
                Logo = view.FindViewById<ImageView>(Resource.Id.logo)!;
                Name = view.FindViewById<TextView>(Resource.Id.name)!;
                FavoriteBadge = view.FindViewById<ImageView>(Resource.Id.favorite_badge)!;
                EpgProgram = view.FindViewById<TextView>(Resource.Id.epg_program)!;
            }

            public ImageView Logo { get; }
            public TextView Name { get; }
            public ImageView FavoriteBadge { get; }
            public TextView EpgProgram { get; }
        }
    }
}
