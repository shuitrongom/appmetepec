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

    public async Task LoadZendeskCredentialsAsync(CancellationToken cancellationToken = default)
    {
        var credentials = await GetJsonAsync<ZendeskCredentialsResponse>(AppConstants.ZendeskCredentialsUrl, cancellationToken);
        if (credentials is null)
        {
            return;
        }

        _preferences.ZendeskUser = credentials.usuario ?? "";
        _preferences.ZendeskToken = credentials.token ?? "";
        _preferences.SchedStartTime = credentials.schedStartTime ?? _preferences.SchedStartTime;
        _preferences.SchedEndTime = credentials.schedEndTime ?? _preferences.SchedEndTime;
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

    public async Task<long?> CreateZendeskReportAsync(ReportSubmission submission, CancellationToken cancellationToken = default)
    {
        await LoadZendeskCredentialsIfNeededAsync(cancellationToken);

        var uploads = new List<string>();
        if (submission.Attachment is not null)
        {
            var token = await UploadZendeskAttachmentAsync(submission.Attachment, cancellationToken);
            if (!string.IsNullOrWhiteSpace(token))
            {
                uploads.Add(token);
            }
        }

        var request = BuildZendeskRequest(submission, uploads);
        var message = new HttpRequestMessage(HttpMethod.Post, AppConstants.BaseZendeskUrl + "/api/v2/requests.json")
        {
            Content = JsonContent(request)
        };
        AddZendeskAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var created = await ReadJsonAsync<ZendeskRequestResponse>(response, cancellationToken);
        return created?.request?.id;
    }

    public async Task<BackendLoginResponse?> LoginAsync(string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        var content = JsonContent(new BackendLoginRequest
        {
            UserNameOrEmail = userNameOrEmail,
            Password = password
        });

        using var response = await _httpClient.PostAsync(AppConstants.MetepecBackendUrl + "/seguridad/login", content, cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            return null;

        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendLoginResponse>(response, cancellationToken);
    }

    public async Task<string?> RegisterAsync(BackendRegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsync(AppConstants.MetepecBackendUrl + "/ciudadanos/registro", JsonContent(request), cancellationToken);

        if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
        {
            var body = await ReadJsonAsync<ErrorResponse>(response, cancellationToken);
            throw new InvalidOperationException(body?.error ?? "No se pudo completar el registro.");
        }

        response.EnsureSuccessStatusCode();
        return null;
    }

    public async Task<int?> GetMyCiudadanoAsync(CancellationToken cancellationToken = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/ciudadanos/me");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        if (response.StatusCode is System.Net.HttpStatusCode.NotFound or System.Net.HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<BackendCiudadanoDto>(response, cancellationToken);
        return result?.Id;
    }

    public async Task<BackendUploadResult?> UploadEvidenceAsync(FileResult attachment, CancellationToken cancellationToken = default)
    {
        await using var stream = await attachment.OpenReadAsync();
        using var content = new MultipartFormDataContent();
        var fileContent = new StreamContent(stream);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(attachment.ContentType ?? "application/octet-stream");
        content.Add(fileContent, "file", attachment.FileName);
        content.Add(new StringContent("Ticket"), "modulo");

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

    private async Task LoadZendeskCredentialsIfNeededAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_preferences.ZendeskUser) || string.IsNullOrWhiteSpace(_preferences.ZendeskToken))
        {
            await LoadZendeskCredentialsAsync(cancellationToken);
        }
    }

    private async Task<string?> UploadZendeskAttachmentAsync(FileResult file, CancellationToken cancellationToken)
    {
        await LoadZendeskCredentialsIfNeededAsync(cancellationToken);
        await using var stream = await file.OpenReadAsync();
        var request = new HttpRequestMessage(HttpMethod.Post,
            $"{AppConstants.BaseZendeskUrl}/api/v2/uploads.json?filename={Uri.EscapeDataString(file.FileName)}")
        {
            Content = new StreamContent(stream)
        };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType ?? "application/octet-stream");
        AddZendeskAuthorization(request);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var upload = await ReadJsonAsync<ZendeskUploadResponse>(response, cancellationToken);
        return upload?.upload?.token;
    }

    private ZendeskCreateRequest BuildZendeskRequest(ReportSubmission submission, List<string> uploads)
    {
        var tags = new[]
        {
            submission.Report.Dependencia.DisplayName(),
            submission.Report.Title
        }.Select(ToTag).ToList();

        var fields = new List<ZendeskCustomFieldValue>
        {
            Field(ZendeskCustomField.Tramite, "Reporte"),
            Field(ZendeskCustomField.CanalIngreso, "MAUI App"),
            Field(ZendeskCustomField.Nombre, submission.Name),
            Field(ZendeskCustomField.NumeroTelefono, submission.Phone),
            Field(ZendeskCustomField.CorreoElectronico, submission.Email),
            Field(ZendeskCustomField.Direccion, submission.Address),
            Field(ZendeskCustomField.Ubicacion, ""),
            Field(ZendeskCustomField.Coordenadas, submission.Coordinates),
            Field(ZendeskCustomField.Boleta, ""),
            Field(ZendeskCustomField.Observaciones, submission.Comments)
        };

        return new ZendeskCreateRequest
        {
            Request = new ZendeskRequest
            {
                Subject = $"{ZendeskDependencia.GerenciaCiudad.DisplayName()} - {submission.Report.Title}",
                Comment = new ZendeskComment
                {
                    Body = string.IsNullOrWhiteSpace(submission.Comments) ? submission.Report.Title : submission.Comments,
                    Uploads = uploads.Count > 0 ? uploads : null
                },
                Tags = tags,
                CustomFields = fields
            }
        };
    }

    private void AddZendeskAuthorization(HttpRequestMessage request)
    {
        var auth = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_preferences.ZendeskUser}@mobzilla.com/token:{_preferences.ZendeskToken}"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", auth);
    }

    private static ZendeskCustomFieldValue Field(ZendeskCustomField field, string value) => new()
    {
        Id = (long)field,
        Value = value
    };

    private static string ToTag(string value) => value.ToLowerInvariant().Replace(" ", "_").Replace(",", "_");

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
