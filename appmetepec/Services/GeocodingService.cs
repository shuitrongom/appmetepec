using System.Globalization;
using System.Text.Json;

namespace appmetepec.Services;

// Equivalente al GeocodingService de la web (front-end core/services/geocoding.service.ts), contra
// el Nominatim publico de OpenStreetMap. Su politica de uso exige identificar la app (User-Agent) y
// no pasar de 1 consulta por segundo, por eso las llamadas se serializan con _gate. Si el volumen
// crece hay que moverse a un proveedor propio/contratado: basta con cambiar BaseUrl.
public sealed class GeocodingService
{
    private const string BaseUrl = "https://nominatim.openstreetmap.org";
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromSeconds(1);

    // HttpClient propio (no el de MauiProgram): ese lleva AuthExpiredHandler y el token del
    // back-end, que no deben mandarse a un servicio externo.
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTime _ultimaConsulta = DateTime.MinValue;

    public GeocodingService()
    {
        _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd($"Metepec7311/{AppInfo.Current.VersionString} (com.app.metepec)");
        _httpClient.DefaultRequestHeaders.AcceptLanguage.ParseAdd("es");
    }

    // Direccion legible de unas coordenadas, o "" si no se pudo obtener.
    public async Task<string> ReverseGeocodeAsync(double lat, double lng, CancellationToken cancellationToken = default)
    {
        var url = string.Create(CultureInfo.InvariantCulture, $"{BaseUrl}/reverse?lat={lat}&lon={lng}&format=json");
        using var doc = await GetJsonAsync(url, cancellationToken);
        return doc is not null && doc.RootElement.TryGetProperty("display_name", out var name)
            ? name.GetString() ?? ""
            : "";
    }

    // Primer resultado de buscar una direccion en Mexico, o null si no hubo resultados.
    public async Task<(double Lat, double Lng, string Direccion)?> SearchAddressAsync(string query, CancellationToken cancellationToken = default)
    {
        var url = $"{BaseUrl}/search?q={Uri.EscapeDataString(query)}&format=json&limit=1&countrycodes=mx";
        using var doc = await GetJsonAsync(url, cancellationToken);
        if (doc is null || doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
        {
            return null;
        }

        var r = doc.RootElement[0];
        if (!double.TryParse(r.GetProperty("lat").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)
            || !double.TryParse(r.GetProperty("lon").GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var lng))
        {
            return null;
        }

        return (lat, lng, r.TryGetProperty("display_name", out var name) ? name.GetString() ?? "" : "");
    }

    private async Task<JsonDocument?> GetJsonAsync(string url, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var espera = _ultimaConsulta + IntervaloMinimo - DateTime.UtcNow;
            if (espera > TimeSpan.Zero)
            {
                await Task.Delay(espera, cancellationToken);
            }

            _ultimaConsulta = DateTime.UtcNow;
            using var response = await _httpClient.GetAsync(url, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine($"[Geocoding] {ex}");
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }
}
