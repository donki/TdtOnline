using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using TdtOnline.Localization;
using TdtOnline.Services;

namespace TdtOnline;

/// <summary>
/// Ajustes: las listas de canales. Se pueden añadir direcciones (JSON de tdt-canales, JSON de
/// TDTChannels o M3U), quitarlas, volver a descargarlas ahora mismo o restaurar la lista por defecto.
/// </summary>
/// <remarks>
/// Cualquier cambio sube <see cref="UserPreferences.ListsVersion"/>; la pantalla principal lo ve al
/// volver y recarga. Los botones son iconos, como manda la constitucion; la linea de ayuda de
/// debajo dice que hace cada uno.
/// </remarks>
[Activity(Label = "TDT Online", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class SettingsActivity : AppCompatActivity
{
    private UserPreferences _prefs = null!;
    private ChannelCatalog _catalog = null!;
    private LinearLayout _lists = null!;
    private EditText _newUrl = null!;

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_settings);

        _prefs = new UserPreferences(this);
        _catalog = new ChannelCatalog(CacheDir!.AbsolutePath, _prefs);
        Loc.Override = _prefs.Language;

        FindViewById<TextView>(Resource.Id.title)!.Text = Loc.Get("Settings");
        FindViewById<TextView>(Resource.Id.lists_title)!.Text = Loc.Get("ListsTitle");
        FindViewById<TextView>(Resource.Id.lists_hint)!.Text = Loc.Get("ListsHint");
        FindViewById<TextView>(Resource.Id.actions_hint)!.Text = Loc.Get("ActionsHint");
        _lists = FindViewById<LinearLayout>(Resource.Id.lists)!;
        _newUrl = FindViewById<EditText>(Resource.Id.new_url)!;
        _newUrl.Hint = Loc.Get("ListUrlHint");
        _newUrl.EditorAction += (_, args) =>
        {
            if (args.ActionId == Android.Views.InputMethods.ImeAction.Done)
            {
                AddList();
                args.Handled = true;
            }
        };

        FindViewById<ImageButton>(Resource.Id.btn_add)!.Click += (_, _) => AddList();
        FindViewById<ImageButton>(Resource.Id.btn_refresh)!.Click += (_, _) =>
        {
            _catalog.ClearCache();
            _prefs.BumpListsVersion();
            Toast.MakeText(this, Loc.Get("RefreshDone"), ToastLength.Short)?.Show();
        };
        FindViewById<ImageButton>(Resource.Id.btn_restore)!.Click += (_, _) =>
        {
            _prefs.ListUrls = [ChannelCatalog.DefaultListUrl];
            _catalog.ClearCache();
            Toast.MakeText(this, Loc.Get("ListsRestored"), ToastLength.Short)?.Show();
            RenderLists();
        };

        var close = FindViewById<Button>(Resource.Id.close_button)!;
        close.Text = Loc.Get("Close");
        close.Click += (_, _) => Finish();

        RenderLists();
    }

    private void RenderLists()
    {
        _lists.RemoveAllViews();
        var urls = _prefs.ListUrls;
        foreach (var url in urls)
        {
            var row = LayoutInflater.Inflate(Resource.Layout.item_list_url, _lists, false)!;
            row.FindViewById<TextView>(Resource.Id.url)!.Text = url;

            var label = row.FindViewById<TextView>(Resource.Id.label)!;
            if (url == ChannelCatalog.DefaultListUrl)
            {
                label.Text = Loc.Get("DefaultListLabel");
                label.Visibility = ViewStates.Visible;
            }

            row.FindViewById<ImageButton>(Resource.Id.btn_remove)!.Click += (_, _) =>
            {
                _prefs.ListUrls = urls.Where(u => u != url).ToList();
                Toast.MakeText(this, Loc.Get("ListRemoved"), ToastLength.Short)?.Show();
                RenderLists();
            };

            _lists.AddView(row);
        }
    }

    private void AddList()
    {
        var text = (_newUrl.Text ?? string.Empty).Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
        {
            Toast.MakeText(this, Loc.Get("ListInvalid"), ToastLength.Short)?.Show();
            return;
        }

        var urls = _prefs.ListUrls.ToList();
        if (urls.Contains(text, StringComparer.OrdinalIgnoreCase))
        {
            Toast.MakeText(this, Loc.Get("ListExists"), ToastLength.Short)?.Show();
            return;
        }

        urls.Add(text);
        _prefs.ListUrls = urls;
        _newUrl.Text = string.Empty;
        Toast.MakeText(this, Loc.Get("ListAdded"), ToastLength.Short)?.Show();
        RenderLists();
    }
}
