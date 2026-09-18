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
    string IconSource = "ic_otros.png",
    int IdServicio = 0);

public sealed record DependenciaCategoria(
    int IdDependencia,
    string Nombre,
    IList<ScreenReport> Reportes);

public sealed record UserProfile(string Name, string Email, string Phone);

public sealed class PendingTicketSubmission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime SavedAt { get; set; } = DateTime.Now;
    public string Title { get; set; } = "";
    public int IdServicio { get; set; }
    public string Dependencia { get; set; } = "";
    public string Name { get; set; } = "";
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Address { get; set; } = "";
    public string Coordinates { get; set; } = "";
    public string Comments { get; set; } = "";
    public string? LocalPhotoPath { get; set; }
    public string? PhotoDescription { get; set; }
    public bool IsBache { get; set; }
    public string? LastError { get; set; }
}

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
    public bool destacada { get; set; }
    public DateTime? fechaInicioEvento { get; set; }
    public DateTime? fechaFinEvento { get; set; }
    public bool permiteComentarios { get; set; } = true;
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

public sealed class TwilioVerifyResponse
{
    public bool valid { get; set; }
}

// ── Back-end Metepec (api/seguridad) ─────────────────────────────────────────

public sealed class BackendLoginRequest
{
    [JsonPropertyName("userNameOrEmail")]
    public string UserNameOrEmail { get; set; } = "";

    [JsonPropertyName("password")]
    public string Password { get; set; } = "";

    [JsonPropertyName("plataforma")]
    public string? Plataforma { get; set; }

    // Opcionales: solo se mandan si el ciudadano dio permiso de ubicacion. Nunca bloquean el
    // login si faltan (permiso negado, GPS sin respuesta, etc.) -- ver AuthService.LoginAsync.
    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }
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
    // Usuario y contraseña ya no los captura el ciudadano: el back-end los genera
    // (ver RegistroCiudadanoResponse.IdentityUser.Username / .GeneratedPassword).
    [JsonPropertyName("email")]
    public string Email { get; set; } = "";

    // Token de SolicitarVerificacionEmailAsync, confirmado por VerificarCodigoEmailAsync antes de
    // registrar. El back-end lo vuelve a validar en CiudadanoService.RegistrarAsync.
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("apaterno")]
    public string Apaterno { get; set; } = "";

    [JsonPropertyName("amaterno")]
    public string? Amaterno { get; set; }

    [JsonPropertyName("telefonomovil")]
    public string? Telefonomovil { get; set; }
}

public sealed class BackendRegistroResponse
{
    public BackendRegistroUsuarioDto IdentityUser { get; set; } = new();
    public string GeneratedPassword { get; set; } = "";
}

public sealed class BackendRegistroUsuarioDto
{
    public string Username { get; set; } = "";
}

public sealed class ErrorResponse
{
    public string? error { get; set; }
}

public sealed class ErrorsResponse
{
    public string[]? errors { get; set; }
}

public sealed class BackendSolicitarRecuperacionResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("mensaje")]
    public string Mensaje { get; set; } = "";

    [JsonPropertyName("bloqueado")]
    public bool Bloqueado { get; set; }
}

public sealed class BackendIdentificarUsuarioResponse
{
    [JsonPropertyName("existe")]
    public bool Existe { get; set; }

    [JsonPropertyName("nombreCorto")]
    public string? NombreCorto { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }
}

public sealed class BackendSolicitarVerificacionEmailResponse
{
    [JsonPropertyName("token")]
    public string Token { get; set; } = "";

    [JsonPropertyName("mensaje")]
    public string Mensaje { get; set; } = "";

    [JsonPropertyName("bloqueado")]
    public bool Bloqueado { get; set; }
}

public sealed class BackendCreateTicketRequest
{
    [JsonPropertyName("idciudadano")]
    public int Idciudadano { get; set; }

