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
                await AvisoAsync("Sin rutas en tu zona", "No encontramos rutas de recolección para tu ubicación.", nota: "Verifica que el GPS esté activado e intenta de nuevo.", icono: "?");
            }
            else if (_routes.Count == 1)
            {
                RoutesPicker.SelectedIndex = 0;
            }
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se pudieron consultar las rutas", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

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
            await AvisoAsync("No se pudo consultar la ruta", ErrorMessageHelper.Traducir(ex), nota: "Revisa tu conexión a internet e intenta de nuevo.");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }
}
