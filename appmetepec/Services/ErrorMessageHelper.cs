namespace appmetepec.Services;

// Las excepciones de .NET/MAUI Essentials (permisos de ubicacion/camara, HttpClient, timeouts,
// JSON) traen su mensaje en ingles por defecto, y varias pantallas lo mostraban tal cual en un
// DisplayAlert con titulo en espanol. Este helper traduce esos casos conocidos a un mensaje en
// espanol para el ciudadano. Cualquier otra excepcion (en particular InvalidOperationException,
// que MetepecApiService.cs usa para propagar el campo "error" ya en espanol que regresa el
// backend) se deja pasar sin tocar, porque ese texto SI es el que se le debe mostrar al usuario.
public static class ErrorMessageHelper
{
    public static string Traducir(Exception ex) => ex switch
    {
        PermissionException =>
            "No se otorgaron los permisos necesarios. Actívalos desde los ajustes de la aplicación e intenta de nuevo.",
        FeatureNotEnabledException =>
            "Esta función está desactivada en tu dispositivo (verifica que el GPS o la cámara estén activados) e intenta de nuevo.",
        FeatureNotSupportedException =>
            "Tu dispositivo no es compatible con esta función.",
        TaskCanceledException or TimeoutException =>
            "La operación tardó demasiado. Verifica tu conexión a internet e intenta de nuevo.",
        HttpRequestException =>
            "No se pudo conectar con el servidor. Verifica tu conexión a internet e intenta de nuevo.",
        System.Text.Json.JsonException =>
            "Ocurrió un problema al procesar la información. Intenta de nuevo más tarde.",
        _ => ex.Message,
    };
}
