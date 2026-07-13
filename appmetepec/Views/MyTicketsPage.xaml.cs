using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class MyTicketsPage : ContentPage
{
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private readonly PendingTicketsService _pendingTickets;
    private readonly PreferencesService _preferences;

    public MyTicketsPage(MetepecApiService api, NavigationState navigationState, PendingTicketsService pendingTickets, PreferencesService preferences)
    {
        InitializeComponent();
        _api = api;
        _navigationState = navigationState;
        _pendingTickets = pendingTickets;
        _preferences = preferences;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPendingAsync();
        if (TicketsView.ItemsSource is null)
        {
            await LoadTicketsAsync();
        }
    }

    private async Task LoadTicketsAsync()
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            TicketsView.ItemsSource = await _api.GetMyTicketsAsync();
        }
        catch (Exception ex)
        {
            TicketsView.ItemsSource = Array.Empty<BackendTicketDto>();
            await DisplayAlert("No se pudieron cargar tus reportes", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async Task LoadPendingAsync()
    {
        var pending = await _pendingTickets.GetAllAsync();
        PendingSection.IsVisible = pending.Count > 0;
        PendingView.ItemsSource = pending.OrderByDescending(item => item.SavedAt).ToList();
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        await LoadPendingAsync();
        await LoadTicketsAsync();
        TicketsRefreshView.IsRefreshing = false;
    }

    private async void OnRetryPendingClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PendingTicketSubmission pending })
        {
            return;
        }

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

            List<BackendEvidenciaItemRequest>? evidencias = null;
            if (!string.IsNullOrWhiteSpace(pending.LocalPhotoPath) && File.Exists(pending.LocalPhotoPath))
            {
                await using var stream = File.OpenRead(pending.LocalPhotoPath);
                var uploaded = await _api.UploadEvidenceAsync(stream, Path.GetFileName(pending.LocalPhotoPath), "image/jpeg");
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

            var (latitud, longitud) = ParseCoordinates(pending.Coordinates);
            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                Asunto = pending.Title,
                Descripcion = pending.Comments,
                Observacionesapp = pending.Comments,
                Correoelectronico = pending.Email,
                Numerotelefonico = pending.Phone,
                Dependencia = pending.Dependencia,
                Idservicio = pending.IdServicio,
                Ubicacion = new BackendTicketUbicacionRequest
                {
                    Direccionapp = pending.Address,
                    Coordenadas = pending.Coordinates,
                    Latitud = latitud,
                    Longitud = longitud
                },
                Evidencias = evidencias,
                Servicios = pending.IdServicio > 0
                    ? [new BackendTicketServicioItemRequest { IdServicio = pending.IdServicio, EsPrincipal = true }]
                    : null,
                Observacion = new BackendTicketObservacionRequest
                {
                    IdTipoMensaje = 1,
                    Observaciones = string.IsNullOrWhiteSpace(pending.Comments) ? pending.Title : pending.Comments,
                    VisibleCiudadano = true
                }
            };

            var ticketId = await _api.CreateTicketAsync(request);
            await _pendingTickets.RemoveAsync(pending.Id);
            await LoadPendingAsync();
            await LoadTicketsAsync();
            await Shell.Current.GoToAsync($"{nameof(ReportSuccessPage)}?reportId={ticketId}&isBache={pending.IsBache}");
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

    private async void OnDiscardPendingClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PendingTicketSubmission pending })
        {
            return;
        }

        var confirm = await DisplayAlert("Descartar reporte", $"Se eliminara \"{pending.Title}\" guardado localmente.", "Descartar", "Cancelar");
        if (!confirm)
        {
            return;
        }

        await _pendingTickets.RemoveAsync(pending.Id);
        await LoadPendingAsync();
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

    private async void OnTicketSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not BackendTicketDto ticket)
        {
            return;
        }

        ((CollectionView)sender).SelectedItem = null;
        _navigationState.SelectedTicket = ticket;
        await Shell.Current.GoToAsync(nameof(TicketDetailPage));
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