    [JsonPropertyName("asunto")]
    public string Asunto { get; set; } = "";

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }

    [JsonPropertyName("correoelectronico")]
    public string? Correoelectronico { get; set; }

    [JsonPropertyName("numerotelefonico")]
    public string? Numerotelefonico { get; set; }

    [JsonPropertyName("dependencia")]
    public string? Dependencia { get; set; }

    [JsonPropertyName("idservicio")]
    public int Idservicio { get; set; }

    [JsonPropertyName("idestatus")]
    public int Idestatus { get; set; } = 1;

    // No se expone selector de prioridad en la app: el ciudadano no la elige,
    // se manda fija en "Normal" (Id 2 en el catalogo Prioridad).
    [JsonPropertyName("idprioridad")]
    public int Idprioridad { get; set; } = 2;

    [JsonPropertyName("idCanalIngreso")]
    public int IdCanalIngreso { get; set; }

    [JsonPropertyName("ubicacion")]
    public BackendTicketUbicacionRequest? Ubicacion { get; set; }

    [JsonPropertyName("evidencias")]
    public List<BackendEvidenciaItemRequest>? Evidencias { get; set; }

    [JsonPropertyName("servicios")]
    public List<BackendTicketServicioItemRequest>? Servicios { get; set; }

    [JsonPropertyName("observacion")]
    public BackendTicketObservacionRequest? Observacion { get; set; }
}

public sealed class BackendPrioridadDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("nombre")]
    public string Nombre { get; set; } = "";

    [JsonPropertyName("esDefault")]
    public bool EsDefault { get; set; }
}

public sealed class BackendVersionAppDto
{
    [JsonPropertyName("plataforma")]
    public string Plataforma { get; set; } = "";

    [JsonPropertyName("versionReciente")]
    public string VersionReciente { get; set; } = "";

    [JsonPropertyName("mensaje")]
    public string? Mensaje { get; set; }
}

public sealed class BackendServicioDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    // Null = sin definir (no obliga evidencia). Si coincide con el Id del
    // TipoObligatoriedadEvidencia de clave "OBLIGATORIA", ReportPage exige la foto antes de
    // enviar; cualquier otro valor (u obligatoriedad "Opcional"/"No requerida") no la exige.
    [JsonPropertyName("requierefoto")]
    public int? Requierefoto { get; set; }

    [JsonPropertyName("idTipoModoCoberturaGeografica")]
    public int IdTipoModoCoberturaGeografica { get; set; }

    // Si coincide con AppConstants.ClaveModoCoberturaGeocerca, ReportPage exige capturar
    // ubicacion antes de enviar (ver CargarRequerimientosAsync/OnSendClicked).
    [JsonPropertyName("claveTipoModoCoberturaGeografica")]
    public string? ClaveTipoModoCoberturaGeografica { get; set; }
}

public sealed class BackendTipoObligatoriedadEvidenciaDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("clave")]
    public string Clave { get; set; } = "";
}

public sealed class BackendTicketServicioItemRequest
{
    [JsonPropertyName("idServicio")]
    public int IdServicio { get; set; }

    [JsonPropertyName("esPrincipal")]
    public bool EsPrincipal { get; set; }
}

public sealed class BackendTicketObservacionRequest
{
    [JsonPropertyName("idTipoMensaje")]
    public int IdTipoMensaje { get; set; }

    [JsonPropertyName("idusuarioemisor")]
    public int? Idusuarioemisor { get; set; }

    [JsonPropertyName("visibleCiudadano")]
    public bool VisibleCiudadano { get; set; }

    [JsonPropertyName("observaciones")]
    public string Observaciones { get; set; } = "";
}

public sealed class BackendTicketUbicacionRequest
{
    [JsonPropertyName("direccion")]
    public string? Direccion { get; set; }

    [JsonPropertyName("latitud")]
    public decimal? Latitud { get; set; }

    [JsonPropertyName("longitud")]
    public decimal? Longitud { get; set; }

    [JsonPropertyName("coordenadas")]
    public string? Coordenadas { get; set; }
}

