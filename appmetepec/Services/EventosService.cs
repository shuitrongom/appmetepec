using System.Net.Http.Headers;
using System.Text.Json;
using appmetepec.Models;

namespace appmetepec.Services;

// Servicio del modulo de Eventos interactivos (mapa de escenarios).
//
// Diseno a prueba de futuro: hoy el backend aun no expone /eventos, asi que el servicio
// trae datos de EJEMPLO (Quimera) para poder construir y probar la UI en iOS/Android.
// Cuando el API real exista, basta con poner UsarDatosDeEjemplo = false (o quitar la
// rama de ejemplo): los metodos ya consumen MetepecBackendUrl + "/eventos" con el mismo
// patron que el resto de MetepecApiService (Bearer token, System.Text.Json).
public sealed class EventosService
{
    private readonly HttpClient _httpClient;
    private readonly PreferencesService _preferences;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    // Mientras el backend no tenga el modulo, servimos datos de ejemplo para poder
    // desarrollar/probar la pantalla. Cambiar a false (en AppConstants) cuando /eventos
    // este disponible. Es una propiedad (no const) para no generar codigo inaccesible.
    private static bool UsarDatosDeEjemplo => AppConstants.EventosUsarDatosDeEjemplo;

    public EventosService(HttpClient httpClient, PreferencesService preferences)
    {
        _httpClient = httpClient;
        _preferences = preferences;
    }

    /// <summary>Eventos activos (para el menu/listado). Si no hay, lista vacia.</summary>
    public async Task<List<BackendEventoDto>> GetEventosActivosAsync(CancellationToken cancellationToken = default)
    {
        if (UsarDatosDeEjemplo)
        {
            return EventosEjemplo.Listado();
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + "/eventos");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await ReadJsonAsync<List<BackendEventoDto>>(response, cancellationToken);
        return result ?? [];
    }

    /// <summary>Detalle de un evento: imagen + escenarios (con % posicion) + actividades.</summary>
    public async Task<BackendEventoDto?> GetEventoDetalleAsync(int idEvento, CancellationToken cancellationToken = default)
    {
        if (UsarDatosDeEjemplo)
        {
            return EventosEjemplo.Detalle(idEvento);
        }

        using var message = new HttpRequestMessage(HttpMethod.Get, AppConstants.MetepecBackendUrl + $"/eventos/{idEvento}");
        AddBackendAuthorization(message);

        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadJsonAsync<BackendEventoDto>(response, cancellationToken);
    }

    private void AddBackendAuthorization(HttpRequestMessage message)
    {
        if (!string.IsNullOrWhiteSpace(_preferences.JwtToken))
        {
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _preferences.JwtToken);
        }
    }

    private static async Task<T?> ReadJsonAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
    }
}
