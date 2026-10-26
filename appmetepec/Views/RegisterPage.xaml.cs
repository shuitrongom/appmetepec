using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class RegisterPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private string _phone = "";
    private string _email = "";
    private string _emailVerificationToken = "";
    private int _secondsLeft;
    private bool _timerActive;
    private bool _canResend;

    public RegisterPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
    }

    private async void OnRegisterClicked(object sender, EventArgs e)
    {
        var nombre = NombreEntry.Text?.Trim() ?? "";
        var apellido = ApellidoEntry.Text?.Trim() ?? "";
        var phone = PhoneEntry.Text?.Trim() ?? "";
        var email = EmailEntry.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(apellido))
        {
            await AvisoAsync("Datos incompletos", "Captura tu nombre y tu apellido paterno para continuar.");
            return;
        }

        // Evita registros como una sola letra (ej. "V"): un nombre/apellido real tiene al menos
        // 2 caracteres.
        if (nombre.Length < 2 || apellido.Length < 2)
        {
            await AvisoAsync("Datos incompletos", "Tu nombre y tu apellido paterno deben tener al menos 2 letras.");
            return;
        }

        if (phone.Length != 10 || !phone.All(char.IsDigit))
        {
            await DialogoPage.MostrarAsync(
                Navigation,
                titulo: "Teléfono inválido",
                mensaje: "Captura tu número de celular a 10 dígitos, sin espacios ni guiones.",
                nota: "Ejemplo: 7221234567",
                botonPrincipal: "Entendido",
                icono: "!");
            return;
        }

        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@') || !email.Contains('.'))
        {
            await AvisoAsync("Correo inválido", "Captura un correo electrónico válido. Ahí te enviaremos tu código de verificación.", nota: "Ejemplo: nombre@correo.com");
            return;
        }

        _phone = phone;
        _email = email;

        try
        {
            RegisterButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var solicitud = await _api.SolicitarVerificacionEmailAsync(_email);
            if (solicitud?.Bloqueado == true)
            {
                await AvisoAsync("No se pudo enviar el código", solicitud.Mensaje, colores: DialogoPage.Rojo);
                return;
            }

            _emailVerificationToken = solicitud?.Token ?? "";
            FormPanel.IsVisible = false;
            CodePanel.IsVisible = true;
            StartCodeTimer();
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo enviar el código", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            RegisterButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async Task CompleteRegistrationAsync(bool confirmarVinculacion = false)
    {
        try
        {
            RegisterButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var registro = await _api.RegisterAsync(new BackendRegisterRequest
            {
                Email = EmailEntry.Text?.Trim() ?? "",
                Token = _emailVerificationToken,
                Nombre = NombreEntry.Text?.Trim() ?? "",
                Apaterno = ApellidoEntry.Text?.Trim() ?? "",
                Telefonomovil = _phone,
                ConfirmarVinculacion = confirmarVinculacion
            });

            var username = registro?.IdentityUser.Username ?? "";
            var password = registro?.GeneratedPassword ?? "";

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                await AvisoAsync("¡Registro completado!", "Tu cuenta fue creada correctamente. Inicia sesión desde la pantalla de acceso.", icono: "✓", colores: DialogoPage.Verde, boton: "Ir a iniciar sesión");
                await Shell.Current.GoToAsync("..");
                return;
            }

            // El ciudadano ya no elige (ni necesita ver) su usuario/contrasena: se autentica en
            // automatico con lo que genero el backend. Si alguna vez los olvida, los recupera por
            // correo desde LoginPage (ver OfrecerRecuperacionAsync).
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
        catch (VinculacionRequeridaException ex)
        {
            // Ya existe un ciudadano dado de alta en la web con este correo (sin cuenta de la app).
            // Nunca se vincula sin preguntar: si es el, se reintenta vinculando (conserva sus
            // reportes); si no, debe registrarse con otro correo.
            var esElCiudadano = await DialogoPage.MostrarAsync(
                Navigation,
                titulo: "Encontramos un registro",
                mensaje: "Este correo electrónico ya está asociado a un registro en la Gerencia de la Ciudad a nombre de:",
                destacado: ex.NombreEnmascarado,
                nota: "Si eres tú, vincularemos tu cuenta a este registro y podrás consultar los reportes que ya tienes.",
                botonPrincipal: "Sí, soy yo",
                botonSecundario: "No, no soy yo",
                icono: "?");

            if (esElCiudadano)
            {
                await CompleteRegistrationAsync(confirmarVinculacion: true);
                return;
            }

            StopCodeTimer();
            CodePanel.IsVisible = false;
            FormPanel.IsVisible = true;
            await DialogoPage.MostrarAsync(
                Navigation,
                titulo: "Usa otro correo",
                mensaje: "Este correo electrónico pertenece a otro ciudadano. Para continuar con tu registro, captura un correo diferente.",
                botonPrincipal: "Entendido",
                icono: "!",
                colores: DialogoPage.Rojo);
        }
        catch (Exception ex)
        {
            // Heuristica sobre el mensaje del backend (identityResult.Errors de ASP.NET Identity,
            // sin codigo de error estructurado que distinguir): si menciona correo/email, es casi
            // seguro DuplicateEmail. Se ofrece el flujo de recuperacion en vez de solo tronar.
            var esCorreoDuplicado = ex.Message.Contains("correo", StringComparison.OrdinalIgnoreCase)
                || ex.Message.Contains("email", StringComparison.OrdinalIgnoreCase);

            if (esCorreoDuplicado)
            {
                var irARecuperar = await DialogoPage.MostrarAsync(
                    Navigation,
                    titulo: "El correo ya está registrado",
                    mensaje: "Ya existe una cuenta con este correo electrónico:",
                    destacado: EmailEntry.Text?.Trim(),
                    nota: "Si es tuya y no recuerdas tu usuario o contraseña, puedes recuperar tu cuenta.",
                    botonPrincipal: "Recuperar cuenta",
                    botonSecundario: "Cancelar",
                    icono: "?");

                if (irARecuperar)
                {
                    _navigationState.RecoverAccountEmail = EmailEntry.Text?.Trim();
                    await Shell.Current.GoToAsync(nameof(RecoverAccountPage));
                }
            }
            else
            {
                await AvisoAsync("No se pudo completar el registro", ErrorMessageHelper.Traducir(ex), colores: DialogoPage.Rojo);
            }
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
            await AvisoAsync("Código requerido", "Captura el código de verificación que te enviamos por correo electrónico.", nota: "Si no lo ves, revisa tu carpeta de correo no deseado.");
            return;
        }

        try
        {
            VerifyButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            var valid = await _api.VerificarCodigoEmailAsync(_email, _emailVerificationToken, code);
            if (!valid)
            {
                await AvisoAsync("Código incorrecto", "El código de verificación no coincide. Revisa el correo que te enviamos e intenta de nuevo.", colores: DialogoPage.Rojo);
                return;
            }

            await CompleteRegistrationAsync();
        }
        catch (Exception ex)
        {
            StopCodeTimer();
            CodePanel.IsVisible = false;
            FormPanel.IsVisible = true;
            await AvisoAsync("No se pudo completar el registro", ErrorMessageHelper.Traducir(ex), colores: DialogoPage.Rojo);
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
            var solicitud = await _api.SolicitarVerificacionEmailAsync(_email);
            if (solicitud?.Bloqueado == true)
            {
                await AvisoAsync("No se pudo reenviar el código", solicitud.Mensaje, colores: DialogoPage.Rojo);
                return;
            }

            _emailVerificationToken = solicitud?.Token ?? "";
            StartCodeTimer();
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo reenviar el código", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
    }

    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

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
