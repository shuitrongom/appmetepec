using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class ReportPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private ScreenReport? _report;
    private FileResult? _attachment;
    private string _coordinates = "";

    public ReportPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _report = _navigationState.SelectedReport;
        if (_report is null)
        {
            Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = _report.Title;
        ReportIcon.Source = _report.IconSource;
        InstructionsLabel.Text = _report.Instructions;
        InstructionsLabel.IsVisible = !string.IsNullOrWhiteSpace(_report.Instructions);

        var user = _preferences.CurrentUser;
        NameEntry.Text = user.Name;
        PhoneEntry.Text = user.Phone;
        EmailEntry.Text = user.Email;
    }

    private async void OnUseLocationClicked(object sender, EventArgs e)
    {
        try
        {
            var location = await Geolocation.Default.GetLocationAsync(new GeolocationRequest(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(10)));
            if (location is null)
            {
                await DisplayAlert("Ubicacion", "No fue posible obtener la ubicacion.", "Aceptar");
                return;
            }

            _coordinates = $"{location.Latitude},{location.Longitude}";
            CoordinatesLabel.Text = _coordinates;
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ubicacion", ex.Message, "Aceptar");
        }
    }

    private async void OnPickImageClicked(object sender, EventArgs e)
    {
        _attachment = await MediaPicker.Default.PickPhotoAsync();
        AttachmentLabel.Text = _attachment?.FileName ?? "";
    }

    private async void OnPickImageTapped(object sender, TappedEventArgs e)
    {
        _attachment = await MediaPicker.Default.PickPhotoAsync();
        AttachmentLabel.Text = _attachment?.FileName ?? "";
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private async void OnSendClicked(object sender, EventArgs e)
    {
        if (_report is null)
        {
            return;
        }

        var name = NameEntry.Text?.Trim() ?? "";
        var phone = PhoneEntry.Text?.Trim() ?? "";
        var email = EmailEntry.Text?.Trim() ?? "";
        var address = AddressEditor.Text?.Trim() ?? "";
        var comments = CommentsEditor.Text?.Trim() ?? "";

        if (string.IsNullOrWhiteSpace(phone) || string.IsNullOrWhiteSpace(address))
        {
            await DisplayAlert("Campos obligatorios", "Captura telefono y direccion.", "Aceptar");
            return;
        }

        try
        {
            SendButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            _preferences.CurrentUser = new UserProfile(name, email, phone);

            if (_preferences.CiudadanoId == 0)
            {
                _preferences.CiudadanoId = await _api.GetMyCiudadanoAsync() ?? 0;
            }

            if (_preferences.CiudadanoId == 0)
            {
                await DisplayAlert("No se pudo identificar tu cuenta", "Vuelve a iniciar sesion e intenta de nuevo.", "Aceptar");
                return;
            }

            List<BackendEvidenciaItemRequest>? evidencias = null;
            if (_attachment is not null)
            {
                var uploaded = await _api.UploadEvidenceAsync(_attachment);
                if (uploaded is not null)
                {
                    evidencias =
                    [
                        new BackendEvidenciaItemRequest
                        {
                            NombreArchivo = uploaded.NombreOriginal,
                            RutaArchivo = uploaded.Ruta,
                            TipoMime = uploaded.MimeType,
                            TamanoBytes = uploaded.Peso,
                            EsEvidenciaInicial = true
                        }
                    ];
                }
            }

            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                Asunto = _report.Title,
                Descripcion = comments,
                Observacionesapp = comments,
                Correoelectronico = email,
                Numerotelefonico = phone,
                Dependencia = _report.Dependencia.DisplayName(),
                Idservicio = _report.IdServicio,
                Ubicacion = new BackendTicketUbicacionRequest
                {
                    Direccionapp = address,
                    Coordenadas = _coordinates
                },
                Evidencias = evidencias
            };

            var ticketId = await _api.CreateTicketAsync(request);
            await Shell.Current.GoToAsync($"{nameof(ReportSuccessPage)}?reportId={ticketId}&isBache={_report.Title.Contains("bache", StringComparison.OrdinalIgnoreCase)}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo enviar", ex.Message, "Aceptar");
        }
        finally
        {
            SendButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }
}
