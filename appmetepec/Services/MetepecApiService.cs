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

        return (result ?? [])
            .Where(item => item.Publicada && item.Activo)
            .OrderByDescending(item => item.FechaPublicacion)
            .Select(item => new NewsLetter
            {
                id = item.Id,
                title = item.Titulo,
                subtitle = item.Resumen,
                shortContent = item.Resumen,
                content = item.Contenido,
                image = item.ImagenPrincipal
            })
            .ToList();
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
