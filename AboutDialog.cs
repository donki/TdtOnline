using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using TdtOnline.Localization;

namespace TdtOnline;

/// <summary>
/// Pantalla «Acerca de» canonica sOCratic (constitucion general seccion 1, constitucion mobile).
/// Muestra version, autor Josep Sola, licencia MIT, fuentes (TDTChannels) y principios de privacidad.
/// </summary>
public static class AboutDialog
{
    public static void Show(AppCompatActivity activity)
    {
        var inflater = LayoutInflater.From(activity);
        var view = inflater?.Inflate(Resource.Layout.dialog_about, null);
        if (view is null)
            return;

        var builder = new AndroidX.AppCompat.App.AlertDialog.Builder(activity);
        builder.SetView(view);
        var dialog = builder.Create();
        if (dialog is null)
            return;

        var versionView = view.FindViewById<TextView>(Resource.Id.about_version);
        var authorView = view.FindViewById<TextView>(Resource.Id.about_author);
        var licenseView = view.FindViewById<TextView>(Resource.Id.about_license);
        var descView = view.FindViewById<TextView>(Resource.Id.about_description);
        var closeBtn = view.FindViewById<Button>(Resource.Id.btn_close_about);

        string versionName = "2026.09.12.1";
        long versionCode = 2026091201;
        try
        {
            var info = activity.PackageManager?.GetPackageInfo(activity.PackageName!, 0);
            if (info is not null)
            {
                versionName = info.VersionName ?? versionName;
#pragma warning disable CA1416, CA1422, CS0618
                versionCode = (int)Build.VERSION.SdkInt >= 28
                    ? info.LongVersionCode
                    : info.VersionCode;
#pragma warning restore CA1416, CA1422, CS0618
            }
        }
        catch
        {
            // Ignorar
        }

        if (versionView is not null)
            versionView.Text = $"v{versionName} ({versionCode})";

        if (authorView is not null)
            authorView.Text = Loc.Get("AboutAuthor");

        if (licenseView is not null)
            licenseView.Text = Loc.Get("AboutLicense");

        if (descView is not null)
            descView.Text = Loc.Get("AboutDescription");

        if (closeBtn is not null)
        {
            closeBtn.Text = Loc.Get("Close");
            closeBtn.Click += (_, _) => dialog.Dismiss();
        }

        dialog.Show();
        closeBtn?.RequestFocus();
    }
}
