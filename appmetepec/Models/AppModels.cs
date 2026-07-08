using System.Text.Json.Serialization;

namespace appmetepec.Models;

public enum ZendeskDependencia
{
    ObrasPublicas,
    ServiciosPublicos,
    Opdapas,
    MedioAmbiente,
    ProteccionCivil,
    SeguridadPublica,
    DesarrolloUrbano,
    Gobernacion,
    CallCenter,
    ConsejeriaJuridica,
    C2,
    Otros,
    Dif,
    GerenciaCiudad
}

public static class ZendeskDependenciaExtensions
{
    public static string DisplayName(this ZendeskDependencia dependencia) => dependencia switch
    {
        ZendeskDependencia.ObrasPublicas => "Servicios Publicos",
        ZendeskDependencia.ServiciosPublicos => "Servicios Publicos",
        ZendeskDependencia.Opdapas => "OPDAPAS",
        ZendeskDependencia.MedioAmbiente => "Medio Ambiente",
        ZendeskDependencia.ProteccionCivil => "Proteccion Civil",
        ZendeskDependencia.SeguridadPublica => "Seguridad Publica",
        ZendeskDependencia.DesarrolloUrbano => "Desarrollo Urbano",
        ZendeskDependencia.Gobernacion => "Gobernacion",
        ZendeskDependencia.CallCenter => "Call Center",
        ZendeskDependencia.ConsejeriaJuridica => "Consejeria Juridica",
        ZendeskDependencia.C2 => "C2",
        ZendeskDependencia.Otros => "OTROS",
        ZendeskDependencia.Dif => "DIF",
        _ => "Gerencia de la ciudad"
    };
}

public enum ZendeskCustomField : long
{
    Dependencia = 4415375578651,
    Tramite = 4415359706011,
    Descripcion = 4415375599003,
    CanalIngreso = 4415366554779,
    Nombre = 4415375543963,
    NumeroTelefono = 4415375557019,
    CorreoElectronico = 4415366526619,
    Direccion = 5009955887899,
    Ubicacion = 4415375619611,
    Boleta = 4415375616155,
    Observaciones = 5025334635419,
    Coordenadas = 4415359755547
}

public enum ZendeskNaranjaCustomField : long
{
    Nombre = 9702822013463,
    Direccion = 9702848121623,
    CorreoElectronico = 9702870738711,
    NumeroTelefono = 9702895670167,
    Coordenadas = 9702894264727
}

public sealed record ScreenReport(
    string Title,
    string Instructions,
    bool AddressRequired,
    bool CommentsRequired,
    bool WitnessRequired,
    bool AudioRequired,
    ZendeskDependencia Dependencia,
    int IdSeccion = 0,
    int IdRequest = 0,
    int IdRequestGroup = 0,
    int IdPriority = 0,
    int IdStatus = 0,
    int IdArea = 0,
    int IdForm = 0,
    string IconSource = "ic_otros.png");

public sealed record DependenciaCategoria(
    int IdDependencia,
    string Nombre,
    IList<ScreenReport> Reportes);

public sealed record UserProfile(string Name, string Email, string Phone);

public sealed record ReportSubmission(
    ScreenReport Report,
    string Name,
    string Email,
    string Phone,
    string Address,
    string Coordinates,
    string Comments,
    FileResult? Attachment);

public sealed class NewsLetterResponse
{
    public List<NewsLetter>? Newsletters { get; set; }
    public int ResponseCode { get; set; }
}

public sealed class NewsLetter
{
    public int id { get; set; }
    public string? title { get; set; }
    public string? subtitle { get; set; }
    public string? shortContent { get; set; }
    public string? content { get; set; }
    public string? image { get; set; }
    public string? url { get; set; }
}

public sealed class ZendeskCredentialsResponse
{
    public string? usuario { get; set; }
    public string? token { get; set; }
    public string? schedStartTime { get; set; }
    public string? schedEndTime { get; set; }
    public bool charge_property_tax { get; set; }
    public int taxes_duration { get; set; }
    public List<TaxesLink>? taxes_links { get; set; }
}

public sealed class TaxesLink
{
    public string? name { get; set; }
    public string? img { get; set; }
    public string? android { get; set; }
    public int sort { get; set; }
}

