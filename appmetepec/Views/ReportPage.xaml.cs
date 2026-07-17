using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class ReportPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private readonly PendingTicketsService _pendingTickets;
    private ScreenReport? _report;
    private FileResult? _attachment;
    private string _coordinates = "";

    public ReportPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState, PendingTicketsService pendingTickets)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
        _pendingTickets = pendingTickets;
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

    private async void OnPickImageTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var options = MediaPicker.Default.IsCaptureSupported
                ? new[] { "Tomar foto", "Elegir de la galeria" }
                : new[] { "Elegir de la galeria" };

            var choice = await DisplayActionSheet("Agrega un testigo", "Cancelar", null, options);

            FileResult? result = choice switch
            {
                "Tomar foto" => await MediaPicker.Default.CapturePhotoAsync(),
                "Elegir de la galeria" => await MediaPicker.Default.PickPhotoAsync(),
                _ => null
            };

            if (result is null)
            {
                return;
            }

            _attachment = result;
            AttachmentLabel.Text = _attachment.FileName;
            PreviewImage.Source = ImageSource.FromFile(_attachment.FullPath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo agregar el testigo", ex.Message, "Aceptar");
        }
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

            var idPrioridad = 2; // Normal: fallback si falla la consulta del catalogo
            try
            {
                var prioridades = await _api.GetPrioridadesAsync();
                var porDefecto = prioridades.FirstOrDefault(p => p.EsDefault);
                if (porDefecto is not null)
                {
                    idPrioridad = porDefecto.Id;
                }
            }
            catch
            {
                // Sin bloquear el envio del reporte si el catalogo de prioridades no responde.
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
                // Sin bloquear el envio del reporte si el catalogo de canal de ingreso no responde.
            }

            var (latitud, longitud) = ParseCoordinates(_coordinates);
            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                Idprioridad = idPrioridad,
                IdCanalIngreso = idCanalIngreso,
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
                    Coordenadas = _coordinates,
                    Latitud = latitud,
                    Longitud = longitud
                },
                Evidencias = evidencias,
                Servicios = _report.IdServicio > 0
                    ? [new BackendTicketServicioItemRequest { IdServicio = _report.IdServicio, EsPrincipal = true }]
                    : null,
                Observacion = new BackendTicketObservacionRequest
                {
                    IdTipoMensaje = 1,
                    Observaciones = string.IsNullOrWhiteSpace(comments) ? _report.Title : comments,
                    VisibleCiudadano = true
                }
            };

            var ticketId = await _api.CreateTicketAsync(request);
            await Shell.Current.GoToAsync($"{nameof(ReportSuccessPage)}?reportId={ticketId}&isBache={_report.Title.Contains("bache", StringComparison.OrdinalIgnoreCase)}");
        }
        catch (Exception ex)
        {
            var guardar = await DisplayAlert(
                "No se pudo enviar",
                $"{ex.Message}\n\n¿Quieres guardar este reporte para volver a intentarlo despues?",
                "Guardar",
                "Cancelar");

            if (guardar)
            {
                await GuardarReportePendienteAsync(name, email, phone, address, comments, ex.Message);
            }
        }
        finally
        {
            SendButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async Task GuardarReportePendienteAsync(string name, string email, string phone, string address, string comments, string lastError)
    {
        if (_report is null)
        {
            return;
        }

        var submission = new PendingTicketSubmission
        {
            Title = _report.Title,
            IdServicio = _report.IdServicio,
            Dependencia = _report.Dependencia.DisplayName(),
            Name = name,
            Email = email,
            Phone = phone,
            Address = address,
            Coordinates = _coordinates,
            Comments = comments,
            IsBache = _report.Title.Contains("bache", StringComparison.OrdinalIgnoreCase),
            LastError = lastError
        };

        if (_attachment is not null)
        {
            submission.LocalPhotoPath = await _pendingTickets.SavePhotoAsync(_attachment);
        }

        await _pendingTickets.SaveAsync(submission);
        await DisplayAlert(
            "Reporte guardado",
            "Lo encontraras en \"Mis reportes\" para reintentar el envio cuando tengas conexion.",
            "Aceptar");
        await Shell.Current.GoToAsync("..");
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
