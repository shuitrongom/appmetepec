using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class MyTicketsPage : ContentPage
{
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private readonly PendingTicketsService _pendingTickets;
    private readonly PreferencesService _preferences;
    private bool _enviandoPendiente;
    private bool _cargandoTickets;
    private bool _abriendoDetalle;

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
        // La peticion de tickets (red) arranca de inmediato, sin esperar a leer el archivo local
        // de pendientes.
        var cargaTickets = TicketsView.ItemsSource is null ? LoadTicketsAsync() : Task.CompletedTask;
        await LoadPendingAsync();
        await cargaTickets;
    }

    // mostrarIndicador = false en el "jalar para refrescar": el RefreshView ya muestra su propio
    // spinner (antes salian los dos a la vez). _cargandoTickets evita pedir la lista dos veces si
    // se refresca mientras todavia carga la primera.
    private async Task LoadTicketsAsync(bool mostrarIndicador = true)
    {
        if (_cargandoTickets)
        {
            return;
        }

        _cargandoTickets = true;
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = mostrarIndicador;
            var tickets = await _api.GetMyTicketsAsync();
            TicketsView.ItemsSource = tickets;
            ReportsCountLabel.Text = $"{tickets.Count} reporte{(tickets.Count == 1 ? "" : "s")}";
        }
        catch (Exception ex)
        {
            TicketsView.ItemsSource = Array.Empty<BackendTicketDto>();
            ReportsCountLabel.Text = string.Empty;
            await DisplayAlert("No se pudieron cargar tus reportes", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
            _cargandoTickets = false;
        }
    }

    private async Task LoadPendingAsync()
    {
        // Solo los reportes pendientes de la cuenta en sesion (la cola es compartida por dispositivo).
        var idCiudadano = _preferences.CiudadanoId;
        var pending = (await _pendingTickets.GetAllAsync())
            .Where(item => idCiudadano > 0 && item.IdCiudadano == idCiudadano)
            .ToList();
        PendingSection.IsVisible = pending.Count > 0;
        BindableLayout.SetItemsSource(PendingView, pending.OrderByDescending(item => item.SavedAt).ToList());
    }

    private async void OnRefreshing(object sender, EventArgs e)
    {
        try
        {
            await LoadPendingAsync();
            await LoadTicketsAsync(mostrarIndicador: false);
        }
        finally
        {
            TicketsRefreshView.IsRefreshing = false;
        }
    }

    private async void OnRetryPendingClicked(object sender, EventArgs e)
    {
        if (sender is not Button { BindingContext: PendingTicketSubmission pending } boton)
        {
            return;
        }

        // Evita crear el mismo ticket varias veces por toques repetidos mientras se envia.
        if (_enviandoPendiente)
        {
            return;
        }

        _enviandoPendiente = true;
        boton.IsEnabled = false;
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

            // Nunca enviar a nombre de la cuenta actual un reporte guardado por otra.
            if (pending.IdCiudadano != _preferences.CiudadanoId)
            {
                await LoadPendingAsync();
                return;
            }

            // Solo una evidencia por reporte pendiente: ReportPage.xaml.cs permite agregar varias
            // (fotos y/o video) al enviar en linea, pero el respaldo offline solo guarda la
            // primera (ver comentario en GuardarReportePendienteAsync alla). Limitacion conocida.
            List<BackendEvidenciaItemRequest>? evidencias = null;
            if (!string.IsNullOrWhiteSpace(pending.LocalPhotoPath) && File.Exists(pending.LocalPhotoPath))
            {
                await using var stream = File.OpenRead(pending.LocalPhotoPath);
                var uploaded = await _api.UploadEvidenceAsync(stream, Path.GetFileName(pending.LocalPhotoPath), pending.PhotoMimeType ?? "image/jpeg");
                if (uploaded is not null)
                {
                    evidencias =
                    [
                        new BackendEvidenciaItemRequest
                        {
                            // Ver mismo comentario en ReportPage.xaml.cs: nombre amigable, no el
                            // que le puso la camara/galeria del dispositivo.
                            NombreArchivo = "Evidencia" + Path.GetExtension(uploaded.NombreOriginal),
                            RutaArchivo = uploaded.Ruta,
                            TipoMime = uploaded.MimeType,
                            TamanoBytes = uploaded.Peso,
                            EsEvidenciaInicial = true,
                            Descripcion = pending.PhotoDescription
                        }
                    ];
                }
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
                // Sin bloquear el reintento del ticket si el catalogo de canal de ingreso no responde.
            }

            var (latitud, longitud) = ParseCoordinates(pending.Coordinates);
            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                IdCanalIngreso = idCanalIngreso,
                Asunto = pending.Title,
                Descripcion = pending.Comments,
                Correoelectronico = pending.Email,
                Numerotelefonico = pending.Phone,
                Dependencia = pending.Dependencia,
                Idservicio = pending.IdServicio,
                Ubicacion = new BackendTicketUbicacionRequest
                {
                    Direccion = pending.Address,
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
                    // El mensaje inicial del hilo muestra el nombre del servicio/reporte (igual
                    // que Zendesk); el texto libre del ciudadano va aparte, en Descripcion.
                    Observaciones = pending.Title,
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
            await DisplayAlert("No se pudo enviar", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            _enviandoPendiente = false;
            boton.IsEnabled = true;
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

        // Evita apilar el detalle dos veces por toques rapidos.
        if (_abriendoDetalle)
        {
            return;
        }

        _abriendoDetalle = true;
        try
        {
            _navigationState.SelectedTicket = ticket;
            await Shell.Current.GoToAsync(nameof(TicketDetailPage));
        }
        finally
        {
            _abriendoDetalle = false;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
