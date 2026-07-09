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
            await DisplayAlert("Ubicacion", ex.Message, "Aceptar");
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

            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                Asunto = "ALERTA DE GENERO",
                Descripcion = "Alerta naranja activada desde la app.",
                Observacionesapp = "Alerta naranja activada desde la app.",
                Correoelectronico = EmailEntry.Text?.Trim() ?? "",
                Numerotelefonico = PhoneEntry.Text?.Trim() ?? "",
                Dependencia = ZendeskDependencia.GerenciaCiudad.DisplayName(),
                Idservicio = 36,
                Ubicacion = new BackendTicketUbicacionRequest
                {
                    Direccionapp = AddressEditor.Text?.Trim() ?? "",
                    Coordenadas = _coordinates
                }
            };

            var ticketId = await _api.CreateTicketAsync(request);
            await DisplayAlert("Alerta enviada", $"Las autoridades locales se pondran en contacto a la brevedad.\nFolio: {ticketId}", "Aceptar");
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo enviar", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }
}
