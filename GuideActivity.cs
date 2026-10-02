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
/// La parrilla: una fila por canal con lo que emite ahora y lo que viene despues. Con el mando se
/// baja de canal en canal y se recorre cada fila; OK sobre el canal o sobre un programa lo abre.
/// </summary>
/// <remarks>
/// Solo salen los canales que tienen guia (la de TDTChannels), en el orden de la lista y con los
/// favoritos delante. No es una rejilla a escala de tiempo: en una tele a tres metros lo que se lee
/// es «ahora» y «luego», y las tarjetas de ancho fijo se recorren mejor con la cruceta.
/// </remarks>
[Activity(Label = "TDT Online", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class GuideActivity : AppCompatActivity
{
    private const int ProgramsPerChannel = GuideLogic.ProgramsPerChannel;

    private ChannelCatalog _catalog = null!;
    private LogoLoader _logos = null!;
    private UserPreferences _prefs = null!;
    private EpgService _epg = null!;

    private RecyclerView _rows = null!;
    private TextView _empty = null!;
    private TextView _clock = null!;
    private RowAdapter _adapter = null!;

    private IReadOnlyList<Category> _categories = [];

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_guide);

        var cache = CacheDir!.AbsolutePath;
        _prefs = new UserPreferences(this);
        _catalog = new ChannelCatalog(cache, _prefs);
        _logos = new LogoLoader(cache);
        _epg = EpgService.Shared(cache);
        Loc.Override = _prefs.Language;

        FindViewById<TextView>(Resource.Id.title)!.Text = Loc.Get("Guide");
        _clock = FindViewById<TextView>(Resource.Id.clock)!;
        _empty = FindViewById<TextView>(Resource.Id.empty)!;
        _rows = FindViewById<RecyclerView>(Resource.Id.rows)!;
        _rows.SetLayoutManager(new LinearLayoutManager(this));
        _adapter = new RowAdapter(_logos, _epg, Play);
        _rows.SetAdapter(_adapter);

        _epg.EpgLoaded += () => RunOnUiThread(Rebuild);
        _catalog.Updated += categories => RunOnUiThread(() => { _categories = categories; Rebuild(); });

        _ = LoadAsync();
    }

    protected override void OnResume()
    {
        base.OnResume();
        _clock.Text = DateTime.Now.ToString("HH:mm");
        if (_categories.Count > 0)
            Rebuild();
    }

    private async Task LoadAsync()
    {
        try
        {
            _categories = await _catalog.LoadAsync();
        }
        catch (Exception)
        {
            _categories = [];
        }

        Rebuild();
        await _epg.LoadAsync();
    }

    /// <summary>Canales con guia, sin repetir, favoritos delante.</summary>
    private void Rebuild()
    {
        // Los canales con guia, cada uno una vez, con los favoritos delante.
        var ordered = GuideLogic.Rows(_categories, _prefs.GetFavorites(), _epg.Has);
        _adapter.Submit(ordered);

        _empty.Visibility = ordered.Count == 0 ? ViewStates.Visible : ViewStates.Gone;
        if (ordered.Count == 0)
            _empty.Text = GuideLogic.EmptyText(_epg.IsLoaded);
    }

    private void Play(Channel channel)
    {
        _prefs.LastChannel = channel.Name;
        StartActivity(PlayerActivity.IntentFor(this, channel));
    }

    // =====================================================================
    //  Adaptadores
    // =====================================================================

    /// <summary>
    /// Agranda una pieza de alto fijo (y, si se pide, de ancho fijo) en la proporcion de la letra del
    /// sistema, cuando es mayor del 100 %. Las filas de la parrilla miden 92dp para que el canal y
    /// sus programas queden alineados; con la letra al 145 % el titulo del programa no cabia y salia
    /// cortado por abajo (2026-09-29).
    /// </summary>
    private static void FitToFontScale(View view, bool width)
    {
        var scale = view.Resources?.Configuration?.FontScale ?? 1f;
        if (scale <= 1f || view.LayoutParameters is not { } lp)
            return;

        if (lp.Height > 0)
            lp.Height = (int)(lp.Height * scale);
        if (width && lp.Width > 0)
            lp.Width = (int)(lp.Width * scale);
        view.LayoutParameters = lp;
    }

    private sealed class RowAdapter(LogoLoader logos, EpgService epg, Action<Channel> onPlay) : RecyclerView.Adapter
    {
        private IReadOnlyList<Channel> _items = [];

        // Las tarjetas de programa se reciclan entre todas las filas.
        private readonly RecyclerView.RecycledViewPool _pool = new();

        public void Submit(IReadOnlyList<Channel> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.item_guide_row, parent, false)!;
            var holder = new Holder(view);
            FitToFontScale(holder.Channel, width: false);
            FitToFontScale(holder.Programs, width: false);
            holder.Programs.SetLayoutManager(new LinearLayoutManager(parent.Context, LinearLayoutManager.Horizontal, false));
            holder.Programs.SetRecycledViewPool(_pool);
            holder.Programs.SetAdapter(new ProgramAdapter(() => onPlay(_items[holder.BindingAdapterPosition])));
            holder.Channel.Click += (_, _) =>
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
            ((ProgramAdapter)h.Programs.GetAdapter()!).Submit(epg.GetUpcoming(channel.EpgId, ProgramsPerChannel));
            h.Programs.ScrollToPosition(0);
        }

        private sealed class Holder : RecyclerView.ViewHolder
        {
            public Holder(View view) : base(view)
            {
                Channel = view.FindViewById<View>(Resource.Id.channel)!;
                Logo = view.FindViewById<ImageView>(Resource.Id.logo)!;
                Name = view.FindViewById<TextView>(Resource.Id.name)!;
                Programs = view.FindViewById<RecyclerView>(Resource.Id.programs)!;
            }

            public View Channel { get; }
            public ImageView Logo { get; }
            public TextView Name { get; }
            public RecyclerView Programs { get; }
        }
    }

    private sealed class ProgramAdapter(Action onPlay) : RecyclerView.Adapter
    {
        private IReadOnlyList<EpgProgram> _items = [];

        public void Submit(IReadOnlyList<EpgProgram> items)
        {
            _items = items;
            NotifyDataSetChanged();
        }

        public override int ItemCount => _items.Count;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            var view = LayoutInflater.From(parent.Context)!.Inflate(Resource.Layout.item_guide_program, parent, false)!;
            FitToFontScale(view, width: true);
            view.Click += (_, _) => onPlay();
            return new Holder(view);
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            var h = (Holder)holder;
            var p = _items[position];
            var (time, current, progress) = GuideLogic.ProgramCell(p, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            h.Time.Text = time;
            h.Title.Text = p.Title;
            h.Progress.Visibility = current ? ViewStates.Visible : ViewStates.Gone;
            if (current)
                h.Progress.Progress = progress;
        }

        private sealed class Holder : RecyclerView.ViewHolder
        {
            public Holder(View view) : base(view)
            {
                Time = view.FindViewById<TextView>(Resource.Id.time)!;
                Title = view.FindViewById<TextView>(Resource.Id.program_title)!;
                Progress = view.FindViewById<ProgressBar>(Resource.Id.progress)!;
            }

            public TextView Time { get; }
            public TextView Title { get; }
            public ProgressBar Progress { get; }
        }
    }
}
