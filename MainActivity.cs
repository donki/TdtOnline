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

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var cache = CacheDir!.AbsolutePath;
        _prefs = new UserPreferences(this);
        _catalog = new ChannelCatalog(cache, _prefs);
        _logos = new LogoLoader(cache);
        _epg = new EpgService(cache);
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
        _categories = new CategoryAdapter(ShowCategory);
        _categoriesView.SetAdapter(_categories);

        _channelsView = FindViewById<RecyclerView>(Resource.Id.channels)!;
        _channelsView.SetLayoutManager(new GridLayoutManager(this, ColumnsForWidth()));
        _channels = new ChannelAdapter(_logos, _prefs, _epg, Play, ToggleFavorite);
        _channelsView.SetAdapter(_channels);

        _epg.EpgLoaded += () => RunOnUiThread(() => _channels.NotifyDataSetChanged());
        _catalog.Updated += categories => RunOnUiThread(() =>
        {
            // Ha terminado la comprobacion de Free-TV: entran los canales que faltaban.
            _rawCategories = categories;
            RebuildDisplayCategories(maintainCategory: true);
            _status.Text = Loc.Format("ChannelsCount", _rawCategories.Sum(c => c.Channels.Count));
        });

        _ = LoadAsync(forceRefresh: false);
        _ = _epg.LoadAsync();
    }

    protected override void OnResume()
    {
        base.OnResume();
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
            _lastChannelView.Visibility = ViewStates.Gone;
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

    private static string ShortUrl(string url) => Uri.TryCreate(url, UriKind.Absolute, out var u) ? u.Host + u.AbsolutePath : url;

    private void RebuildDisplayCategories(bool maintainCategory)
    {
        var currentSelectedName = _selectedCategoryIndex >= 0 && _selectedCategoryIndex < _displayCategories.Count
            ? _displayCategories[_selectedCategoryIndex].Name
            : null;

        var list = new List<Category>();
        var favSet = _prefs.GetFavorites();

        // 1. Favoritos, siempre el primero aunque este vacio: es el grupo del usuario, y si no se
        //    ve no hay forma de saber que existe ni de meter nada en el.
        var favChannels = new List<Channel>();
        var addedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cat in _rawCategories)
        {
            foreach (var ch in cat.Channels)
            {
                if (favSet.Contains(ch.Name) && addedNames.Add(ch.Name))
                    favChannels.Add(ch);
            }
        }

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
    private List<Channel> AllChannels()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var all = new List<Channel>();
        foreach (var cat in _rawCategories)
        {
            foreach (var ch in cat.Channels)
            {
                if (seen.Add(ch.Name))
                    all.Add(ch);
            }
        }

        return all;
    }

    private void ShowCategory(int index)
    {
        if (index < 0 || index >= _displayCategories.Count)
            return;

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

        var key = Normalize(text);
        var matches = AllChannels().Where(ch => Normalize(ch.Name).Contains(key, StringComparison.Ordinal)).ToList();
        _channels.Submit(matches);
        _channelsView.ScrollToPosition(0);

        _emptyHint.Visibility = matches.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        if (matches.Count == 0)
            _emptyHint.Text = Loc.Format("NoResults", text);
    }

    /// <summary>Minusculas y sin acentos, para que «aragon» encuentre «Aragón TV».</summary>
    private static string Normalize(string text)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text.Normalize(System.Text.NormalizationForm.FormD))
        {
            if (char.IsLetterOrDigit(ch))
                sb.Append(char.ToLowerInvariant(ch));
        }

        return sb.ToString();
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

    // =====================================================================
    //  Adaptadores
    // =====================================================================

    private sealed class CategoryAdapter(Action<int> onSelect) : RecyclerView.Adapter
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
            view.Click += (_, _) => onSelect(holder.BindingAdapterPosition);

            // En la tele basta con posarse encima: pasar por las categorias ya las abre.
            view.FocusChange += (_, args) =>
            {
                if (args.HasFocus && holder.BindingAdapterPosition != RecyclerView.NoPosition)
                    onSelect(holder.BindingAdapterPosition);
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
        Action<Channel> onToggleFavorite) : RecyclerView.Adapter
    {
        private IReadOnlyList<Channel> _items = [];

        public void Submit(IReadOnlyList<Channel> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.item_channel, parent, false)!;
            var holder = new Holder(view);

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
