using Android.Runtime;
using Android.Util;
using Android.Widget;
using TdtOnline.Localization;
using TdtOnline.Services;

namespace TdtOnline;

/// <summary>
/// La aplicacion entera: aqui van los ganchos del gestor global de excepciones (constitucion
/// general 6.12). Un error que no se esperaba nunca cierra la aplicacion: se registra con su traza
/// (logcat y <c>files/errors.log</c>), se avisa con un aviso breve en el idioma del usuario y se
/// sigue.
/// </summary>
/// <remarks>
/// Es .NET para Android sin MAUI, asi que no vale la pieza comun <c>Shared/CrashGuard.cs</c>. Tres
/// ganchos: <see cref="AndroidEnvironment.UnhandledExceptionRaiser"/> (lo que sube por un
/// manejador de Android, como un Click; con <c>Handled = true</c> no se cierra),
/// <see cref="AppDomain.UnhandledException"/> (solo se puede registrar) y
/// <see cref="TaskScheduler.UnobservedTaskException"/> (un <c>_ = TareaAsync()</c> que falla).
/// </remarks>
[Application]
public sealed class TdtApplication : Application
{
    private const string Tag = "TdtOnline";
    private static ErrorLog? _log;

    public TdtApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
    {
    }

    public override void OnCreate()
    {
        base.OnCreate();
        _log = new ErrorLog(Path.Combine(FilesDir!.AbsolutePath, "errors.log"));

        // El idioma elegido en «Acerca de», para que el aviso salga en el del usuario desde el arranque.
        Loc.Override = new UserPreferences(this).Language;

        AndroidEnvironment.UnhandledExceptionRaiser += (_, e) =>
        {
            e.Handled = true;
            Report(e.Exception, "android");
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
                Report(ex, "appdomain");
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            e.SetObserved();
            Report(e.Exception, "task");
        };
    }

    /// <summary>Registra el error con su traza y avisa sin cerrar. Nunca lanza.</summary>
    public static void Report(Exception ex, string source)
    {
        try
        {
            Log.Error(Tag, $"Error no controlado ({source}): {ex}");
            _log?.Write(ex, source, DateTime.Now);
        }
        catch
        {
            // Registrar no puede ser otra fuente de errores.
        }

        try
        {
            // Un error en bucle no debe llenar la pantalla de avisos.
            if (_log is not null && !_log.ShouldNotify(DateTime.UtcNow))
                return;

            var context = Android.App.Application.Context;
            new Android.OS.Handler(Android.OS.Looper.MainLooper!).Post(() =>
            {
                try
                {
                    Toast.MakeText(context, Loc.Get("UnexpectedError"), ToastLength.Long)?.Show();
                }
                catch
                {
                    // Sin aviso, pero registrado.
                }
            });
        }
        catch
        {
        }
    }
}
