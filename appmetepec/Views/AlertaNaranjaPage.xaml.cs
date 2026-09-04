using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class AlertaNaranjaPage : ContentPage
{
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
            await DisplayAlert("Ubicacion", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        _preferences.NaranjaName = NameEntry.Text?.Trim() ?? "";
        _preferences.CurrentUser = new UserProfile(_preferences.NaranjaName, EmailEntry.Text?.Trim() ?? "", PhoneEntry.Text?.Trim() ?? "");
        await DisplayAlert("Alerta", "Datos guardados.", "Aceptar");
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
                await DisplayAlert("No se pudo identificar tu cuenta", "Vuelve a iniciar sesion e intenta de nuevo.", "Aceptar");
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
            await DisplayAlert("Alerta enviada", $"Las autoridades locales se pondran en contacto a la brevedad.\nFolio: {ticketId}", "Aceptar");
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo enviar", ErrorMessageHelper.Traducir(ex), "Aceptar");
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
