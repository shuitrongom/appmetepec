using Microsoft.Maui.Graphics;
using Microsoft.Maui.Graphics.Platform;
using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace appmetepec.Services;

/// <summary>
/// Foto de evidencia ya materializada en un archivo local propio de la app.
/// A diferencia del <see cref="FileResult"/> que devuelve el <see cref="MediaPicker"/>
/// (cuyo FullPath en iOS puede apuntar a un temporal aun no escrito en disco, lo que
/// produce preview en blanco y subida de 0 bytes), esta ruta esta garantizada: el
/// contenido ya fue copiado byte a byte a un archivo estable en el cache de la app.
/// </summary>
public sealed record EvidencePhoto(string LocalPath, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// Captura o selecciona una foto de evidencia y la materializa en un archivo local
/// confiable, valido tanto en iOS como en Android.
///
/// Motivacion (bug definitivo, no parche): en iOS, <see cref="MediaPicker.CapturePhotoAsync"/>
/// entrega un FileResult cuyo archivo puede no estar completamente escrito en el
/// momento en que la app lo lee. Usar directamente su FullPath para el preview o para
/// abrir el stream de subida provoca imagen en blanco y un cuerpo vacio que el backend
/// rechaza (se manifiesta como "error de conexion"). La solucion robusta es leer el
/// stream una sola vez y copiarlo a un archivo propio; a partir de ahi todo el flujo
/// (preview, subida, guardado offline) opera sobre un archivo que sabemos integro.
/// </summary>
public sealed class EvidencePhotoService
{
    // Subcarpeta dedicada dentro del cache: son archivos efimeros (evidencia en
    // proceso de envio), no datos que deban persistir ni respaldarse.
    private readonly string _workingDirectory =
        Path.Combine(FileSystem.CacheDirectory, "evidence_capture");

    /// <summary>Indica si el dispositivo puede tomar foto con la camara.</summary>
    public bool IsCaptureSupported => MediaPicker.Default.IsCaptureSupported;

    /// <summary>Toma una foto con la camara y la materializa. Null si el usuario cancela.</summary>
    public Task<EvidencePhoto?> CapturePhotoAsync(CancellationToken cancellationToken = default) =>
        MaterializeAsync(() => MediaPicker.Default.CapturePhotoAsync(), cancellationToken);

    /// <summary>Elige una foto de la galeria y la materializa. Null si el usuario cancela.</summary>
    public Task<EvidencePhoto?> PickPhotoAsync(CancellationToken cancellationToken = default) =>
        MaterializeAsync(() => MediaPicker.Default.PickPhotoAsync(), cancellationToken);

    private async Task<EvidencePhoto?> MaterializeAsync(
        Func<Task<FileResult?>> pick, CancellationToken cancellationToken)
    {
        var result = await pick().ConfigureAwait(false);
        if (result is null)
        {
            // El usuario cancelo el selector/camara.
            return null;
        }

        Directory.CreateDirectory(_workingDirectory);

        // SIEMPRE se re-codifica a JPEG. La camara de iOS entrega HEIC (formato de
        // Apple); ese HEIC es el que rompia la subida (el backend no lo procesa y la
        // peticion terminaba como "error de conexion"), mientras que galeria (JPEG) y
        // Android (JPEG) funcionaban. Normalizar a JPEG aqui elimina el problema de
        // raiz para todas las plataformas y ademas reduce el peso del archivo.
        var localPath = Path.Combine(_workingDirectory, $"{Guid.NewGuid():N}.jpg");

        await using (var source = await result.OpenReadAsync().ConfigureAwait(false))
        {
            // Decodifica cualquier formato de entrada (HEIC/PNG/JPEG) y lo vuelve a
            // exportar como JPEG (calidad 0.85) usando el motor grafico nativo de la
            // plataforma. SaveAsync es la API documentada de IImage para persistir.
            using var image = PlatformImage.FromStream(source);
            await using var target = File.Create(localPath);
            await image.SaveAsync(target, ImageFormat.Jpeg, 0.85f).ConfigureAwait(false);
        }

        var info = new FileInfo(localPath);
        if (info.Length == 0)
        {
            // Defensa extra: si aun asi el archivo quedo vacio, lo limpiamos y
            // avisamos con una excepcion clara en vez de subir 0 bytes al backend.
            TryDelete(localPath);
            throw new InvalidOperationException(
                "La foto no se pudo leer del dispositivo. Intenta tomarla de nuevo o elígela desde la galería.");
        }

        return new EvidencePhoto(localPath, Path.GetFileName(localPath), "image/jpeg", info.Length);
    }

    /// <summary>Borra un archivo de evidencia temporal (best-effort).</summary>
    public void Delete(string? localPath)
    {
        if (!string.IsNullOrWhiteSpace(localPath))
        {
            TryDelete(localPath);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Es cache; si no se puede borrar ahora, el SO lo recupera despues.
        }
    }
}
