using System.Globalization;
using System.Text.Json;
using appmetepec.Services;

namespace appmetepec.Views;

public sealed record MapLocation(double Latitud, double Longitud, string Direccion);

// Selector de ubicacion con Leaflet dentro de un HybridWebView (Resources/Raw/mapa/index.html),
// equivalente al map-picker de la web. Se abre modal desde ReportPage con PickAsync y regresa
// null si el ciudadano cancela.
public partial class MapPickerPage : ContentPage
{
    private const int ZoomConMarcador = 17;

    private readonly GeocodingService _geocoding;
    private readonly (double Lat, double Lng)? _inicial;
    private readonly TaskCompletionSource<MapLocation?> _resultado = new();

    private double? _lat;
    private double? _lng;
    private string _direccion = "";
    // Descarta respuestas de Nominatim que llegan despues de que el ciudadano ya movio el marcador.
    private int _consultaActual;

    private MapPickerPage(GeocodingService geocoding, (double Lat, double Lng)? inicial)
    {
        InitializeComponent();
        _geocoding = geocoding;
        _inicial = inicial;
    }

    public static async Task<MapLocation?> PickAsync(INavigation navigation, GeocodingService geocoding, (double Lat, double Lng)? inicial)
    {
        var page = new MapPickerPage(geocoding, inicial);
        await navigation.PushModalAsync(page);
        return await page._resultado.Task;
    }

    // En Android este evento llega desde el hilo del puente JavaScript del WebView, no desde el
    // hilo principal; tocar la UI (AddressLabel, AcceptButton) o llamar EvaluateJavaScriptAsync
    // desde ahi lanza excepcion. Por eso todo el manejo se pasa al hilo principal.
    private void OnMapMessageReceived(object? sender, HybridWebViewRawMessageReceivedEventArgs e)
    {
        var mensaje = e.Message;
        if (string.IsNullOrEmpty(mensaje)) return;

        MainThread.BeginInvokeOnMainThread(async () =>
        {
            try
            {
                using var doc = JsonDocument.Parse(mensaje);
                var root = doc.RootElement;
                switch (root.GetProperty("tipo").GetString())
                {
                    case "listo":
                        await CentrarAlIniciarAsync();
                        break;
                    case "seleccion":
                        await SeleccionarAsync(root.GetProperty("lat").GetDouble(), root.GetProperty("lng").GetDouble(), null);
                        break;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MapPicker] Error al procesar mensaje del mapa: {ex}");
            }
        });
    }

    // Si el reporte ya traia coordenadas se parte de ahi; si no, de la ultima ubicacion conocida
    // del GPS (sin pedir una lectura nueva para no hacer esperar); si tampoco hay, el mapa se queda
    // en el centro de Metepec que trae index.html.
    private async Task CentrarAlIniciarAsync()
    {
        if (_inicial is { } inicial)
        {
            await CentrarAsync(inicial.Lat, inicial.Lng, conMarcador: true);
            await SeleccionarAsync(inicial.Lat, inicial.Lng, null);
            return;
        }

        try
        {
            var ultima = await Geolocation.Default.GetLastKnownLocationAsync();
            if (ultima is not null)
            {
                await CentrarAsync(ultima.Latitude, ultima.Longitude, conMarcador: false);
            }
        }
        catch
        {
            // Sin permiso de ubicacion o GPS apagado: se queda el centro por defecto.
        }
    }

    private Task CentrarAsync(double lat, double lng, bool conMarcador) =>
        MapView.EvaluateJavaScriptAsync(string.Create(CultureInfo.InvariantCulture,
            $"centrar({lat}, {lng}, {ZoomConMarcador}, {(conMarcador ? "true" : "false")})"));

    // direccionConocida != null cuando ya viene de la busqueda (no hace falta geocodificar otra vez).
    private async Task SeleccionarAsync(double lat, double lng, string? direccionConocida)
    {
        _lat = lat;
        _lng = lng;
        AcceptButton.IsEnabled = true;

        if (direccionConocida is not null)
        {
            _consultaActual++;
            MostrarDireccion(direccionConocida, lat, lng);
            return;
        }

        var consulta = ++_consultaActual;
        _direccion = "";
        AddressLabel.Text = "Obteniendo direccion...";
        MostrarOcupado(true);

        var direccion = await _geocoding.ReverseGeocodeAsync(lat, lng);
        if (consulta != _consultaActual) return;

        MostrarOcupado(false);
        MostrarDireccion(direccion, lat, lng);
    }

    private void MostrarDireccion(string direccion, double lat, double lng)
    {
        _direccion = direccion;
        AddressLabel.Text = string.IsNullOrWhiteSpace(direccion)
            ? string.Create(CultureInfo.InvariantCulture, $"{lat:F6}, {lng:F6}")
            : direccion;
    }

    private void MostrarOcupado(bool ocupado)
    {
        BusyIndicator.IsVisible = ocupado;
        BusyIndicator.IsRunning = ocupado;
    }

    private async void OnSearchClicked(object? sender, EventArgs e)
    {
        var q = SearchEntry.Text?.Trim();
        if (string.IsNullOrEmpty(q)) return;

        SearchEntry.Unfocus();
        SearchButton.IsEnabled = false;
        MostrarOcupado(true);
        try
        {
            var resultado = await _geocoding.SearchAddressAsync(q);
            if (resultado is null)
            {
                await DisplayAlert("Buscar direccion", "No se encontro la direccion. Intenta con otra o toca el mapa.", "Aceptar");
                return;
            }

            var (lat, lng, direccion) = resultado.Value;
            await CentrarAsync(lat, lng, conMarcador: true);
            await SeleccionarAsync(lat, lng, direccion);
        }
        finally
        {
            MostrarOcupado(false);
            SearchButton.IsEnabled = true;
        }
    }

    private async void OnAcceptClicked(object? sender, EventArgs e)
    {
        if (_lat is null || _lng is null) return;
        await CerrarAsync(new MapLocation(_lat.Value, _lng.Value, _direccion));
    }

    private async void OnCancelClicked(object? sender, EventArgs e) => await CerrarAsync(null);

    // Boton "atras" de Android: igual que Cancelar.
    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync(null);
        return true;
    }

    private async Task CerrarAsync(MapLocation? resultado)
    {
        if (!_resultado.TrySetResult(resultado)) return;
        await Navigation.PopModalAsync();
    }
}