public sealed class TokenRecoleccionResponse
{
    public bool code { get; set; }
    public string? message { get; set; }
    public string? token { get; set; }
}

public sealed class RoutesResponse : GenericRecResponse
{
    public List<RoutesModel> data { get; set; } = [];
}

public sealed class RoutesModel
{
    public int id { get; set; }
    public string? name { get; set; }
    public bool active { get; set; }
}

public sealed class RouteResponse : GenericRecResponse
{
    public RouteInfo? data { get; set; }
}

public sealed class RouteInfo
{
    public RouteModel? route { get; set; }
    public List<RouteMarkerModel> routeMarkers { get; set; } = [];
}

public sealed class RouteModel
{
    public int id { get; set; }
    public string? name { get; set; }
    public bool active { get; set; }
    public string? coordinates { get; set; }
}

public sealed class RouteMarkerModel
{
    public int id { get; set; }
    public double latitude { get; set; }
    public double longitude { get; set; }
}

public sealed class TruckResponse : GenericRecResponse
{
    public TruckModel? data { get; set; }
}

public sealed class TruckModel
{
    public int id { get; set; }
    public string? name { get; set; }
    public double latitude { get; set; }
    public double longitude { get; set; }
    public List<LatitudeLongitudeModel> visited { get; set; } = [];
    public List<LatitudeLongitudeModel> pending { get; set; } = [];
}

public sealed class LatitudeLongitudeModel
{
    public double lat { get; set; }
    public double lng { get; set; }
}

public class GenericRecResponse
{
    public bool success { get; set; }
    public string? message { get; set; }
}

public sealed class ZendeskTicketList
{
    public List<ZendeskTicket> tickets { get; set; } = [];
}

public sealed class ZendeskTicket
{
    public int id { get; set; }
}

public sealed class TwilioVerifyResponse
{
    public bool valid { get; set; }
}

public sealed class ZendeskCreateRequest
{
    [JsonPropertyName("request")]
    public ZendeskRequest Request { get; set; } = new();
}

public sealed class ZendeskRequest
{
    [JsonPropertyName("subject")]
    public string Subject { get; set; } = "";

    [JsonPropertyName("comment")]
    public ZendeskComment Comment { get; set; } = new();

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("custom_fields")]
    public List<ZendeskCustomFieldValue> CustomFields { get; set; } = [];
}

public sealed class ZendeskComment
{
    [JsonPropertyName("body")]
    public string Body { get; set; } = "";

    [JsonPropertyName("uploads")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Uploads { get; set; }
}

public sealed class ZendeskCustomFieldValue
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";
}

public sealed class ZendeskRequestResponse
{
    public ZendeskCreatedRequest? request { get; set; }
}

public sealed class ZendeskCreatedRequest
{
    public long id { get; set; }
    public long requester_id { get; set; }
}

public sealed class ZendeskUploadResponse
{
    public ZendeskUpload? upload { get; set; }
}

public sealed class ZendeskUpload
{
    public string? token { get; set; }
}

// ── Back-end Metepec (api/seguridad) ─────────────────────────────────────────

public sealed class BackendLoginRequest
{
    [JsonPropertyName("userNameOrEmail")]
    public string UserNameOrEmail { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";
}

public sealed class BackendLoginResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; }

    [JsonPropertyName("user")]
    public BackendUserDto User { get; set; } = new();
}

public sealed class BackendUserDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("username")]
    public string Username { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("fullName")]
    public string FullName { get; set; } = "";

    [JsonPropertyName("phoneNumber")]
    public string? PhoneNumber { get; set; }

    [JsonPropertyName("roles")]
    public IReadOnlyCollection<string> Roles { get; set; } = [];
}

public sealed class BackendRegisterRequest
{
    [JsonPropertyName("userName")]
    public string UserName { get; set; } = "";

    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("apaterno")]
    public string Apaterno { get; set; } = "";

    [JsonPropertyName("amaterno")]
    public string? Amaterno { get; set; }

    [JsonPropertyName("telefonomovil")]
    public string? Telefonomovil { get; set; }
}

public sealed class ErrorResponse
{
    public string? error { get; set; }
}