public sealed class BackendEvidenciaItemRequest
{
    [JsonPropertyName("nombreArchivo")]
    public string NombreArchivo { get; set; } = "";

    [JsonPropertyName("rutaArchivo")]
    public string RutaArchivo { get; set; } = "";

    [JsonPropertyName("tipoMime")]
    public string? TipoMime { get; set; }

    [JsonPropertyName("tamanoBytes")]
    public long? TamanoBytes { get; set; }

    [JsonPropertyName("esEvidenciaInicial")]
    public bool EsEvidenciaInicial { get; set; }

    [JsonPropertyName("descripcion")]
    public string? Descripcion { get; set; }
}

public sealed class BackendTicketDto
{
    public int Id { get; set; }
    public string? Folio { get; set; }
    public string Asunto { get; set; } = "";
    public string? Descripcion { get; set; }
    public string? Descestatus { get; set; }
    public string? Colorestatus { get; set; }
    public string? Claveestatus { get; set; }
    public string? Descservicio { get; set; }
    public string? Dependencia { get; set; }
    public int Idciudadano { get; set; }
    public DateTime Fechaalta { get; set; }
    public int Idestatus { get; set; }
    public bool ConfirmadoCiudadano { get; set; }
    public DateTime? FechaConfirmacionCiudadano { get; set; }
    public DateTime? FechaCierre { get; set; }
    public DateTime? Fechareapertura { get; set; }
}

public sealed class BackendTipoEncuestaDto
{
    public int Id { get; set; }
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public sealed class BackendCanalIngresoDto
{
    public int Id { get; set; }
    public string Clave { get; set; } = "";
    public string Nombre { get; set; } = "";
}

public sealed class BackendEncuestaPreguntaDto
{
    public int Id { get; set; }
    public int IdTipoEncuesta { get; set; }
    public string Pregunta { get; set; } = "";
    public string TipoRespuesta { get; set; } = "";
    public int Orden { get; set; }
}

public sealed class BackendEncuestaDto
{
    public int Id { get; set; }
}

public sealed class BackendEncuestaRespuestaItemRequest
{
    public int IdPregunta { get; set; }
    public string? RespuestaTexto { get; set; }
    public int? RespuestaNumero { get; set; }
    public bool? RespuestaBool { get; set; }
}

public sealed class BackendSubmitEncuestaRequest
{
    public int IdTipoEncuesta { get; set; }
    public int? IdTicket { get; set; }
    public int? IdCiudadano { get; set; }
    public string Canal { get; set; } = "RESOLUCION";
    public int? CalificacionGeneral { get; set; }
    public string? Comentario { get; set; }
    public List<BackendEncuestaRespuestaItemRequest> Respuestas { get; set; } = new();
}

public sealed class BackendTicketObservacionDto
{
    public long Id { get; set; }
    public string Observaciones { get; set; } = "";
    public bool Visibleciudadano { get; set; }
    public DateTime Fechaalta { get; set; }
    public string? Desctipomensaje { get; set; }
    public string? Clavetipomensaje { get; set; }
    public string? Descestatus { get; set; }
    public string Usuarioregistra { get; set; } = "";
    public string? NombreUsuarioregistra { get; set; }
    public List<BackendTicketObservacionEvidenciaDto>? Evidencias { get; set; }
    public bool HasEvidencias => Evidencias is { Count: > 0 };
    public string NombreMostrar => string.IsNullOrWhiteSpace(NombreUsuarioregistra) ? "Atención Metepec" : NombreUsuarioregistra;
}

public sealed class BackendTicketObservacionEvidenciaDto
{
    public long Id { get; set; }
    public string NombreArchivo { get; set; } = "";
    public string RutaArchivo { get; set; } = "";
    public string? TipoMime { get; set; }
    public string? Descripcion { get; set; }
    public bool HasDescripcion => !string.IsNullOrWhiteSpace(Descripcion);
}

public sealed class BackendUploadResult
{
    [JsonPropertyName("nombreOriginal")]
    public string NombreOriginal { get; set; } = "";

