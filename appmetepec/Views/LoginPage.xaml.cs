using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class LoginPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private readonly PushRegistrationService _pushRegistration;

    public LoginPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState, PushRegistrationService pushRegistration)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
        _pushRegistration = pushRegistration;
        VersionLabel.Text = $"v{AppInfo.Current.VersionString}";
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DisplayAlert("Datos incompletos", "Captura usuario y contrasena.", "Aceptar");
            return;
        }

        try
        {
            LoginButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var (latitud, longitud) = await IntentarObtenerUbicacionAsync();
            var result = await _api.LoginAsync(username, password, latitud, longitud);

            if (result is null)
            {
                await OfrecerRecuperacionAsync(username);
                return;
            }

            _preferences.JwtToken = result.Token;
            _preferences.CurrentUser = new UserProfile(
                result.User.FullName,
                result.User.Email,
                result.User.PhoneNumber ?? "");
            try
            {
                _preferences.CiudadanoId = await _api.GetMyCiudadanoAsync() ?? 0;
            }
            catch (Exception)
            {
                // No bloquea el login; ReportPage vuelve a intentarlo antes de crear un ticket.
            }

            // Se dispara aqui (ademas de HomePage.OnAppearing) para que cada login reintente el
            // registro, incluso si un intento previo fallo en silencio (permiso no otorgado,
            // Firebase aun inicializando, etc.) -- PushRegistrationService hace upsert por token en
            // el back-end, asi que repetirlo es inofensivo.
            await _pushRegistration.RegistrarSiAplicaAsync(_preferences.CiudadanoId);

            await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Login] {ex.GetType().Name}: {ex.Message}");
            await DisplayAlert("No se pudo conectar", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            LoginButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    // Cuando el login falla, se identifica activamente la credencial (usuario o correo) para
    // preguntar "eres tu?" y, si acepta, mandarlo directo al flujo de recuperacion con el correo
    // ya precargado. Si la identificacion misma falla o la credencial no existe, cae al mensaje
    // generico de siempre.
    private async Task OfrecerRecuperacionAsync(string credential)
    {
        try
        {
            var identidad = await _api.IdentificarUsuarioAsync(credential);
            if (identidad is { Existe: true })
            {
                var saludo = string.IsNullOrWhiteSpace(identidad.NombreCorto) ? "" : $" {identidad.NombreCorto}";
                var irARecuperar = await DisplayAlert(
                    "Acceso denegado",
                    $"Usuario o contrasena incorrectos. ¿Eres{saludo}? Si no recuerdas tu contrasena, puedes recuperar tu cuenta.",
                    "Recuperar cuenta",
                    "Cancelar");

                if (irARecuperar && !string.IsNullOrWhiteSpace(identidad.Email))
                {
                    _navigationState.RecoverAccountEmail = identidad.Email;
                    await Shell.Current.GoToAsync(nameof(RecoverAccountPage));
                }
                return;
            }
        }
        catch
        {
            // Si falla la identificacion (sin conexion, etc.), se cae al mensaje generico de abajo.
        }

        await DisplayAlert("Acceso denegado", "Usuario o contrasena incorrectos.", "Aceptar");
    }

    // Best-effort: nunca bloquea ni retrasa el login por falta de permiso, GPS apagado o
    // timeout -- solo alimenta el Mapa de Concentracion de Usuarios cuando se puede.
    private static async Task<(decimal? Latitud, decimal? Longitud)> IntentarObtenerUbicacionAsync()
    {
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(5)));
            return location is null ? (null, null) : ((decimal?)location.Latitude, (decimal?)location.Longitude);
        }
        catch
        {
            return (null, null);
        }
    }

    private void OnTogglePasswordTapped(object sender, TappedEventArgs e)
    {
        PasswordEntry.IsPassword = !PasswordEntry.IsPassword;
        EyeLabel.TextColor = PasswordEntry.IsPassword
            ? Color.FromArgb("#BBBBBB")
            : Color.FromArgb("#F89A1C");
    }

    private async void OnTermsTapped(object sender, TappedEventArgs e)
    {
        await Launcher.Default.OpenAsync(AppConstants.PrivacyUrl);
    }

    private async void OnRegisterTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RegisterPage));
    }

    private async void OnRecoverAccountTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync(nameof(RecoverAccountPage));
    }
}
