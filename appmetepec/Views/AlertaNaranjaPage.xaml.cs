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
        var report = new ScreenReport("ALERTA DE GENERO", "", true, true, false, false, ZendeskDependencia.GerenciaCiudad);
        var submission = new ReportSubmission(
            report,
            NameEntry.Text?.Trim() ?? "",
            EmailEntry.Text?.Trim() ?? "",
            PhoneEntry.Text?.Trim() ?? "",
            AddressEditor.Text?.Trim() ?? "",
            _coordinates,
            "Alerta naranja activada desde la app MAUI.",
            null);

        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            var id = await _api.CreateZendeskReportAsync(submission);
            await DisplayAlert("Alerta enviada", $"Las autoridades locales se pondran en contacto a la brevedad.\nFolio: {id}", "Aceptar");
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
