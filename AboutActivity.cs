using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using TdtOnline.Localization;
using TdtOnline.Services;

namespace TdtOnline;

/// <summary>
/// «Acerca de» con la estructura canonica del catalogo, la misma que Music Player: cabecera,
/// contacto, idioma, privacidad, licencia y aviso legal.
/// </summary>
[Activity(Label = "TDT Online", ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize)]
public sealed class AboutActivity : AppCompatActivity
{
    private const string ContactEmail = "jsoladelarosa@gmail.com";

    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        SetContentView(Resource.Layout.activity_about);
        Title = Loc.Get("AboutTitle");

        FindViewById<TextView>(Resource.Id.about_app_name)!.Text = Loc.Get("AppTitle");
        FindViewById<TextView>(Resource.Id.about_version)!.Text = $"v{VersionName()}";
        FindViewById<TextView>(Resource.Id.about_description)!.Text = Loc.Get("AboutDescription");
        FindViewById<TextView>(Resource.Id.about_publisher)!.Text = Loc.Get("Publisher");

        FindViewById<TextView>(Resource.Id.contact_title)!.Text = Loc.Get("ContactTitle");
        var contact = FindViewById<Button>(Resource.Id.contact_button)!;
        contact.Text = ContactEmail;
        contact.Click += (_, _) => WriteToAuthor();
        FindViewById<TextView>(Resource.Id.contact_hint)!.Text = Loc.Get("ContactHint");

        FindViewById<TextView>(Resource.Id.language_title)!.Text = Loc.Get("SectionLanguage");
        var spanish = FindViewById<Button>(Resource.Id.spanish_button)!;
        var english = FindViewById<Button>(Resource.Id.english_button)!;
        spanish.Text = Loc.Get("SpanishButton");
        english.Text = Loc.Get("EnglishButton");
        spanish.Selected = Loc.Language == "es";
        english.Selected = Loc.Language == "en";
        spanish.Click += (_, _) => UseLanguage("es");
        english.Click += (_, _) => UseLanguage("en");
        FindViewById<TextView>(Resource.Id.language_hint)!.Text = Loc.Get("LanguageHint");

        FindViewById<TextView>(Resource.Id.privacy_title)!.Text = Loc.Get("PrivacyTitle");
        FindViewById<TextView>(Resource.Id.privacy_text)!.Text = Loc.Format("PrivacyText", ChannelCatalog.SourceName);

        FindViewById<TextView>(Resource.Id.license_title)!.Text = Loc.Get("LicenseTitle");
        FindViewById<TextView>(Resource.Id.license_text)!.Text = Loc.Get("LicenseText");
        FindViewById<TextView>(Resource.Id.license_line)!.Text = Loc.Get("LicenseLine");

        FindViewById<TextView>(Resource.Id.legal_title)!.Text = Loc.Get("LegalTitle");
        FindViewById<TextView>(Resource.Id.legal_text1)!.Text = Loc.Get("LegalText1");
        FindViewById<TextView>(Resource.Id.legal_text2)!.Text = Loc.Get("LegalText2");
        FindViewById<TextView>(Resource.Id.warning_text)!.Text = Loc.Get("WarningText");

        var close = FindViewById<Button>(Resource.Id.close_button)!;
        close.Text = Loc.Get("Close");
        close.Click += (_, _) => Finish();
    }

    private string VersionName()
    {
        try
        {
            return PackageManager?.GetPackageInfo(PackageName!, 0)?.VersionName ?? "?";
        }
        catch (Exception)
        {
            return "?";
        }
    }

    private void WriteToAuthor()
    {
        try
        {
            var intent = new Intent(Intent.ActionSendto, Android.Net.Uri.Parse($"mailto:{ContactEmail}"));
            intent.PutExtra(Intent.ExtraSubject, Loc.Get("AppTitle"));
            StartActivity(intent);
        }
        catch (Exception)
        {
            // Sin aplicacion de correo (una tele, por ejemplo): la direccion esta a la vista.
        }
    }

    /// <summary>Cambia el idioma, lo guarda y rehace la pantalla; la principal se rehace al volver.</summary>
    private void UseLanguage(string language)
    {
        if (Loc.Language == language)
            return;

        new UserPreferences(this).Language = language;
        Loc.Override = language;
        Recreate();
    }
}
