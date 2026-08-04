using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class RegisterPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private string _phone = "";
    private int _secondsLeft;
    private bool _timerActive;
    private bool _canResend;

    public RegisterPage(PreferencesService preferences, MetepecApiService api)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var nombre = NombreEntry.Text?.Trim() ?? "";
        var apellido = ApellidoEntry.Text?.Trim() ?? "";
        var phone = PhoneEntry.Text?.Trim() ?? "";
        var email = EmailEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido))
        {
            await DisplayAlert("Datos incompletos", "Captura tu nombre y apellido paterno.", "Aceptar");
            return;
        }

        if (phone.Length != 10 || !phone.All(char.IsDigit))
        {
            await DisplayAlert("Telefono invalido", "Captura un numero a 10 digitos.", "Aceptar");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            await DisplayAlert("Correo invalido", "Captura un correo electronico valido.", "Aceptar");
            return;
        }

        _phone = phone;

        // NOTA: la verificacion por SMS (Twilio) queda deshabilitada temporalmente porque requiere
        // credenciales de Twilio que no deben vivir en la app movil (ver AppConstants.TwilioAccountSid).
        // Cuando se mueva la verificacion al backend, restaurar este flujo:
        //   await _api.SendTwilioCodeAsync(_phone);
        //   FormPanel.IsVisible = false;
        //   CodePanel.IsVisible = true;
        //   StartCodeTimer();
        // y que OnVerifyClicked llame a CompleteRegistrationAsync() tras validar el codigo.
        await CompleteRegistrationAsync();
    }

    private async Task CompleteRegistrationAsync()
    {
        try
        {
            RegisterButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var registro = await _api.RegisterAsync(new BackendRegisterRequest
            {
                Email = EmailEntry.Text?.Trim() ?? "",
                Nombre = NombreEntry.Text?.Trim() ?? "",
                Apaterno = ApellidoEntry.Text?.Trim() ?? "",
                Telefonomovil = _phone
            });

            var username = registro?.IdentityUser.Username ?? "";
            var password = registro?.GeneratedPassword ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                await DisplayAlert("Registro completado", "Tu cuenta fue creada. Inicia sesion desde la pantalla de acceso.", "Aceptar");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // El ciudadano ya no elige usuario/contrasena: se le muestran una sola vez aqui,
            // recien generados, para que los guarde antes de continuar.
            await DisplayAlert(
                "Registro completado",
                $"Guarda estos datos para iniciar sesion despues:\n\nUsuario: {username}\nContraseña: {password}",
                "Entendido");

            var (latitud, longitud) = await IntentarObtenerUbicacionAsync();
            var login = await _api.LoginAsync(username, password, latitud, longitud);
            if (login is null)
            {
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
                // No bloquea el registro; ReportPage vuelve a intentarlo antes de crear un ticket.
            }

            await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo completar el registro", ex.Message, "Aceptar");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
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

    private async void OnVerifyClicked(object sender, EventArgs e)
    {
        var code = CodeEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(code))
        {
            await DisplayAlert("Codigo requerido", "Captura el codigo que recibiste por SMS.", "Aceptar");
            return;
        }

        try
        {
            VerifyButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var valid = await _api.VerifyTwilioCodeAsync(_phone, code);
            if (!valid)
            {
                await DisplayAlert("Codigo incorrecto", "El codigo de verificacion es incorrecto.", "Aceptar");
                return;
            }

            await CompleteRegistrationAsync();
        }
        catch (Exception ex)
        {
            StopCodeTimer();
            CodePanel.IsVisible = false;
            FormPanel.IsVisible = true;
            await DisplayAlert("No se pudo completar el registro", ex.Message, "Aceptar");
        }
        finally
        {
            VerifyButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnResendTapped(object sender, TappedEventArgs e)
    {
        if (!_canResend)
        {
            return;
        }

        try
        {
            await _api.SendTwilioCodeAsync(_phone);
            StartCodeTimer();
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo reenviar el codigo", ex.Message, "Aceptar");
        }
    }

    private void OnModifyTapped(object sender, TappedEventArgs e)
    {
        StopCodeTimer();
        CodePanel.IsVisible = false;
        FormPanel.IsVisible = true;
    }

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
            if (!_timerActive)
            {
                return false;
            }

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
