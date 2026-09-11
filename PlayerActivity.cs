using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Media3.Common;
using AndroidX.Media3.ExoPlayer;
using AndroidX.Media3.UI;
using TdtOnline.Localization;

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

    private IExoPlayer? _player;
    private PlayerView _view = null!;
    private TextView _error = null!;
    private string[] _urls = [];
    private int _current;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_player);

        _view = FindViewById<PlayerView>(Resource.Id.player)!;
        _error = FindViewById<TextView>(Resource.Id.error)!;
        FindViewById<TextView>(Resource.Id.channelName)!.Text = Intent?.GetStringExtra(ExtraName) ?? string.Empty;
        _urls = Intent?.GetStringArrayExtra(ExtraUrls) ?? [];

        // El nombre del canal se enseña con los controles y se va con ellos.
        _view.SetControllerVisibilityListener(new ControllerVisibility(FindViewById<TextView>(Resource.Id.channelName)!));
    }

    protected override void OnStart()
    {
        base.OnStart();
        _player = new ExoPlayerBuilder(this).Build()!;
        _player.AddListener(new Listener(this));
        _view.Player = _player;
        PlayCurrent();
    }

    protected override void OnStop()
    {
        _view.Player = null;
        _player?.Release();
        _player = null;
        base.OnStop();
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

    private sealed class ControllerVisibility(TextView name) : Java.Lang.Object, PlayerView.IControllerVisibilityListener
    {
        public void OnVisibilityChanged(int visibility) => name.Visibility = (ViewStates)visibility;
    }
}
