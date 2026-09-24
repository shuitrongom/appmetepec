using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using appmetepec.Models;

namespace appmetepec.Services;

public sealed class MetepecApiService
{
    private readonly HttpClient _httpClient;
    private readonly PreferencesService _preferences;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    public MetepecApiService(HttpClient httpClient, PreferencesService preferences)
    {
        _httpClient = httpClient;
        _preferences = preferences;
    }

    public Task<NewsLetterResponse?> GetNewsAsync(CancellationToken cancellationToken = default)
    {
        const string url = AppConstants.EnvConsultingUrl +
                           "?actorId=0&comm=searchnewsletter&environmentId=10&newsletterId=0&newsletterTypeId=0&serviceId=10&subjectId=23";
        return GetJsonAsync<NewsLetterResponse>(url, cancellationToken);
    }

    public async Task<string?> GetRecoleccionTokenAsync(CancellationToken cancellationToken = default)
    {
        var content = JsonContent(new { apikey = AppConstants.RecoleccionApiKey });
        using var response = await _httpClient.PostAsync(AppConstants.MetepecApiUrl + "/", content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<TokenRecoleccionResponse>(response, cancellationToken);

        if (result?.code == true && !string.IsNullOrWhiteSpace(result.token))
        {
            _preferences.RecoleccionToken = result.token;
            return result.token;
        }

        return null;
    }

    public async Task<List<RoutesModel>> GetRoutesByLocationAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var token = await EnsureRecoleccionTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Post, AppConstants.RecoleccionUrl + "get-routes-by-location")
        {
            Content = JsonContent(new { lat = latitude, lng = longitude })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<RoutesResponse>(response, cancellationToken);
        return result?.success == true ? result.data : [];
    }