    [JsonPropertyName("nombreFisico")]
    public string NombreFisico { get; set; } = "";

    [JsonPropertyName("extension")]
    public string Extension { get; set; } = "";

    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";

    [JsonPropertyName("peso")]
    public long Peso { get; set; }

    [JsonPropertyName("ruta")]
    public string Ruta { get; set; } = "";
}

public sealed class BackendCiudadanoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("fechaPrimerAcceso")]
    public DateTime? FechaPrimerAcceso { get; set; }
}

public sealed class BackendPublicacionDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = "";

    [JsonPropertyName("resumen")]
    public string? Resumen { get; set; }

    [JsonPropertyName("contenido")]
    public string Contenido { get; set; } = "";

    [JsonPropertyName("imagenPrincipal")]
    public string? ImagenPrincipal { get; set; }

    [JsonPropertyName("publicada")]
    public bool Publicada { get; set; }

    [JsonPropertyName("destacada")]
    public bool Destacada { get; set; }

    [JsonPropertyName("fechaInicioEvento")]
    public DateTime? FechaInicioEvento { get; set; }

    [JsonPropertyName("fechaFinEvento")]
    public DateTime? FechaFinEvento { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }

    [JsonPropertyName("fechaPublicacion")]
    public DateTime? FechaPublicacion { get; set; }

    [JsonPropertyName("fechaInicioVigencia")]
    public DateTime? FechaInicioVigencia { get; set; }

    [JsonPropertyName("fechaFinVigencia")]
    public DateTime? FechaFinVigencia { get; set; }

    [JsonPropertyName("permiteComentarios")]
    public bool PermiteComentarios { get; set; } = true;
}

public sealed class BackendPublicacionReaccionDto
{
    [JsonPropertyName("idPublicacion")]
    public int IdPublicacion { get; set; }

    [JsonPropertyName("idCiudadano")]
    public int IdCiudadano { get; set; }

    [JsonPropertyName("tipo")]
    public string Tipo { get; set; } = "";

    [JsonPropertyName("fechaReaccion")]
    public DateTime FechaReaccion { get; set; }
}

public sealed class BackendPublicacionComentarioDto
{
    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("idPublicacion")]
    public int IdPublicacion { get; set; }

    // Nullable: una respuesta hecha desde el panel web (IdUsuario) no trae ciudadano.
    [JsonPropertyName("idCiudadano")]
    public int? IdCiudadano { get; set; }

    [JsonPropertyName("idUsuario")]
    public int? IdUsuario { get; set; }

    [JsonPropertyName("idComentarioPadre")]
    public long? IdComentarioPadre { get; set; }

    [JsonPropertyName("comentario")]
    public string Comentario { get; set; } = "";

    [JsonPropertyName("fechaComentario")]
    public DateTime FechaComentario { get; set; }

    // Antes "nombreCiudadano": el back-end lo renombro porque ahora tambien puede ser el nombre
    // de un agente/administrador que respondio desde el panel web (ver EsRespuestaAdmin).
    [JsonPropertyName("nombreAutor")]
    public string? NombreAutor { get; set; }

    [JsonPropertyName("esRespuestaAdmin")]
    public bool EsRespuestaAdmin { get; set; }

    // El ciudadano comento como anonimo; NombreAutor ya viene como "Anónimo" en ese caso.
    [JsonPropertyName("anonimo")]
    public bool Anonimo { get; set; }
}

public sealed class BackendArticuloConocimientoDto
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("titulo")]
    public string Titulo { get; set; } = "";

    [JsonPropertyName("resumen")]
    public string? Resumen { get; set; }

    [JsonPropertyName("contenido")]
    public string Contenido { get; set; } = "";

    [JsonPropertyName("palabrasClave")]
    public string? PalabrasClave { get; set; }

    [JsonPropertyName("activo")]
    public bool Activo { get; set; }
}
