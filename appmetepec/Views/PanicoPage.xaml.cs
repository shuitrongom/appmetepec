using appmetepec.Services;

namespace appmetepec.Views;

// Primer paso del boton de panico (general, no especifico de genero -- ver PanicoConfirmarPage
// para la segunda confirmacion antes de detonar). Mismo patron de captura de datos que ya usaba
// AlertaNaranjaPage, pero aqui solo se recopila info; el ticket y la llamada se disparan hasta
// la pantalla de confirmacion.
public partial class PanicoPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly NavigationState _navigationState;
    private string _coordinates = "";

    public PanicoPage(PreferencesService preferences, NavigationState navigationState)
    {
        InitializeComponent();
        _preferences = preferences;
        _navigationState = navigationState;
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
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Best, TimeSpan.FromSeconds(10)));
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

    private async void OnContinuarClicked(object sender, EventArgs e)
    {
        var phone = PhoneEntry.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(phone))
        {
            await DisplayAlert("Dato requerido", "Captura un telefono de contacto antes de continuar.", "Aceptar");
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
