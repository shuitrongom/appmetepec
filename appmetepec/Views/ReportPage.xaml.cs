using System.Collections.ObjectModel;
using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

// Un elemento de evidencia ya seleccionado (foto o video, tomado o elegido de la galeria) en
// espera de subirse al enviar el reporte. EsVideo se decide segun la accion que el ciudadano
// eligio en el action sheet (CapturarVideoAsync/PickVideoAsync vs CapturePhotoAsync/
// PickPhotoAsync), no por inspeccion de ContentType, porque ya se sabe en el momento de crearlo.
public sealed class EvidenciaSeleccionada
{
    // Evidencia ya MATERIALIZADA en un archivo local propio de la app (ver EvidencePhotoService):
    // la ruta es estable y legible tanto en Android como en iOS. Antes se guardaba el FileResult
    // crudo del picker, cuyo path en iOS (sobre todo videos) apunta a un temporal protegido
    // (PluginKitPlugin/tmp) que al subir daba UnauthorizedAccess_IODenied.
    public EvidencePhoto Material { get; }
    public bool EsVideo { get; }
    public bool EsFoto => !EsVideo;
    public ImageSource? Miniatura { get; }

    public EvidenciaSeleccionada(EvidencePhoto material, bool esVideo)
    {
        Material = material;
        EsVideo = esVideo;
        // La miniatura de foto se arma desde la ruta local materializada (en iOS esto evita la
        // imagen en blanco de la camara). Para video no se muestra miniatura.
        Miniatura = esVideo ? null : ImageSource.FromFile(material.LocalPath);
    }
}

public partial class ReportPage : ContentPage
{
    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private readonly PendingTicketsService _pendingTickets;
    private ScreenReport? _report;
    // Varias evidencias (fotos y/o videos): se pueden agregar y quitar antes de enviar el
    // reporte. El envio en si (OnSendClicked) sube cada una y solo la primera lleva la
    // descripcion capturada en EvidenciaDescripcionEntry (EsEvidenciaInicial).
    private readonly ObservableCollection<EvidenciaSeleccionada> _evidencias = [];
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
    // Materializa fotos/videos del picker a un archivo local estable (clave para iOS).
    private readonly EvidencePhotoService _evidencePhotos;
    // Al cerrar el modal del mapa se vuelve a disparar OnAppearing; sin esto se reiniciaria el
    // formulario (nombre/telefono/correo editados por el ciudadano, requerimientos, etc.).
    private bool _volviendoDelMapa;
    // El picker (camara/galeria) aparece como vista modal; en iOS eso re-dispara OnAppearing al
    // volver. Esta bandera evita que OnAppearing reinicialice el formulario tras seleccionar.
    private bool _volviendoDeEvidencia;

