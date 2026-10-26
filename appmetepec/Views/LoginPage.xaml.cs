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
        VersionLabel.Text = $"Versión {AppInfo.Current.VersionString}";
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        var username = UsernameEntry.Text?.Trim() ?? "";
        var password = PasswordEntry.Text ?? "";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            await DialogoPage.MostrarAsync(
                Navigation,
                titulo: "Datos incompletos",
                mensaje: "Captura tu usuario y tu contraseña para iniciar sesión.",
                botonPrincipal: "Entendido",
                icono: "!");
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

            // appmetepec es exclusiva para ciudadanos: una cuenta de agente/usuario interno
            // tambien puede autenticarse contra /seguridad/login (lo comparte con el sitio web),
            // pero no tiene Ciudadano asociado. GET /ciudadanos/me regresa 404 limpio (sin
            // excepcion) en ese caso -- eso es lo que se rechaza aqui. Una excepcion (sin
            // conexion momentanea) NO se trata como "no es ciudadano": no se bloquea el login por
            // eso, ReportPage vuelve a intentar la misma verificacion antes de crear un ticket.
            int? idCiudadano = null;
            var verificado = false;
            try
            {
                idCiudadano = await _api.GetMyCiudadanoAsync();
                verificado = true;
            }
            catch (Exception)
            {
            }

            if (verificado && idCiudadano is null)
            {
                _preferences.Logout();
                await DialogoPage.MostrarAsync(
                    Navigation,
                    titulo: "Acceso no permitido",
                    mensaje: "Esta aplicación es exclusiva para ciudadanos.",
                    nota: "La cuenta con la que intentas entrar no tiene un perfil de ciudadano asociado.",
                    botonPrincipal: "Entendido",
                    icono: "!",
                    colores: DialogoPage.Rojo);
                return;
            }

            _preferences.CiudadanoId = idCiudadano ?? 0;

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
            await DialogoPage.MostrarAsync(
                Navigation,
                titulo: "No se pudo conectar",
                mensaje: ErrorMessageHelper.Traducir(ex),
                nota: "Revisa tu conexión a internet e intenta de nuevo.",
                botonPrincipal: "Entendido",
                icono: "!");
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
                var tieneNombre = !string.IsNullOrWhiteSpace(identidad.NombreCorto);
                var irARecuperar = await DialogoPage.MostrarAsync(
                    Navigation,
                    titulo: "Usuario o contraseña incorrectos",
                    mensaje: tieneNombre ? "La contraseña no coincide con la cuenta de:" : "La contraseña no coincide con la cuenta.",
                    destacado: tieneNombre ? identidad.NombreCorto : null,
                    nota: "¿Eres tú y no recuerdas tu contraseña? Puedes recuperar tu cuenta con tu correo electrónico.",
                    botonPrincipal: "Recuperar cuenta",
                    botonSecundario: "Intentar de nuevo",
                    icono: "?");

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

        await DialogoPage.MostrarAsync(
            Navigation,
            titulo: "Acceso denegado",
            mensaje: "El usuario o la contraseña son incorrectos. Verifica tus datos e intenta de nuevo.",
            botonPrincipal: "Entendido",
            icono: "!",
            colores: DialogoPage.Rojo);
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
