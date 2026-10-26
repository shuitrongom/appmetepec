using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class RecoverAccountPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private string _email = "";
    private string _token = "";
    private int _secondsLeft;
    private bool _timerActive;
    private bool _canResend;

    public RecoverAccountPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!string.IsNullOrWhiteSpace(_navigationState.RecoverAccountEmail))
        {
            EmailEntry.Text = _navigationState.RecoverAccountEmail;
            _navigationState.RecoverAccountEmail = null;
        }
    }

    private async void OnSolicitarClicked(object sender, EventArgs e)
    {
        var email = EmailEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            await AvisoAsync("Correo inválido", "Captura el correo electrónico con el que te registraste.", nota: "Ejemplo: nombre@correo.com");
            return;
        }

        try
        {
            SolicitarButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var respuesta = await _api.SolicitarRecuperacionAsync(email);
            if (respuesta?.NoEncontrado == true)
            {
                await AvisoAsync("Correo no registrado", respuesta.Mensaje, nota: "Verifica que sea el mismo correo con el que te registraste.", icono: "?");
                return;
            }

            if (respuesta is null || string.IsNullOrWhiteSpace(respuesta.Token))
            {
                await AvisoAsync("No se pudo continuar", "Por el momento no pudimos procesar tu solicitud. Intenta de nuevo más tarde.", colores: DialogoPage.Rojo);
                return;
            }

            if (respuesta.Bloqueado)
            {
                await AvisoAsync("Demasiados intentos", respuesta.Mensaje, colores: DialogoPage.Rojo);
                return;
            }

            _email = email;
            _token = respuesta.Token;

            EmailPanel.IsVisible = false;
            CodePanel.IsVisible = true;
            StartCodeTimer();
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo enviar el código", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            SolicitarButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnVerifyClicked(object sender, EventArgs e)
    {
        var codigo = CodeEntry.Text?.Trim() ?? "";
        if (codigo.Length != 6 || !codigo.All(char.IsDigit))
        {
            await AvisoAsync("Código inválido", "Captura el código de 6 dígitos que te enviamos por correo electrónico.", nota: "Si no lo ves, revisa tu carpeta de correo no deseado.");
            return;
        }

        try
        {
            VerifyButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var valido = await _api.VerificarCodigoRecuperacionAsync(_email, _token, codigo);
            if (!valido)
            {
                await AvisoAsync("Código incorrecto", "El código es incorrecto o ya expiró.", nota: "Puedes solicitar uno nuevo con la opción de reenviar código.", colores: DialogoPage.Rojo);
                return;
            }

            StopCodeTimer();
            CodePanel.IsVisible = false;
            NewPasswordPanel.IsVisible = true;
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo verificar el código", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            VerifyButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnRestablecerClicked(object sender, EventArgs e)
    {
        var nueva = NewPasswordEntry.Text ?? "";
        var confirmar = ConfirmPasswordEntry.Text ?? "";

        if (nueva.Length < 6)
        {
            await AvisoAsync("Contraseña muy corta", "Tu nueva contraseña debe tener al menos 6 caracteres.");
            return;
        }

        if (nueva != confirmar)
        {
            await AvisoAsync("Las contraseñas no coinciden", "Verifica que hayas escrito la misma contraseña en ambos campos.");
            return;
        }

        try
        {
            RestablecerButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var error = await _api.RestablecerPasswordAsync(_email, _token, nueva);
            if (error is not null)
            {
                await AvisoAsync("No se pudo restablecer la contraseña", error, colores: DialogoPage.Rojo);
                return;
            }

            var (latitud, longitud) = await IntentarObtenerUbicacionAsync();
            var login = await _api.LoginAsync(_email, nueva, latitud, longitud);
            if (login is null)
            {
                // La contraseña si se cambio; solo no se pudo autenticar en automatico.
                await AvisoAsync("¡Contraseña actualizada!", "Tu contraseña se actualizó correctamente.", nota: "Inicia sesión con tu usuario y tu nueva contraseña.", icono: "✓", colores: DialogoPage.Verde, boton: "Ir a iniciar sesión");
                await Shell.Current.GoToAsync("..");
                return;
            }

            _preferences.JwtToken = login.Token;
            _preferences.CurrentUser = new UserProfile(login.User.FullName, login.User.Email, login.User.PhoneNumber ?? "");

            try
            {
                _preferences.CiudadanoId = await _api.GetMyCiudadanoAsync() ?? 0;
            }
            catch (Exception)
            {
                // No bloquea el acceso; ReportPage vuelve a intentarlo antes de crear un ticket.
            }

            await AvisoAsync("¡Contraseña actualizada!", "Tu contraseña se actualizó correctamente. ¡Bienvenido de nuevo!", icono: "✓", colores: DialogoPage.Verde, boton: "Continuar");
            await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo restablecer la contraseña", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            RestablecerButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnResendTapped(object sender, TappedEventArgs e)
    {
        if (!_canResend) return;

        try
        {
            var respuesta = await _api.SolicitarRecuperacionAsync(_email);
            if (respuesta is not null && !string.IsNullOrWhiteSpace(respuesta.Token))
            {
                _token = respuesta.Token;
            }
            StartCodeTimer();
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo reenviar el código", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
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

    private void OnToggleNewPasswordTapped(object sender, TappedEventArgs e)
    {
        NewPasswordEntry.IsPassword = !NewPasswordEntry.IsPassword;
        NewPasswordEyeLabel.TextColor = NewPasswordEntry.IsPassword
            ? Color.FromArgb("#BBBBBB")
            : Color.FromArgb("#F89A1C");
    }

    private void OnToggleConfirmPasswordTapped(object sender, TappedEventArgs e)
    {
        ConfirmPasswordEntry.IsPassword = !ConfirmPasswordEntry.IsPassword;
        ConfirmPasswordEyeLabel.TextColor = ConfirmPasswordEntry.IsPassword
            ? Color.FromArgb("#BBBBBB")
            : Color.FromArgb("#F89A1C");
    }

    private void OnModifyEmailTapped(object sender, TappedEventArgs e)
    {
        StopCodeTimer();
        CodePanel.IsVisible = false;
        EmailPanel.IsVisible = true;
    }

    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

    private async void OnBackToLoginTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private void StartCodeTimer()
    {
        _secondsLeft = 59;
        _timerActive = true;
        _canResend = false;
        TimerLabel.IsVisible = true;
        TimerLabel.Text = $"0:{_secondsLeft:D2}";
        ResendLabel.Opacity = 0.3;

        Dispatcher.StartTimer(TimeSpan.FromSeconds(1), () =>
        {
            if (!_timerActive) return false;

            _secondsLeft--;
            if (_secondsLeft <= 0)
            {
                TimerLabel.IsVisible = false;
                ResendLabel.Opacity = 1;
                _canResend = true;
                _timerActive = false;
                return false;
            }

            TimerLabel.Text = $"0:{_secondsLeft:D2}";
            return true;
        });
    }

    private void StopCodeTimer()
    {
        _timerActive = false;
    }
}