    public async Task<RouteInfo?> GetRouteByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        var token = await EnsureRecoleccionTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Post, AppConstants.RecoleccionUrl + "get-route")
        {
            Content = JsonContent(new { id })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<RouteResponse>(response, cancellationToken);
        return result?.success == true ? result.data : null;
    }

    public async Task<TruckModel?> GetTruckLocationAsync(int routeId, CancellationToken cancellationToken = default)
    {
        var token = await EnsureRecoleccionTokenAsync(cancellationToken);
        var request = new HttpRequestMessage(HttpMethod.Post, AppConstants.RecoleccionUrl + "get-truck-location")
        {
            Content = JsonContent(new { id = routeId })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<TruckResponse>(response, cancellationToken);
        return result?.success == true ? result.data : null;
    }

    public async Task<BackendLoginResponse?> LoginAsync(string userNameOrEmail, string password, decimal? latitud = null, decimal? longitud = null, CancellationToken cancellationToken = default)
    {
        var content = JsonContent(new BackendLoginRequest
        {
            UserNameOrEmail = userNameOrEmail,
            Password = password,
            Plataforma = DeviceInfo.Current.Platform == DevicePlatform.iOS ? "IOS" : "ANDROID",
            Latitud = latitud,
            Longitud = longitud
        });

        using var response = await _httpClient.PostAsync(AppConstants.MetepecBackendUrl + "/seguridad/login", content, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendLoginResponse>(response, cancellationToken);
    }

    public async Task<BackendRegistroResponse?> RegisterAsync(BackendRegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(AppConstants.MetepecBackendUrl + "/ciudadanos/registro", JsonContent(request), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var body = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
            throw new InvalidOperationException(body?.error ?? "No se pudo completar el registro.");
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendRegistroResponse>(response, cancellationToken);
    }

    // Flujo publico "olvide mi contrasena" (recuperacion de cuenta por correo ya registrado).
    // El token siempre se regresa aunque el correo no exista (para no filtrar cuentas), asi que
    // este metodo no lanza por 400: solo EnsureSuccessStatusCode contra errores reales de servidor.
    public async Task<BackendSolicitarRecuperacionResponse?> SolicitarRecuperacionAsync(string email, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            AppConstants.MetepecBackendUrl + "/seguridad/recuperar-password/solicitar",
            JsonContent(new { email }),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendSolicitarRecuperacionResponse>(response, cancellationToken);
    }

    public async Task<bool> VerificarCodigoRecuperacionAsync(string email, string token, string codigo, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            AppConstants.MetepecBackendUrl + "/seguridad/recuperar-password/verificar",
            JsonContent(new { email, token, codigo }),
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // Regresa null si se restablecio correctamente, o el mensaje de error del backend en caso contrario.
    public async Task<string?> RestablecerPasswordAsync(string email, string token, string nuevaContrasena, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            AppConstants.MetepecBackendUrl + "/seguridad/recuperar-password/restablecer",
            JsonContent(new { email, token, nuevaContrasena }),
            cancellationToken);

        if (response.IsSuccessStatusCode) return null;

        var body = await ReadJsonAsync<ErrorsResponse>(response, cancellationToken);
        return body?.errors is { Length: > 0 } errores ? errores[0] : "No se pudo restablecer la contraseña.";
    }

    // Flujo publico "verificar mi correo antes de registrarme". Igual que SolicitarRecuperacionAsync,
    // el token siempre se regresa aunque el correo ya este bloqueado (Bloqueado=true en la respuesta),
    // asi que este metodo no lanza por 400.
    public async Task<BackendSolicitarVerificacionEmailResponse?> SolicitarVerificacionEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            AppConstants.MetepecBackendUrl + "/seguridad/verificar-correo/solicitar",
            JsonContent(new { email }),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendSolicitarVerificacionEmailResponse>(response, cancellationToken);
    }

    public async Task<bool> VerificarCodigoEmailAsync(string email, string token, string codigo, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            AppConstants.MetepecBackendUrl + "/seguridad/verificar-correo/verificar",
            JsonContent(new { email, token, codigo }),
            cancellationToken);
        return response.IsSuccessStatusCode;
    }

    // Usado cuando el login falla: si la credencial (usuario o correo) corresponde a una cuenta
    // activa, se le pregunta al ciudadano "eres tu?" antes de ofrecer el flujo de recuperacion.
    public async Task<BackendIdentificarUsuarioResponse?> IdentificarUsuarioAsync(string credential, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(
            AppConstants.MetepecBackendUrl + "/seguridad/identificar-usuario",
            JsonContent(new { credential }),
            cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendIdentificarUsuarioResponse>(response, cancellationToken);
    }

    public async Task<int?> GetMyCiudadanoAsync(CancellationToken cancellationToken = default)
    {
        var result = await GetMyCiudadanoDetailsAsync(cancellationToken);
        return result?.Id;
    }

    public async Task<BackendCiudadanoDto?> GetMyCiudadanoDetailsAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/ciudadanos/me");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendCiudadanoDto>(response, cancellationToken);
    }

    public async Task<BackendUploadResult?> UploadEvidenceAsync(FileResult attachment, CancellationToken cancellationToken = default)
    {
        await using var stream = await attachment.OpenReadAsync();
        return await UploadEvidenceAsync(stream, attachment.FileName, attachment.ContentType, cancellationToken);
    }

    public async Task<BackendUploadResult?> UploadEvidenceAsync(Stream fileStream, string fileName, string? contentType, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(fileStream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? "application/octet-stream");
        content.Add(fileContent, "file", fileName);
        content.Add(new StringContent("ticket"), "modulo");

        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.MetepecBackendUrl + "/uploads")
        {
            Content = content
        };
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendUploadResult>(response, cancellationToken);
    }

    public async Task<int> CreateTicketAsync(BackendCreateTicketRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.MetepecBackendUrl + "/tickets")
        {
            Content = JsonContent(request)
        };
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var body = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
            throw new InvalidOperationException(body?.error ?? "No se pudo crear el reporte.");
        }

        response.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync<BackendTicketDto>(response, cancellationToken);
        return created?.Id ?? 0;
    }

    public async Task<BackendTicketDto?> ConfirmarResolucionTicketAsync(int idTicket, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.MetepecBackendUrl + $"/tickets/{idTicket}/confirmar-resolucion");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
            throw new InvalidOperationException(body?.error ?? "No se pudo confirmar la resolucion del reporte.");
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendTicketDto>(response, cancellationToken);
    }

    public async Task<BackendTicketDto?> ReabrirTicketAsync(int idTicket, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.MetepecBackendUrl + $"/tickets/{idTicket}/reabrir");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
            throw new InvalidOperationException(body?.error ?? "No se pudo reabrir el reporte.");
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendTicketDto>(response, cancellationToken);
    }

    public async Task<List<BackendTicketDto>> GetMyTicketsAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/tickets/mis-tickets");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<List<BackendTicketDto>>(response, cancellationToken) ?? [];
    }

    public async Task<List<BackendTicketObservacionDto>> GetTicketObservacionesAsync(int idTicket, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/ticket-observaciones/ticket/{idTicket}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<List<BackendTicketObservacionDto>>(response, cancellationToken) ?? [];
        return result.OrderBy(item => item.Fechaalta).ToList();
    }

    public async Task<List<NewsLetter>> GetPublicacionesAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/publicaciones");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<List<BackendPublicacionDto>>(response, cancellationToken);
        // El back-end guarda estas fechas en hora local de Mexico (TicketingServiceHelpers.Now()),
        // no UTC -- hay que comparar contra la misma zona sin importar donde este el dispositivo,
        // para no desfasar la vigencia varias horas.
        var ahora = TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTime.UtcNow, "America/Mexico_City");

        return (result ?? [])
            // El back-end no despublica sola una publicacion vencida (Publicada se queda en true
            // para siempre, "Finalizada" es solo un estado visual del panel admin -- ver
            // PublicacionService.CalcularEstado); appmetepec debe filtrar la vigencia el mismo.
            .Where(item => item.Publicada && item.Activo
                && (item.FechaInicioVigencia is null || item.FechaInicioVigencia.Value <= ahora)
                && (item.FechaFinVigencia is null || item.FechaFinVigencia.Value > ahora))
            .OrderByDescending(item => item.FechaPublicacion)
            .Select(item => new NewsLetter
            {
                id = item.Id,
                title = item.Titulo,
                subtitle = item.Resumen,
                shortContent = item.Resumen,
                content = item.Contenido,
                image = item.ImagenPrincipal,
                destacada = item.Destacada,
                fechaInicioEvento = item.FechaInicioEvento,
                fechaFinEvento = item.FechaFinEvento,
                permiteComentarios = item.PermiteComentarios
            })
            .ToList();
    }

    // El back-end incrementa Publicacion.Vistas solo cuando GetById lo consulta un usuario
    // con rol Ciudadano (ver PublicacionService.GetByIdAsync). El listado (GetPublicacionesAsync)
    // no cuenta como vista; hay que golpear este endpoint al abrir el detalle de una noticia.
    // Best-effort: si falla, no debe afectar la experiencia de lectura de la noticia.
    public async Task RegistrarVistaPublicacionAsync(int idPublicacion, int idCiudadano, CancellationToken cancellationToken = default)
    {
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Get, $"{AppConstants.MetepecBackendUrl}/publicaciones/{idPublicacion}");
            AddBackendAuthorization(message);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Ignorado a proposito: es solo telemetria de lectura, no debe interrumpir al usuario.
        }

        // Ademas del contador simple (Vistas), registra el ciudadano en PublicacionVista para que
        // el panel de administracion pueda contar ciudadanos unicos, no solo aperturas totales.
        if (idCiudadano <= 0) return;
        try
        {
            using var message = new HttpRequestMessage(HttpMethod.Post, $"{AppConstants.MetepecBackendUrl}/publicacion-vistas")
            {
                Content = JsonContent(new { idPublicacion, idCiudadano })
            };
            AddBackendAuthorization(message);
            using var response = await _httpClient.SendAsync(message, cancellationToken);
        }
        catch
        {
            // Ignorado a proposito: es solo telemetria de lectura, no debe interrumpir al usuario.
        }
    }

    public async Task<string?> GetMiReaccionPublicacionAsync(int idPublicacion, int idCiudadano, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{AppConstants.MetepecBackendUrl}/publicacion-reacciones/{idPublicacion}/{idCiudadano}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync<BackendPublicacionReaccionDto>(response, cancellationToken);
        return result?.Tipo;
    }

    // tipo: "Like" o "Dislike". Reaccionar de nuevo con un tipo distinto reemplaza la reaccion
    // anterior (un ciudadano solo puede tener una reaccion activa por publicacion).
    public async Task<string?> ReaccionarPublicacionAsync(int idPublicacion, int idCiudadano, string tipo, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{AppConstants.MetepecBackendUrl}/publicacion-reacciones")
        {
            Content = JsonContent(new { idPublicacion, idCiudadano, tipo })
        };
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();

        var result = await ReadJsonAsync<BackendPublicacionReaccionDto>(response, cancellationToken);
        return result?.Tipo;
    }

    public async Task QuitarReaccionPublicacionAsync(int idPublicacion, int idCiudadano, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"{AppConstants.MetepecBackendUrl}/publicacion-reacciones/{idPublicacion}/{idCiudadano}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode != HttpStatusCode.NotFound)
        {
            response.EnsureSuccessStatusCode();
        }
    }

    public async Task<List<BackendPublicacionComentarioDto>> GetComentariosPublicacionAsync(int idPublicacion, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{AppConstants.MetepecBackendUrl}/publicacion-comentarios/publicacion/{idPublicacion}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<List<BackendPublicacionComentarioDto>>(response, cancellationToken) ?? [];
    }

    public async Task<BackendPublicacionComentarioDto> ComentarPublicacionAsync(int idPublicacion, int idCiudadano, string comentario, long? idComentarioPadre = null, bool anonimo = false, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{AppConstants.MetepecBackendUrl}/publicacion-comentarios")
        {
            Content = JsonContent(new { idPublicacion, idCiudadano, comentario, idComentarioPadre, anonimo })
        };
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.BadRequest)
        {
            var body = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
            throw new InvalidOperationException(body?.error ?? "No se pudo publicar el comentario.");
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendPublicacionComentarioDto>(response, cancellationToken)
            ?? throw new InvalidOperationException("No se pudo publicar el comentario.");
    }

    public async Task<bool> EliminarComentarioPublicacionAsync(long idComentario, int idCiudadano, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"{AppConstants.MetepecBackendUrl}/publicacion-comentarios/{idComentario}/{idCiudadano}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return false;
        response.EnsureSuccessStatusCode();
        return true;
    }

    // Se llama en cada arranque de la app (una vez que se tiene el token de Firebase y el
    // ciudadano esta identificado): es un upsert por token en el back-end, asi que reenviar el
    // mismo token en cada arranque es seguro y no crea filas duplicadas.
    public async Task RegistrarDispositivoPushAsync(int idCiudadano, string token, string plataforma, string? modelo, string? versionApp, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"{AppConstants.MetepecBackendUrl}/usuario-dispositivos-push")
        {
            Content = JsonContent(new { idCiudadano, token, plataforma, modelo, versionApp })
        };
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<BackendArticuloConocimientoDto>> GetArticulosConocimientoAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/articulos-conocimiento");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<List<BackendArticuloConocimientoDto>>(response, cancellationToken) ?? [];
        return result.Where(a => a.Activo).ToList();
    }

    public async Task<List<BackendPrioridadDto>> GetPrioridadesAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/prioridades");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<List<BackendPrioridadDto>>(response, cancellationToken) ?? [];
    }

    // Publico (sin JWT, ver VersionAppsController.GetActual): se llama desde el Splash, antes de
    // que el ciudadano inicie sesion. null si no hay configuracion para esa plataforma o si la
    // consulta falla -- en ambos casos SplashViewModel simplemente no muestra ningun aviso.
    public async Task<BackendVersionAppDto?> GetVersionAppActualAsync(string plataforma, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"{AppConstants.MetepecBackendUrl}/version-apps/actual/{plataforma}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendVersionAppDto>(response, cancellationToken);
    }

    public async Task<List<BackendServicioDto>> GetServiciosAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/servicios");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<List<BackendServicioDto>>(response, cancellationToken) ?? [];
    }

    public async Task<List<BackendCatalogoReporteDependenciaDto>> GetCatalogoReportesAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/servicios/catalogo-reportes");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<List<BackendCatalogoReporteDependenciaDto>>(response, cancellationToken) ?? [];
    }

    public async Task<List<BackendTipoObligatoriedadEvidenciaDto>> GetTiposObligatoriedadEvidenciaAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/tipo-obligatoriedad-evidencias");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<List<BackendTipoObligatoriedadEvidenciaDto>>(response, cancellationToken) ?? [];
    }

    public async Task<BackendCanalIngresoDto?> GetCanalIngresoByClaveAsync(string clave, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/canal-ingresos/clave/{clave}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendCanalIngresoDto>(response, cancellationToken);
    }

    public async Task<BackendTipoEncuestaDto?> GetTipoEncuestaByClaveAsync(string clave, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/tipos-encuesta/clave/{clave}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendTipoEncuestaDto>(response, cancellationToken);
    }

    public async Task<List<BackendEncuestaPreguntaDto>> GetEncuestaPreguntasAsync(int idTipoEncuesta, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/encuesta-preguntas?idTipoEncuesta={idTipoEncuesta}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<List<BackendEncuestaPreguntaDto>>(response, cancellationToken) ?? [];
        return result.OrderBy(item => item.Orden).ToList();
    }

    public async Task<bool> ExisteEncuestaTicketAsync(int idTicket, int idTipoEncuesta, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/encuestas/ticket/{idTicket}?idTipoEncuesta={idTipoEncuesta}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task<bool> ExisteEncuestaCiudadanoAsync(int idCiudadano, int idTipoEncuesta, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/encuestas/ciudadano/{idCiudadano}?idTipoEncuesta={idTipoEncuesta}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        response.EnsureSuccessStatusCode();
        return true;
    }

    public async Task SubmitEncuestaAsync(BackendSubmitEncuestaRequest request, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.MetepecBackendUrl + "/encuestas")
        {
            Content = JsonContent(request)
        };
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void AddBackendAuthorization(HttpRequestMessage message)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _preferences.JwtToken);
    }

    public async Task SendTwilioCodeAsync(string phoneNumber, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.TwilioServiceUrl + "/Verifications")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = "+52" + phoneNumber,
                ["Channel"] = "sms"
            })
        };
        AddTwilioAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<bool> VerifyTwilioCodeAsync(string phoneNumber, string code, CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.TwilioServiceUrl + "/VerificationCheck")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = "+52" + phoneNumber,
                ["Code"] = code
            })
        };
        AddTwilioAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<TwilioVerifyResponse>(response, cancellationToken);
        return result?.valid == true;
    }

    private static void AddTwilioAuthorization(HttpRequestMessage message)
    {
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{AppConstants.TwilioAccountSid}:{AppConstants.TwilioAuthToken}"));
        message.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);
    }

    private async Task<string> EnsureRecoleccionTokenAsync(CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(_preferences.RecoleccionToken))
        {
            return _preferences.RecoleccionToken;
        }

        return await GetRecoleccionTokenAsync(cancellationToken) ?? "";
    }

    private static StringContent JsonContent<T>(T value) =>
        new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

    private async Task<T?> GetJsonAsync<T>(string url, CancellationToken cancellationToken)
    {
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<T>(response, cancellationToken);
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }
}
