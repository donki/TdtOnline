using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using TdtOnline.Localization;
using TdtOnline.Services;

namespace TdtOnline;

/// <summary>
/// Reproduce un canal en directo a pantalla completa con ExoPlayer (Media3).
/// </summary>
/// <remarks>
/// Un canal trae varias direcciones (la principal y alguna alternativa): si la que suena falla,
/// se pasa a la siguiente sin que el usuario tenga que hacer nada. Solo cuando se acaban se
/// enseña el aviso. En la tele el mando maneja el PlayerView (pausa/reanudar con OK); atras
/// vuelve a la lista.
/// </remarks>
[Activity(
    Label = "TDT Online",
    Theme = "@style/AppTheme.Player",
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.KeyboardHidden,
    ScreenOrientation = ScreenOrientation.SensorLandscape)]
public sealed class PlayerActivity : AppCompatActivity
{
    public const string ExtraName = "name";
    public const string ExtraUrls = "urls";
    public const string ExtraEpgId = "epg_id";
    public const string ExtraResolver = "resolver";

    private IExoPlayer? _player;
    private PlayerView _view = null!;
    private View _overlay = null!;
    private TextView _error = null!;
    private TextView _channelTitle = null!;
    private TextView _epgInfo = null!;
    private TextView _epgDesc = null!;
    private ImageView _btnFavorite = null!;

    private UserPreferences _prefs = null!;
    private EpgService _epg = null!;

    private string _channelName = string.Empty;
    private string? _epgId;
    private string[] _urls = [];
    private int _current;

    // Cadenas que dan la direccion al ir a verlas (DMAX): cuando se pidio, para no pedirla en bucle.
    private string? _resolver;
    private DateTime _resolvedAt = DateTime.MinValue;
    private CancellationTokenSource? _resolving;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_player);

        _prefs = new UserPreferences(this);
        _epg = new EpgService(CacheDir!.AbsolutePath);

        _view = FindViewById<PlayerView>(Resource.Id.player)!;
        _overlay = FindViewById<View>(Resource.Id.channelOverlay)!;
        _error = FindViewById<TextView>(Resource.Id.error)!;
        _channelTitle = FindViewById<TextView>(Resource.Id.channelName)!;
        _epgInfo = FindViewById<TextView>(Resource.Id.epgInfo)!;
        _epgDesc = FindViewById<TextView>(Resource.Id.epgDesc)!;
        _btnFavorite = FindViewById<ImageView>(Resource.Id.btn_player_favorite)!;

        _channelName = Intent?.GetStringExtra(ExtraName) ?? string.Empty;
        _epgId = Intent?.GetStringExtra(ExtraEpgId);
        _urls = Intent?.GetStringArrayExtra(ExtraUrls) ?? [];
        _resolver = Intent?.GetStringExtra(ExtraResolver);

        _channelTitle.Text = _channelName;
        UpdateFavoriteButton();

        _btnFavorite.Click += (_, _) => ToggleFavorite();

        // Enlazar visibilidad de overlay con los controles del reproductor
        _view.SetControllerVisibilityListener(new ControllerVisibility(_overlay));

        // Cargar informacion EPG del canal
        _ = LoadEpgAsync();

        // Guardar como ultimo canal visto
        if (!string.IsNullOrWhiteSpace(_channelName))
        {
            _prefs.LastChannel = _channelName;
        }
    }

    private void UpdateFavoriteButton()
    {
        var isFav = _prefs.IsFavorite(_channelName);
        _btnFavorite.SetImageResource(isFav ? Resource.Drawable.ic_star_filled : Resource.Drawable.ic_star_outline);
    }

    private void ToggleFavorite()
    {
        var added = _prefs.ToggleFavorite(_channelName);
        UpdateFavoriteButton();
        var msg = added ? Loc.Format("FavoriteAdded", _channelName) : Loc.Format("FavoriteRemoved", _channelName);
        Toast.MakeText(this, msg, ToastLength.Short)?.Show();
    }

    private async Task LoadEpgAsync()
    {
        if (string.IsNullOrWhiteSpace(_epgId))
            return;

        await _epg.LoadAsync().ConfigureAwait(false);

        RunOnUiThread(() =>
        {
            var currentProg = _epg.GetCurrentProgram(_epgId);
            if (currentProg is not null)
            {
                _epgInfo.Text = $"{currentProg.TimeRange} · {currentProg.Title}";
                _epgInfo.Visibility = ViewStates.Visible;

                if (!string.IsNullOrWhiteSpace(currentProg.Description))
                {
                    _epgDesc.Text = currentProg.Description;
                    _epgDesc.Visibility = ViewStates.Visible;
                }
            }
        });
    }

    public override bool OnKeyDown([Android.Runtime.GeneratedEnum] Keycode keyCode, KeyEvent? e)
    {
        // Teclas amarillas / de favoritos en mandos de TV
        if (keyCode is Keycode.ProgYellow or Keycode.ButtonY)
        {
            ToggleFavorite();
            return true;
        }

        return base.OnKeyDown(keyCode, e);
    }

    protected override void OnStart()
    {
        base.OnStart();
        _player = new ExoPlayerBuilder(this).Build()!;
        _player.AddListener(new Listener(this));
        _view.Player = _player;

        if (SonicLive.Handles(_resolver))
            ResolveAndPlay();
        else
            PlayCurrent();
    }

    protected override void OnStop()
    {
        _resolving?.Cancel();
        _resolving = null;
        _view.Player = null;
        _player?.Release();
        _player = null;
        base.OnStop();
    }

    /// <summary>Pide la direccion de hoy al servicio de la cadena y, con ella, reproduce.</summary>
    private async void ResolveAndPlay()
    {
        _resolving?.Cancel();
        var cts = _resolving = new CancellationTokenSource();

        _error.Text = Loc.Get("Trying");
        _error.Visibility = ViewStates.Visible;

        var urls = await SonicLive.ResolveAsync(_resolver!, cts.Token);
        if (cts.IsCancellationRequested || _player is null)
            return;

        _urls = urls;
        _current = 0;
        _resolvedAt = DateTime.UtcNow;
        PlayCurrent();
    }

    private void PlayCurrent()
    {
        if (_player is null || _current >= _urls.Length)
        {
            _error.Text = Loc.Get("PlayFailed");
            _error.Visibility = ViewStates.Visible;
            return;
        }

        _error.Visibility = ViewStates.Gone;
        _player.SetMediaItem(MediaItem.FromUri(_urls[_current]));
        _player.PlayWhenReady = true;
        _player.Prepare();
    }

    /// <summary>Falla la emision: a la siguiente direccion, si queda alguna.</summary>
    private void OnPlaybackError()
    {
        // La direccion pedida al servicio caduca a los minutos: se pide otra una vez, no en bucle.
        if (SonicLive.Handles(_resolver) && DateTime.UtcNow - _resolvedAt > TimeSpan.FromSeconds(30))
        {
            ResolveAndPlay();
            return;
        }

        _current++;
        if (_current < _urls.Length)
        {
            _error.Text = Loc.Get("Trying");
            _error.Visibility = ViewStates.Visible;
        }

        PlayCurrent();
    }

    private sealed class Listener(PlayerActivity owner) : Java.Lang.Object, IPlayerListener
    {
        public void OnPlayerError(PlaybackException? error) => owner.RunOnUiThread(owner.OnPlaybackError);
    }

    private sealed class ControllerVisibility(View overlay) : Java.Lang.Object, PlayerView.IControllerVisibilityListener
    {
        public void OnVisibilityChanged(int visibility) => overlay.Visibility = (ViewStates)visibility;
    }
}
