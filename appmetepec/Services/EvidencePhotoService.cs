using Microsoft.Maui.Media;
using Microsoft.Maui.Storage;

namespace appmetepec.Services;

/// <summary>
/// Foto de evidencia ya materializada en un archivo local propio de la app.
/// A diferencia del <see cref="FileResult"/> que devuelve el <see cref="MediaPicker"/>
/// (cuyo FullPath en iOS puede apuntar a un temporal que aun no esta escrito en disco
/// cuando la app lo lee, produciendo preview en blanco y subida de 0 bytes), esta ruta
/// esta garantizada: el contenido ya fue copiado byte a byte a un archivo estable.
/// </summary>
public sealed record EvidencePhoto(string LocalPath, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// Captura o selecciona una foto de evidencia y la materializa en un archivo local
/// confiable, valido tanto en iOS como en Android.
///
/// IMPORTANTE (no re-codificar): la foto se copia TAL CUAL, byte a byte, sin pasar por
/// ningun decodificador/codificador de imagen. Un intento previo de convertir a JPEG con
/// Microsoft.Maui.Graphics (PlatformImage) rompio la subida en iOS (generaba archivos
/// corruptos/vacios tanto para camara como para galeria). El unico objetivo de este
/// servicio es MATERIALIZAR el archivo del picker en una ruta estable, que es lo que
/// arregla el caso de la camara en iOS (donde el FileResult llega antes de que el archivo
/// este completo). Galeria y Android ya funcionaban con este mismo enfoque de copia.
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

        // Leemos los bytes originales del archivo del picker una sola vez.
        byte[] originalBytes;
        await using (var source = await result.OpenReadAsync().ConfigureAwait(false))
        await using (var buffer = new MemoryStream())
        {
            await source.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            originalBytes = buffer.ToArray();
        }

        if (originalBytes.Length == 0)
        {
            throw new InvalidOperationException(
                "La foto no se pudo leer del dispositivo. Intenta tomarla de nuevo o elígela desde la galería.");
        }

        var extension = Path.GetExtension(result.FileName);
        var esHeic = extension.Equals(".heic", StringComparison.OrdinalIgnoreCase)
                     || extension.Equals(".heif", StringComparison.OrdinalIgnoreCase);

        byte[] finalBytes;
        string finalExtension;
        string finalContentType;

#if IOS
        // La camara de iOS entrega HEIC (formato de Apple que el backend no procesa). Lo
        // convertimos a JPEG con la API NATIVA de iOS (UIImage/AsJPEG, motor Core Graphics de
        // Apple) -- confiable, a diferencia de Microsoft.Maui.Graphics que corrompia la imagen.
        // Asi iOS termina subiendo JPEG igual que Android. Galeria (que ya da JPEG) NO entra aqui.
        if (esHeic)
        {
            using var uiImage = UIKit.UIImage.LoadFromData(Foundation.NSData.FromArray(originalBytes));
            using var jpegData = uiImage?.AsJPEG(0.85f);
            if (jpegData is not null && jpegData.Length > 0)
            {
                finalBytes = jpegData.ToArray();
                finalExtension = ".jpg";
                finalContentType = "image/jpeg";
            }
            else
            {
                // Si por algo la conversion no produce datos, caemos al original sin romper.
                finalBytes = originalBytes;
                finalExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
                finalContentType = ResolveContentType(finalExtension);
            }
        }
        else
        {
            finalBytes = originalBytes;
            finalExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
            finalContentType = string.IsNullOrWhiteSpace(result.ContentType)
                ? ResolveContentType(finalExtension)
                : result.ContentType;
        }
#else
        // Android/otros: la camara ya entrega JPEG. Se copia tal cual, sin re-codificar.
        finalBytes = originalBytes;
        finalExtension = string.IsNullOrWhiteSpace(extension) ? ".jpg" : extension;
        finalContentType = string.IsNullOrWhiteSpace(result.ContentType)
            ? ResolveContentType(finalExtension)
            : result.ContentType;
#endif

        var localPath = Path.Combine(_workingDirectory, $"{Guid.NewGuid():N}{finalExtension}");
        await File.WriteAllBytesAsync(localPath, finalBytes, cancellationToken).ConfigureAwait(false);

        var info = new FileInfo(localPath);
        if (info.Length == 0)
        {
            TryDelete(localPath);
            throw new InvalidOperationException(
                "La foto no se pudo leer del dispositivo. Intenta tomarla de nuevo o elígela desde la galería.");
        }

        return new EvidencePhoto(localPath, Path.GetFileName(localPath), finalContentType, info.Length);
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

    private static string ResolveContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".heic" or ".heif" => "image/heic",
        ".webp" => "image/webp",
        _ => "application/octet-stream"
    };
}
