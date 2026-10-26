using appmetepec.Services;

namespace appmetepec.Views;

// Primer paso del boton de panico (general, no especifico de genero -- ver PanicoConfirmarPage
// para la segunda confirmacion antes de detonar). Mismo patron de captura de datos que ya usaba
// AlertaNaranjaPage, pero aqui solo se recopila info; el ticket y la llamada se disparan hasta
// la pantalla de confirmacion.
public partial class PanicoPage : ContentPage
{
    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

    private readonly PreferencesService _preferences;
    private readonly NavigationState _navigationState;
    private readonly GeocodingService _geocoding;
    private string _coordinates = "";

    public PanicoPage(PreferencesService preferences, NavigationState navigationState, GeocodingService geocoding)
    {
        InitializeComponent();
        _preferences = preferences;
        _navigationState = navigationState;
        _geocoding = geocoding;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var user = _preferences.CurrentUser;
        NameEntry.Text = user.Name;
        PhoneEntry.Text = user.Phone;
        EmailEntry.Text = user.Email;
        _coordinates = "";
        CoordinatesLabel.Text = "";
    }

    private async void OnUseLocationClicked(object sender, EventArgs e)
    {
        try
        {
            CoordinatesLabel.Text = "Obteniendo ubicación...";
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10)));
            if (location is null)
            {
                CoordinatesLabel.Text = "";
                return;
            }

            _coordinates = $"{location.Latitude},{location.Longitude}";
            CoordinatesLabel.Text = _coordinates;

            // Solo precarga el campo Direccion; CoordinatesLabel se queda mostrando la coordenada.
            var direccion = await _geocoding.ReverseGeocodeAsync(location.Latitude, location.Longitude);
            if (!string.IsNullOrWhiteSpace(direccion))
            {
                AddressEditor.Text = direccion;
            }
        }
        catch (Exception ex)
        {
            await AvisoAsync("No se obtuvo tu ubicación", ErrorMessageHelper.Traducir(ex), nota: "Verifica que el GPS esté activado y que la app tenga permiso de ubicación.");
        }
    }

    private async void OnContinuarClicked(object sender, EventArgs e)
    {
        var phone = PhoneEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(phone))
        {
            await AvisoAsync("Falta tu teléfono", "Captura un teléfono de contacto antes de continuar.", nota: "Lo necesitamos para comunicarnos contigo durante la emergencia.");
            return;
        }

        _navigationState.PanicoDatos = new PanicoDatos(
            NameEntry.Text?.Trim() ?? "",
            phone,
            EmailEntry.Text?.Trim() ?? "",
            AddressEditor.Text?.Trim() ?? "",
            _coordinates);

        await Shell.Current.GoToAsync(nameof(PanicoConfirmarPage));
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
