using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class AlertaNaranjaPage : ContentPage
{
    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private string _coordinates = "";

    public AlertaNaranjaPage(PreferencesService preferences, MetepecApiService api)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var user = _preferences.CurrentUser;
        NameEntry.Text = string.IsNullOrWhiteSpace(_preferences.NaranjaName) ? user.Name : _preferences.NaranjaName;
        PhoneEntry.Text = user.Phone;
        EmailEntry.Text = user.Email;
    }

    private async void OnUseLocationClicked(object sender, EventArgs e)
    {
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (location is not null)
            {
                _coordinates = $"{location.Latitude},{location.Longitude}";
                CoordinatesLabel.Text = _coordinates;
            }
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se obtuvo tu ubicación", ErrorMessageHelper.Traducir(ex), nota: "Verifica que el GPS esté activado y que la app tenga permiso de ubicación.");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _preferences.NaranjaName = NameEntry.Text?.Trim() ?? "";
        _preferences.CurrentUser = new UserProfile(_preferences.NaranjaName, EmailEntry.Text?.Trim() ?? "", PhoneEntry.Text?.Trim() ?? "");
        await AvisoAsync("¡Datos guardados!", "Tus datos de contacto para la Alerta Naranja se guardaron correctamente.", icono: "✓", colores: DialogoPage.Verde);
    }

    private async void OnActivateClicked(object sender, EventArgs e)
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            if (_preferences.CiudadanoId == 0)
            {
                _preferences.CiudadanoId = await _api.GetMyCiudadanoAsync() ?? 0;
            }

            if (_preferences.CiudadanoId == 0)
            {
                await AvisoAsync("No se pudo identificar tu cuenta", "Vuelve a iniciar sesión e intenta de nuevo.", colores: DialogoPage.Rojo);
                return;
            }

            var idCanalIngreso = 1; // APP: fallback si falla la consulta del catalogo
            try
            {
                var canalIngreso = await _api.GetCanalIngresoByClaveAsync(AppConstants.ClaveCanalIngresoApp);
                if (canalIngreso is not null)
                {
                    idCanalIngreso = canalIngreso.Id;
                }
            }
            catch
            {
                // Sin bloquear el envio de la alerta si el catalogo de canal de ingreso no responde.
            }

            var (latitud, longitud) = ParseCoordinates(_coordinates);
            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                IdCanalIngreso = idCanalIngreso,
                Asunto = "ALERTA DE GENERO",
                Descripcion = "Alerta naranja activada desde la app.",
                Correoelectronico = EmailEntry.Text?.Trim() ?? "",
                Numerotelefonico = PhoneEntry.Text?.Trim() ?? "",
                Dependencia = ZendeskDependencia.GerenciaCiudad.DisplayName(),
                Idservicio = 36,
                Ubicacion = new BackendTicketUbicacionRequest
                {
                    Direccion = AddressEditor.Text?.Trim() ?? "",
                    Coordenadas = _coordinates,
                    Latitud = latitud,
                    Longitud = longitud
                },
                Servicios = [new BackendTicketServicioItemRequest { IdServicio = 36, EsPrincipal = true }],
                Observacion = new BackendTicketObservacionRequest
                {
                    IdTipoMensaje = 1,
                    // El mensaje inicial del hilo muestra el nombre del servicio/reporte (igual
                    // que Zendesk); el texto libre va aparte, en Descripcion.
                    Observaciones = "ALERTA DE GENERO",
                    VisibleCiudadano = true
                }
            };

            var ticketId = await _api.CreateTicketAsync(request);
            await DialogoPage.MostrarAsync(
                Navigation,
                titulo: "Alerta enviada",
                mensaje: "Las autoridades locales se pondrán en contacto contigo a la brevedad. Tu folio es:",
                destacado: $"{ticketId}",
                botonPrincipal: "Entendido",
                icono: "✓",
                colores: DialogoPage.Verde);
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudo enviar la alerta", ErrorMessageHelper.Traducir(ex), nota: "Si es una emergencia, marca directamente al 911.", colores: DialogoPage.Rojo);
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private static (decimal? Latitud, decimal? Longitud) ParseCoordinates(string coordinates)
    {
        var parts = coordinates.Split(',');
        if (parts.Length == 2
            && decimal.TryParse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var lat)
            && decimal.TryParse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            return (lat, lng);
        }

        return (null, null);
    }
}
