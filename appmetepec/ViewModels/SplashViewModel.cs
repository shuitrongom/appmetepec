using appmetepec.Services;
using appmetepec.Views;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace appmetepec.ViewModels;

public sealed partial class SplashViewModel : ObservableObject
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;

    [ObservableProperty]
    private bool isBusy = true;

    public SplashViewModel(PreferencesService preferences, MetepecApiService api)
    {
        _preferences = preferences;
        _api = api;
    }

    [RelayCommand]
    private async Task InitializeAsync()
    {
        if (!IsBusy)
        {
            return;
        }

        try
        {
            await Task.Delay(AppConstants.MinimumSplashDurationMs);
            await AvisarSiHayVersionNuevaAsync();

            var route = _preferences.IsLoggedIn ? nameof(HomePage) : nameof(LoginPage);
            await Shell.Current.GoToAsync($"//{route}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    // La "version mas reciente" la administra un admin desde el panel web (catalogo Version de
    // la app -> VersionAppsController), no depende de ningun archivo de configuracion del
    // back-end ni de codigo de esta app -- asi alguien con acceso al sistema productivo puede
    // avisar de una version nueva sin necesitar acceso al servidor. Best-effort: si el back-end
    // no responde, simplemente no se muestra ningun aviso (nunca bloquea el arranque).
    private async Task AvisarSiHayVersionNuevaAsync()
    {
        try
        {
            // Cada plataforma tiene su propia version en el catalogo (Version de la app).
            var plataforma = DeviceInfo.Current.Platform == DevicePlatform.iOS ? "IOS" : "ANDROID";
            var remoto = await _api.GetVersionAppActualAsync(plataforma);
            if (remoto is null || string.IsNullOrWhiteSpace(remoto.VersionReciente))
            {
                return;
            }

            if (!EsMasNueva(remoto.VersionReciente, AppInfo.Current.VersionString))
            {
                return;
            }

            // A proposito SIN deduplicar por "ya se avisó de esta version": si el usuario cierra
            // la app sin actualizar, debe seguir viendo el aviso en cada apertura -- de lo
            // contrario, quien lo cierra una vez nunca vuelve a enterarse de que sigue en una
            // version vieja. Solo deja de aparecer cuando de verdad actualiza (AppInfo.VersionString
            // ya no es menor a VersionReciente).
            var tienda = plataforma == "IOS" ? "App Store" : "Play Store";
            var mensaje = string.IsNullOrWhiteSpace(remoto.Mensaje)
                ? "Hay una nueva versión de Metepec *7311 con mejoras para ti."
                : remoto.Mensaje;
            var nota = $"Actualízala desde la {tienda} para seguir disfrutando de las mejoras.";

            // Sin ID de App Store configurado no hay a donde mandar al usuario en iPhone: solo aviso.
            // Mismo dialogo con estilo de la app que el registro/login (DialogoPage), version destacada.
            var puedeAbrirTienda = plataforma != "IOS" || !string.IsNullOrWhiteSpace(AppConstants.AppStoreId);
            var actualizar = await DialogoPage.MostrarAsync(
                Shell.Current.Navigation,
                titulo: "Nueva versión disponible",
                mensaje: mensaje,
                destacado: $"Versión {remoto.VersionReciente}",
                nota: nota,
                botonPrincipal: puedeAbrirTienda ? "Actualizar" : "Entendido",
                botonSecundario: puedeAbrirTienda ? "Más tarde" : null,
                icono: "↑",
                colores: DialogoPage.Verde);
            if (!puedeAbrirTienda)
            {
                return;
            }

            if (actualizar)
            {
                await AbrirTiendaAsync(plataforma);
            }
        }
        catch
        {
            // Ignorado a proposito (ver comentario del metodo).
        }
    }

    // Abre la ficha de la app en la tienda. Primero la app de la tienda (market:// o itms-apps://);
    // si no se puede (p. ej. celular sin Play Store), la pagina web de la tienda en el navegador.
    private static async Task AbrirTiendaAsync(string plataforma)
    {
        var (urlApp, urlWeb) = plataforma == "IOS"
            ? ($"itms-apps://apps.apple.com/app/id{AppConstants.AppStoreId}", $"https://apps.apple.com/app/id{AppConstants.AppStoreId}")
            : ($"market://details?id={AppConstants.PlayStorePackage}", $"https://play.google.com/store/apps/details?id={AppConstants.PlayStorePackage}");

        try
        {
            if (await Launcher.Default.TryOpenAsync(new Uri(urlApp)))
            {
                return;
            }
        }
        catch
        {
            // Se intenta con la pagina web.
        }

        try
        {
            await Browser.Default.OpenAsync(new Uri(urlWeb), BrowserLaunchMode.External);
        }
        catch
        {
            // Sin navegador ni tienda: no hay nada mas que hacer; el aviso vuelve a salir al abrir la app.
        }
    }

    private static bool EsMasNueva(string versionRemota, string versionInstalada)
    {
        if (Version.TryParse(versionRemota, out var remota) && Version.TryParse(versionInstalada, out var instalada))
        {
            return remota > instalada;
        }

        // Si alguna no tiene formato Major.Minor[.Build[.Revision]], mejor esfuerzo por texto.
        return !string.Equals(versionRemota, versionInstalada, StringComparison.OrdinalIgnoreCase);
    }
}
