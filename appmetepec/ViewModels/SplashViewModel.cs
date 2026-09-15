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
            var remoto = await _api.GetVersionAppActualAsync("ANDROID");
            if (remoto is null || string.IsNullOrWhiteSpace(remoto.VersionReciente))
            {
                return;
            }

            if (!EsMasNueva(remoto.VersionReciente, AppInfo.Current.VersionString))
            {
                return;
            }

            // Ya se aviso de esta misma version reciente en un lanzamiento anterior -- no
            // insistir en cada apertura, solo cuando el catalogo suba a una version aun mas nueva.
            if (_preferences.UltimaVersionVista == remoto.VersionReciente)
            {
                return;
            }

            var mensaje = string.IsNullOrWhiteSpace(remoto.Mensaje)
                ? $"Hay una nueva versión de Metepec *7311 disponible ({remoto.VersionReciente}). Actualízala desde la Play Store para seguir disfrutando de las mejoras."
                : remoto.Mensaje;

            await Shell.Current.DisplayAlert("Nueva versión disponible", mensaje, "Entendido");
            _preferences.UltimaVersionVista = remoto.VersionReciente;
        }
        catch
        {
            // Ignorado a proposito (ver comentario del metodo).
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