    public ReportPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState, PendingTicketsService pendingTickets, GeocodingService geocoding, EvidencePhotoService evidencePhotos)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
        _pendingTickets = pendingTickets;
        _geocoding = geocoding;
        _evidencePhotos = evidencePhotos;
        EvidenciasView.ItemsSource = _evidencias;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (_volviendoDelMapa)
        {
            _volviendoDelMapa = false;
            return;
        }

        if (_volviendoDeEvidencia)
        {
            _volviendoDeEvidencia = false;
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
        AjustarCamposEditables(tieneTelefono: !string.IsNullOrWhiteSpace(user.Phone), tieneCorreo: !string.IsNullOrWhiteSpace(user.Email));
        _ = CargarDatosCiudadanoAsync();

        _evidenciaObligatoria = false;
        EvidenciaLabel.Text = "Agrega una o más evidencias:";
        EvidenciaDescripcionEntry.Placeholder = "Describe brevemente la evidencia (opcional)";
        _evidencias.Clear();
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
                EvidenciaLabel.Text = "Agrega una o más evidencias (obligatorio):";
                // "Opcional" ya no aplica: al menos esta descripcion o los Comentarios generales
                // se vuelven obligatorios (ver OnSendClicked).
                EvidenciaDescripcionEntry.Placeholder = "Describe brevemente la evidencia";
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

            _coordinates = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{location.Latitude},{location.Longitude}");
            CoordinatesLabel.Text = _coordinates;
            _ = VerificarVialidadEstatalAsync();

            // Geocodificacion inversa (coordenadas -> direccion), igual que el map-picker: se
            // rellena el campo de direccion automaticamente. Solo si el campo esta vacio o tiene
            // una direccion anterior auto-generada, para no pisar algo que el ciudadano escribio.
            // Best-effort: si Nominatim no responde, quedan al menos las coordenadas.
            await RellenarDireccionDesdeCoordenadasAsync(location.Latitude, location.Longitude);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Ubicacion", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    // Convierte coordenadas en una direccion legible (Nominatim) y la coloca en el campo de
    // direccion. Muestra un indicador mientras consulta. Si no se obtiene direccion, no toca el
    // campo (las coordenadas ya quedaron guardadas y el reporte se puede enviar igual).
    private async Task RellenarDireccionDesdeCoordenadasAsync(double lat, double lng)
    {
        try
        {
            UbicacionBusyIndicator.IsVisible = UbicacionBusyIndicator.IsRunning = true;
            var direccion = await _geocoding.ReverseGeocodeAsync(lat, lng);
            if (!string.IsNullOrWhiteSpace(direccion))
            {
                AddressEditor.Text = direccion;
            }
        }
        catch (Exception ex)
        {
            // No bloquea: si falla, el ciudadano puede escribir la direccion a mano.
            System.Diagnostics.Debug.WriteLine($"[Ubicacion] Reverse geocode fallo: {ex}");
        }
        finally
        {
            UbicacionBusyIndicator.IsVisible = UbicacionBusyIndicator.IsRunning = false;
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

    // Nombre/telefono/correo son de solo lectura y se editan en "Mi perfil" (PerfilPage, que
    // guarda en el Ciudadano). Los datos guardados al iniciar sesion (CurrentUser) salen de la
    // cuenta y no se actualizan al editar el perfil, asi que se refrescan desde /ciudadanos/me;
    // si falla la consulta se quedan los guardados.
    private async Task CargarDatosCiudadanoAsync()
    {
        try
        {
            var ciudadano = await _api.GetMyCiudadanoDetailsAsync();
            if (ciudadano is null)
            {
                return;
            }

            var nombre = string.Join(" ", new[] { ciudadano.Nombre, ciudadano.Apaterno, ciudadano.Amaterno }
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Select(p => p!.Trim()));
            var tieneTelefono = !string.IsNullOrWhiteSpace(ciudadano.Telefonomovil);
            var tieneCorreo = !string.IsNullOrWhiteSpace(ciudadano.Correoelectronico);

            if (!string.IsNullOrWhiteSpace(nombre))
            {
                NameEntry.Text = nombre;
            }
            // Si el perfil no trae el dato se respeta lo que haya (guardado o ya capturado).
            if (tieneTelefono) PhoneEntry.Text = ciudadano.Telefonomovil!.Trim();
            if (tieneCorreo) EmailEntry.Text = ciudadano.Correoelectronico!.Trim();
            AjustarCamposEditables(tieneTelefono, tieneCorreo);
        }
        catch
        {
            // Sin conexion: se usan los datos guardados al iniciar sesion.
        }
    }

    // El nombre nunca se edita al levantar un reporte (NameEntry es IsReadOnly en el XAML).
    // Telefono y correo solo se pueden capturar si el ciudadano no los tiene registrados; si ya
    // vienen con valor quedan de solo lectura (se cambian en "Mi perfil").
    // Se decide por lo que trae el perfil, no por el texto del campo, para no bloquear un dato
    // que el ciudadano ya estaba capturando mientras terminaba de cargar /ciudadanos/me.
    private void AjustarCamposEditables(bool tieneTelefono, bool tieneCorreo)
    {
        PhoneEntry.IsReadOnly = tieneTelefono;
        EmailEntry.IsReadOnly = tieneCorreo;
    }

    private async void OnPickImageTapped(object sender, TappedEventArgs e)
    {
        try
        {
            var options = MediaPicker.Default.IsCaptureSupported
                ? new[] { "Tomar foto", "Elegir foto de la galería", "Grabar video", "Elegir video de la galería" }
                : new[] { "Elegir foto de la galería", "Elegir video de la galería" };

            var choice = await DisplayActionSheet("Agrega una evidencia", "Cancelar", null, options);
            if (choice is not ("Tomar foto" or "Elegir foto de la galería" or "Grabar video" or "Elegir video de la galería"))
            {
                return; // canceló
            }

            // El picker abre como modal; en iOS re-dispara OnAppearing al volver. Evitamos que
            // reinicialice el formulario (perdiendo lo ya capturado y las evidencias agregadas).
            _volviendoDeEvidencia = true;

            var esVideo = choice is "Grabar video" or "Elegir video de la galería";

            // Materializamos SIEMPRE a un archivo local estable:
            // - Fotos: Capture/PickPhotoAsync del servicio (en iOS ademas transcodifica HEIC->JPEG).
            // - Videos: se obtiene el FileResult y se copia byte a byte (MaterializeFileAsync),
            //   lo que resuelve el UnauthorizedAccess de iOS al leer el temporal protegido.
            EvidencePhoto? material = choice switch
            {
                "Tomar foto" => await _evidencePhotos.CapturePhotoAsync(),
                "Elegir foto de la galería" => await _evidencePhotos.PickPhotoAsync(),
                "Grabar video" => await _evidencePhotos.MaterializeFileAsync(await MediaPicker.Default.CaptureVideoAsync()),
                "Elegir video de la galería" => await _evidencePhotos.MaterializeFileAsync(await MediaPicker.Default.PickVideoAsync()),
                _ => null
            };

            if (material is null)
            {
                return; // el usuario canceló el picker/camara
            }

            _evidencias.Add(new EvidenciaSeleccionada(material, esVideo));
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo agregar la evidencia", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    private void OnRemoveEvidenciaTapped(object sender, TappedEventArgs e)
    {
        if (e.Parameter is EvidenciaSeleccionada item)
        {
            _evidencias.Remove(item);
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

        // Se indica exactamente que falta (antes un solo mensaje "telefono y direccion" aunque
        // el telefono ya estuviera capturado).
        var faltantes = new List<string>();
        if (string.IsNullOrWhiteSpace(phone)) faltantes.Add("tu teléfono");
        if (string.IsNullOrWhiteSpace(address)) faltantes.Add("la dirección");
        if (faltantes.Count > 0)
        {
            var mensaje = $"Captura {string.Join(" y ", faltantes)}.";
            // Al elegir el punto en el mapa no siempre se llena la direccion (si no se pudo
            // obtener de la ubicacion): se aclara que hay que escribirla aunque ya haya coordenadas.
            if (string.IsNullOrWhiteSpace(address) && !string.IsNullOrWhiteSpace(_coordinates))
            {
                mensaje += "\n\nLa ubicación ya está marcada en el mapa; escribe también la dirección o una referencia del lugar.";
            }
            await DisplayAlert("Campos obligatorios", mensaje, "Aceptar");
            return;
        }

        if (_evidenciaObligatoria && _evidencias.Count == 0)
        {
            await DisplayAlert("Evidencia requerida", "Este tipo de reporte requiere que agregues una foto o video de evidencia antes de enviarlo.", "Aceptar");
            return;
        }

        // Cuando la evidencia es obligatoria, la foto/video solo no basta: se exige tambien algo
        // de texto que le de contexto (los "Comentarios" generales o, en su defecto, la
        // descripcion de la evidencia) -- cualquiera de los dos es valido, no se piden ambos.
        var evidenciaDescripcion = EvidenciaDescripcionEntry.Text?.Trim() ?? "";
        if (_evidenciaObligatoria && string.IsNullOrWhiteSpace(comments) && string.IsNullOrWhiteSpace(evidenciaDescripcion))
        {
            await DisplayAlert("Descripción requerida", "Este tipo de reporte requiere que describas el problema: agrega comentarios o una descripción de la evidencia antes de enviarlo.", "Aceptar");
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

            // Varias evidencias: se suben una por una (no hay endpoint de subida multiple) y solo
            // la primera lleva la descripcion que capturo el ciudadano (EsEvidenciaInicial),
            // igual que el criterio de un solo adjunto de antes.
            List<BackendEvidenciaItemRequest>? evidencias = null;
            if (_evidencias.Count > 0)
            {
                evidencias = [];
                for (var i = 0; i < _evidencias.Count; i++)
                {
                    // Sube desde el archivo local YA materializado (lectura confiable en iOS,
                    // sin tocar el temporal protegido del picker).
                    var uploaded = await _api.UploadEvidenceAsync(_evidencias[i].Material);
                    if (uploaded is null)
                    {
                        continue;
                    }

                    evidencias.Add(new BackendEvidenciaItemRequest
                    {
                        // Nombre amigable en vez del nombre de archivo que le puso la camara/
                        // galeria del dispositivo (ej. "1000255651.jpg") -- eso es lo que se
                        // muestra en el sistema web al revisar el ticket. RutaArchivo (donde
                        // realmente se guarda/sirve el archivo) no cambia.
                        NombreArchivo = $"Evidencia{i + 1}" + Path.GetExtension(uploaded.NombreOriginal),
                        RutaArchivo = uploaded.Ruta,
                        TipoMime = uploaded.MimeType,
                        TamanoBytes = uploaded.Peso,
                        EsEvidenciaInicial = i == 0,
                        Descripcion = i == 0 && !string.IsNullOrWhiteSpace(evidenciaDescripcion) ? evidenciaDescripcion : null
                    });
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
            IdCiudadano = _preferences.CiudadanoId,
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

        // El respaldo offline ("Mis reportes" -> reintentar) solo conserva la PRIMERA evidencia
        // si el ciudadano agrego varias: MyTicketsPage.xaml.cs reintenta un solo adjunto por
        // reporte pendiente. Limitacion conocida, no silenciosa -- ver comentario alla.
        var primera = _evidencias.FirstOrDefault();
        if (primera is not null)
        {
            // Guarda desde el archivo local ya materializado (ruta estable en iOS y Android).
            submission.LocalPhotoPath = await _pendingTickets.SavePhotoAsync(primera.Material);
            submission.PhotoMimeType = primera.Material.ContentType;
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
