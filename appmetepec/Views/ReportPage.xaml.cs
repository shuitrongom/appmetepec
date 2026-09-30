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
    // Foto de evidencia ya materializada en disco (no el FileResult crudo del picker):
    // garantiza que preview, subida y guardado offline usen un archivo integro, evitando
    // el bug de iOS donde la foto de camara llegaba vacia. Ver EvidencePhotoService.
    private EvidencePhoto? _attachment;
    private string _coordinates = "";
    private List<BackendArticuloConocimientoDto> _articulos = [];
    // Servicio.Requierefoto == Id del TipoObligatoriedadEvidencia de clave "OBLIGATORIA": si no
    // se pudo determinar (catalogo no respondio, servicio sin ese campo, etc.) se asume false,
    // para no bloquear el envio de un reporte por un fallo de red ajeno al ciudadano.
    private bool _evidenciaObligatoria;
    // Servicio.ClaveTipoModoCoberturaGeografica == "GEOCERCA": igual que _evidenciaObligatoria,
    // si no se pudo determinar se asume false (el backend vuelve a validar de todas formas, ver
    // AppConstants.ClaveModoCoberturaGeocerca).
    private bool _coberturaGeografica;
    private readonly GeocodingService _geocoding;
    private readonly EvidencePhotoService _evidencePhotos;
    // Al cerrar el modal del mapa se vuelve a disparar OnAppearing; sin esto se reiniciaria el
    // formulario (nombre/telefono/correo editados por el ciudadano, requerimientos, etc.).
    private bool _volviendoDelMapa;

    public ReportPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState, PendingTicketsService pendingTickets, GeocodingService geocoding, EvidencePhotoService evidencePhotos)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
        _pendingTickets = pendingTickets;
        _geocoding = geocoding;
        _evidencePhotos = evidencePhotos;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_volviendoDelMapa)
        {
            _volviendoDelMapa = false;
            return;
        }

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

        _evidenciaObligatoria = false;
        EvidenciaLabel.Text = "Agrega una evidencia:";
        EvidenciaDescripcionEntry.Placeholder = "Describe brevemente la foto (opcional)";
        _coberturaGeografica = false;
        UbicacionRequeridaLabel.IsVisible = false;
        VialidadEstatalAviso.IsVisible = false;

        _ = CargarArticulosAsync();
        _ = CargarRequerimientosAsync();
    }

    // Consulta el servicio del reporte (y el catalogo de obligatoriedad de evidencia) para saber
    // si este reporte en particular exige una foto y/o una ubicacion antes de enviarse (ver
    // comentarios en BackendServicioDto.Requierefoto/ClaveTipoModoCoberturaGeografica). Best-effort:
    // si cualquier consulta falla, no se obliga nada -- no tiene sentido bloquear el reporte por
    // un problema de red ajeno a lo que el ciudadano esta reportando; el backend vuelve a validar
    // la cobertura geografica de todas formas al crear el ticket.
    private async Task CargarRequerimientosAsync()
    {
        if (_report is null || _report.IdServicio <= 0)
        {
            return;
        }

        try
        {
            var serviciosTask = _api.GetServiciosAsync();
            var tiposTask = _api.GetTiposObligatoriedadEvidenciaAsync();
            await Task.WhenAll(serviciosTask, tiposTask);

            var servicio = serviciosTask.Result.FirstOrDefault(s => s.Id == _report.IdServicio);
            var tipoObligatoria = tiposTask.Result.FirstOrDefault(t =>
                string.Equals(t.Clave, AppConstants.ClaveObligatoriedadEvidenciaObligatoria, StringComparison.OrdinalIgnoreCase));

            _evidenciaObligatoria = servicio?.Requierefoto is not null
                && tipoObligatoria is not null
                && servicio.Requierefoto == tipoObligatoria.Id;

            if (_evidenciaObligatoria)
            {
                EvidenciaLabel.Text = "Agrega una evidencia (obligatoria):";
                // "Opcional" ya no aplica: al menos esta descripcion o los Comentarios generales
                // se vuelven obligatorios (ver OnSendClicked).
                EvidenciaDescripcionEntry.Placeholder = "Describe brevemente la foto";
            }

            _coberturaGeografica = string.Equals(
                servicio?.ClaveTipoModoCoberturaGeografica, AppConstants.ClaveModoCoberturaGeocerca, StringComparison.OrdinalIgnoreCase);
            UbicacionRequeridaLabel.IsVisible = _coberturaGeografica;
        }
        catch
        {
            // Se queda en _evidenciaObligatoria/_coberturaGeografica = false (ver comentario del metodo).
        }
    }

    private async Task CargarArticulosAsync()
    {
        try
        {
            _articulos = await _api.GetArticulosConocimientoAsync();
        }
        catch
        {
            // Sin bloquear el reporte si el catalogo de articulos no responde.
        }
    }

    private void OnCommentsChanged(object sender, TextChangedEventArgs e)
    {
        var texto = e.NewTextValue?.Trim() ?? "";
        if (texto.Length < 4 || _articulos.Count == 0)
        {
            SuggestionsView.IsVisible = false;
            return;
        }

        var palabras = texto
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(p => p.Length >= 4)
            .ToArray();

        if (palabras.Length == 0)
        {
            SuggestionsView.IsVisible = false;
            return;
        }

        var sugerencias = _articulos
            .Where(a => palabras.Any(p =>
                a.Titulo.Contains(p, StringComparison.OrdinalIgnoreCase) ||
                (a.PalabrasClave?.Contains(p, StringComparison.OrdinalIgnoreCase) ?? false) ||
                a.Contenido.Contains(p, StringComparison.OrdinalIgnoreCase)))
            .Take(3)
            .ToList();

        SuggestionsView.ItemsSource = sugerencias;
        SuggestionsView.IsVisible = sugerencias.Count > 0;
    }

    private async void OnSuggestionSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not BackendArticuloConocimientoDto articulo)
        {
            return;
        }

        SuggestionsView.SelectedItem = null;
        _navigationState.SelectedArticulo = articulo;
        await Shell.Current.GoToAsync(nameof(ArticuloDetailPage));
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
            _ = VerificarVialidadEstatalAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ubicacion", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    // Igual que el map-picker de la web: el ciudadano elige el punto en un mapa (Leaflet) y se
    // llenan las coordenadas y, si Nominatim la encontro, la direccion.
    private async void OnPickOnMapClicked(object sender, EventArgs e)
    {
        var (lat, lng) = ParseCoordinates(_coordinates);
        (double, double)? inicial = lat is not null && lng is not null ? ((double)lat.Value, (double)lng.Value) : null;

        _volviendoDelMapa = true;
        var seleccion = await MapPickerPage.PickAsync(Navigation, _geocoding, inicial);
        if (seleccion is null) return;

        _coordinates = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{seleccion.Latitud},{seleccion.Longitud}");
        CoordinatesLabel.Text = _coordinates;
        if (!string.IsNullOrWhiteSpace(seleccion.Direccion))
        {
            AddressEditor.Text = seleccion.Direccion;
        }

        _ = VerificarVialidadEstatalAsync();
    }

    // Aviso informativo (no bloquea el envio): si el servicio lo tiene activado y el punto cae
    // sobre una vialidad del catalogo "Vialidades estatales", se le indica al ciudadano que la
    // atencion corresponde al Gobierno del Estado. Best-effort: si el API falla, no se muestra.
    private int _consultaVialidad;
    private async Task VerificarVialidadEstatalAsync()
    {
        var consulta = ++_consultaVialidad;
        VialidadEstatalAviso.IsVisible = false;

        var (lat, lng) = ParseCoordinates(_coordinates);
        if (_report is null || !_report.AplicaAvisoVialidadEstatal || _report.IdServicio <= 0 || lat is null || lng is null)
        {
            return;
        }

        try
        {
            var resultado = await _api.ValidarVialidadEstatalAsync(new BackendValidarVialidadEstatalRequest
            {
                IdServicio = _report.IdServicio,
                Latitud = lat.Value,
                Longitud = lng.Value
            });

            if (consulta != _consultaVialidad) return;
            VialidadEstatalAviso.IsVisible = resultado?.EnVialidadEstatal == true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[VialidadEstatal] No se pudo validar la ubicacion: {ex}");
        }
    }

    private async void OnPickImageTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var options = _evidencePhotos.IsCaptureSupported
                ? new[] { "Tomar foto", "Elegir de la galeria" }
                : new[] { "Elegir de la galeria" };

            var choice = await DisplayActionSheet("Agrega una evidencia", "Cancelar", null, options);

            var photo = choice switch
            {
                "Tomar foto" => await _evidencePhotos.CapturePhotoAsync(),
                "Elegir de la galeria" => await _evidencePhotos.PickPhotoAsync(),
                _ => null
            };

            if (photo is null)
            {
                return;
            }

            // Si ya habia una foto seleccionada, liberamos su archivo temporal.
            _evidencePhotos.Delete(_attachment?.LocalPath);

            _attachment = photo;
            AttachmentLabel.Text = _attachment.FileName;
            // Preview desde el archivo local ya materializado: en iOS esto es lo que
            // evita la imagen en blanco de las fotos recien tomadas con la camara.
            PreviewImage.Source = ImageSource.FromFile(_attachment.LocalPath);
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo agregar la evidencia", ErrorMessageHelper.Traducir(ex), "Aceptar");
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

        if (_evidenciaObligatoria && _attachment is null)
        {
            await DisplayAlert("Evidencia requerida", "Este tipo de reporte requiere que agregues una foto de evidencia antes de enviarlo.", "Aceptar");
            return;
        }

        // Cuando la evidencia es obligatoria, la foto sola no basta: se exige tambien algo de
        // texto que le de contexto (los "Comentarios" generales o, en su defecto, la descripcion
        // especifica de la foto) -- cualquiera de los dos es valido, no se piden ambos.
        var evidenciaDescripcion = EvidenciaDescripcionEntry.Text?.Trim() ?? "";
        if (_evidenciaObligatoria && string.IsNullOrWhiteSpace(comments) && string.IsNullOrWhiteSpace(evidenciaDescripcion))
        {
            await DisplayAlert("Descripción requerida", "Este tipo de reporte requiere que describas el problema: agrega comentarios o una descripción de la foto antes de enviarlo.", "Aceptar");
            return;
        }

        var (coordLatitud, coordLongitud) = ParseCoordinates(_coordinates);
        if (_coberturaGeografica && (coordLatitud is null || coordLongitud is null))
        {
            await DisplayAlert("Ubicacion requerida", "Este tipo de reporte solo se puede levantar dentro de una zona de cobertura especifica. Captura tu ubicacion con el boton \"Elige tu direccion en el mapa\" antes de enviarlo.", "Aceptar");
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
                            // Nombre amigable en vez del nombre de archivo que le puso la camara/
                            // galeria del dispositivo (ej. "1000255651.jpg") -- eso es lo que se
                            // muestra en el sistema web al revisar el ticket. RutaArchivo (donde
                            // realmente se guarda/sirve el archivo) no cambia.
                            NombreArchivo = "Evidencia" + Path.GetExtension(uploaded.NombreOriginal),
                            RutaArchivo = uploaded.Ruta,
                            TipoMime = uploaded.MimeType,
                            TamanoBytes = uploaded.Peso,
                            EsEvidenciaInicial = true,
                            Descripcion = string.IsNullOrWhiteSpace(evidenciaDescripcion) ? null : evidenciaDescripcion
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

            // Mismo "cuerpo correo" que la web precarga en ticket-create.ts/onServicioSelected al
            // seleccionar el servicio: si el servicio tiene una macro activa configurada, el
            // mensaje inicial del hilo (visible al ciudadano, dispara el correo de "Respuesta
            // publica" en TicketService.NotificarRespuestaPublicaSiAplicaAsync) usa su Cuerpo en
            // vez de solo el titulo del reporte.
            var mensajeInicial = _report.Title;
            try
            {
                var macros = await _api.GetMacroServiciosAsync();
                var macro = macros.FirstOrDefault(m => m.Idservicio == _report.IdServicio && m.Activo);
                if (macro is not null && !string.IsNullOrWhiteSpace(macro.Cuerpo))
                {
                    var saludo = string.IsNullOrWhiteSpace(name) ? "¡Hola!" : $"¡Hola, {name}!";
                    mensajeInicial = $"{saludo}<br/><br/>{macro.Cuerpo}";
                }
            }
            catch
            {
                // Sin bloquear el envio del reporte si el catalogo de macros no responde; se usa
                // el titulo del reporte como mensaje inicial (comportamiento previo).
            }

            var (latitud, longitud) = ParseCoordinates(_coordinates);
            var request = new BackendCreateTicketRequest
            {
                Idciudadano = _preferences.CiudadanoId,
                Idprioridad = idPrioridad,
                IdCanalIngreso = idCanalIngreso,
                Asunto = _report.Title,
                Descripcion = comments,
                Correoelectronico = email,
                Numerotelefonico = phone,
                Dependencia = _report.Dependencia,
                Idservicio = _report.IdServicio,
                Ubicacion = new BackendTicketUbicacionRequest
                {
                    Direccion = address,
                    Coordenadas = _coordinates,
                    Latitud = latitud,
                    Longitud = longitud
                },
                //Observaciones = comments,
                Evidencias = evidencias,
                Servicios = _report.IdServicio > 0
                    ? [new BackendTicketServicioItemRequest { IdServicio = _report.IdServicio, EsPrincipal = true }]
                    : null,
                Observacion = new BackendTicketObservacionRequest
                {
                    IdTipoMensaje = 1,
                    // El mensaje inicial del hilo muestra el cuerpo de la macro del servicio si hay
                    // una activa (ver arriba), o el nombre del servicio/reporte si no (igual que
                    // Zendesk); el texto libre del ciudadano va aparte, en Descripcion.
                    Observaciones = mensajeInicial,
                    VisibleCiudadano = true
                }
            };

            var ticketId = await _api.CreateTicketAsync(request);
            await Shell.Current.GoToAsync($"{nameof(ReportSuccessPage)}?reportId={ticketId}&isBache={_report.Title.Contains("bache", StringComparison.OrdinalIgnoreCase)}");
        }
        catch (Exception ex)
        {
            var mensajeError = ErrorMessageHelper.Traducir(ex);
            var guardar = await DisplayAlert(
                "No se pudo enviar",
                $"{mensajeError}\n\n¿Quieres guardar este reporte para volver a intentarlo despues?",
                "Guardar",
                "Cancelar");

            if (guardar)
            {
                await GuardarReportePendienteAsync(name, email, phone, address, comments, mensajeError);
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
            Dependencia = _report.Dependencia,
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
            submission.PhotoDescription = string.IsNullOrWhiteSpace(EvidenciaDescripcionEntry.Text) ? null : EvidenciaDescripcionEntry.Text.Trim();
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
