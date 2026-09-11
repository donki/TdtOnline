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
    private RecyclerView _categoriesView = null!;
    private RecyclerView _channelsView = null!;
    private TextView _status = null!;
    private TextView _footer = null!;
    private ChannelAdapter _channels = null!;
    private CategoryAdapter _categories = null!;
    private IReadOnlyList<Category> _loaded = [];

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_main);

        var cache = CacheDir!.AbsolutePath;
        _catalog = new ChannelCatalog(cache);
        _logos = new LogoLoader(cache);

        FindViewById<TextView>(Resource.Id.title)!.Text = Loc.Get("AppTitle");
        _status = FindViewById<TextView>(Resource.Id.status)!;
        _footer = FindViewById<TextView>(Resource.Id.footer)!;
        _footer.Text = Loc.Format("Source", ChannelCatalog.SourceName);

        _categoriesView = FindViewById<RecyclerView>(Resource.Id.categories)!;
        _categoriesView.SetLayoutManager(new LinearLayoutManager(this, LinearLayoutManager.Horizontal, false));
        _categories = new CategoryAdapter(ShowCategory);
        _categoriesView.SetAdapter(_categories);

        _channelsView = FindViewById<RecyclerView>(Resource.Id.channels)!;
        _channelsView.SetLayoutManager(new GridLayoutManager(this, ColumnsForWidth()));
        _channels = new ChannelAdapter(_logos, Play);
        _channelsView.SetAdapter(_channels);

        _ = LoadAsync(forceRefresh: false);
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
            _loaded = await _catalog.LoadAsync(forceRefresh);
            _categories.Submit(_loaded);
            if (_loaded.Count > 0)
                ShowCategory(0);
            _status.Text = Loc.Format("ChannelsCount", _loaded.Sum(c => c.Channels.Count));
        }
        catch (Exception)
        {
            _status.Text = Loc.Get("LoadFailed");
        }
    }

    private void ShowCategory(int index)
    {
        if (index < 0 || index >= _loaded.Count)
            return;

        _categories.Select(index);
        _channels.Submit(_loaded[index].Channels);
        _channelsView.ScrollToPosition(0);
    }

    private void Play(Channel channel)
    {
        var intent = new Intent(this, typeof(PlayerActivity));
        intent.PutExtra(PlayerActivity.ExtraName, channel.Name);
        intent.PutExtra(PlayerActivity.ExtraUrls, channel.StreamUrls.ToArray());
        StartActivity(intent);
    }

    public override bool OnKeyDown([Android.Runtime.GeneratedEnum] Keycode keyCode, KeyEvent? e)
    {
        // En la tele, el boton de «menu» o el de repetir del mando vuelve a bajar la lista.
        if (keyCode is Keycode.Menu or Keycode.Refresh)
        {
            _ = LoadAsync(forceRefresh: true);
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

    private sealed class ChannelAdapter(LogoLoader logos, Action<Channel> onPlay) : RecyclerView.Adapter
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
            return holder;
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var h = (Holder)holder;
            var channel = _items[position];
            h.Name.Text = channel.Name;
            logos.Load(h.Logo, channel.LogoUrl);
        }

        private sealed class Holder : RecyclerView.ViewHolder
        {
            public Holder(View view) : base(view)
            {
                Logo = view.FindViewById<ImageView>(Resource.Id.logo)!;
                Name = view.FindViewById<TextView>(Resource.Id.name)!;
            }

            public ImageView Logo { get; }

            public TextView Name { get; }
        }
    }
}
