using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class RecoleccionPage : ContentPage
{
    private readonly MetepecApiService _api;
    private List<RoutesModel> _routes = [];

    public RecoleccionPage(MetepecApiService api)
    {
        InitializeComponent();
        _api = api;
    }

    private async void OnFindRoutesClicked(object sender, EventArgs e)
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(12)))
                ?? new Location(19.263099, -99.576661);

            LocationLabel.Text = $"{location.Latitude}, {location.Longitude}";
            _routes = await _api.GetRoutesByLocationAsync(location.Latitude, location.Longitude);
            RoutesPicker.ItemsSource = _routes;

            if (_routes.Count == 0)
            {
                await DisplayAlert("Rutas", "No se encontraron rutas para la ubicacion.", "Aceptar");
            }
            else if (_routes.Count == 1)
            {
                RoutesPicker.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Rutas", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnRouteSelected(object sender, EventArgs e)
    {
        if (RoutesPicker.SelectedItem is not RoutesModel route)
        {
            return;
        }

        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            var routeInfo = await _api.GetRouteByIdAsync(route.id);
            var truck = await _api.GetTruckLocationAsync(route.id);

            RouteLabel.Text = routeInfo?.route?.name ?? route.name;
            TruckLabel.Text = truck is null
                ? "No se encontro informacion del camion."
                : $"Camion: {truck.name}\nUbicacion: {truck.latitude}, {truck.longitude}";
            RouteCoordinatesLabel.Text = routeInfo?.route?.coordinates ?? "";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ruta", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }
}
