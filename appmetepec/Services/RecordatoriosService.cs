using System.Globalization;
using appmetepec.Models;
using Plugin.LocalNotification;

namespace appmetepec.Services;

// Servicio de RECORDATORIOS LOCALES del modulo de Eventos.
//
// Servicio HERMANO de FavoritosService (FASE 1). Cuando el ciudadano marca una actividad con
// la estrella (favorito LOCAL), ademas de guardarla, programamos una NOTIFICACION LOCAL que le
// avisa 15 MINUTOS ANTES de la hora de inicio de esa actividad; al desmarcarla, cancelamos esa
// notificacion. TODO es LOCAL en el dispositivo: no hay backend, ni push remoto, ni red. El
// aviso lo dispara el propio sistema operativo aunque la app este cerrada.
//
// Esta clase ENCAPSULA Plugin.LocalNotification para no esparcir el plugin por la pagina: la
// pantalla solo habla con este servicio (AsegurarPermisoAsync / ProgramarRecordatorioActividad-
// Async / CancelarRecordatorioActividad) y nunca con el centro de notificaciones directamente.
//
// Convive con el push REMOTO de Firebase (Plugin.Firebase) sin reemplazarlo: Firebase son
// avisos del servidor; estos recordatorios son 100% locales y los agenda el ciudadano al
// marcar sus favoritos.
public sealed class RecordatoriosService
{
    // Minutos de antelacion del aviso respecto a la hora de inicio de la actividad.
    private const int MinutosAntelacion = 15;

    // Consulta y, si hace falta, SOLICITA el permiso de notificaciones del sistema. Devuelve
    // true si quedo concedido. En iOS y en Android 13+ (API 33) esto dispara el prompt del
    // sistema la primera vez. NUNCA lanza: ante cualquier error (p. ej. plataforma sin soporte)
    // devuelve false para que el llamador simplemente no programe, sin romper el flujo del
    // favorito (la Fase 1 guarda el favorito pase lo que pase).
    public async Task<bool> AsegurarPermisoAsync()
    {
        try
        {
            // Si ya estan habilitadas, no volvemos a pedir (no molestar al usuario).
            if (await LocalNotificationCenter.Current.AreNotificationsEnabled())
            {
                return true;
            }

            // Primera vez (o permiso revocado): se solicita. Devuelve si quedo concedido.
            return await LocalNotificationCenter.Current.RequestNotificationPermission();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Recordatorios] No se pudo asegurar el permiso: {ex}");
            return false;
        }
    }

    // Programa el recordatorio LOCAL de una actividad para 15 min antes de su hora de inicio.
    //
    // Reglas (documentadas, parte de "a la primera"):
    //  - Si la actividad NO tiene HoraInicio valida ("HH:mm"), NO se programa: sin hora no hay
    //    cuando avisar.
    //  - Se combina el DIA (actividad.Fecha) con la HORA (actividad.HoraInicio) y se resta la
    //    antelacion. Si la hora resultante del aviso YA PASO (<= ahora), NO se programa: no tiene
    //    sentido recordar algo cuyo aviso ya quedo en el pasado.
    //  - Se usa actividad.Id como NotificationId (int) para poder cancelarlo luego de forma
    //    DETERMINISTICA (misma actividad -> mismo id -> cancelacion exacta).
    //  - El cuerpo no incluye datos sensibles: solo la sede y la hora de inicio.
    public async Task ProgramarRecordatorioActividadAsync(BackendActividadDto actividad, string sedeNombre)
    {
        if (actividad is null)
        {
            return;
        }

        var horaInicio = ParsearHora(actividad.HoraInicio);
        if (horaInicio is null)
        {
            // Actividad sin hora de inicio valida: no hay cuando avisar.
            System.Diagnostics.Debug.WriteLine(
                $"[Recordatorios] Actividad {actividad.Id} sin HoraInicio valida; no se programa.");
            return;
        }

        // Fecha-hora exacta del inicio = dia de la actividad + hora de inicio.
        var inicio = actividad.Fecha.Date.Add(horaInicio.Value);
        var horaAviso = inicio.AddMinutes(-MinutosAntelacion);

        if (horaAviso <= DateTime.Now)
        {
            // El aviso ya quedo en el pasado: no se programa nada.
            System.Diagnostics.Debug.WriteLine(
                $"[Recordatorios] Actividad {actividad.Id}: el aviso ({horaAviso:g}) ya paso; no se programa.");
            return;
        }

        // Hora de inicio en texto "HH:mm" para el cuerpo del aviso (sin datos sensibles).
        var horaTexto = inicio.ToString("HH:mm", CultureInfo.InvariantCulture);

        var solicitud = new NotificationRequest
        {
            NotificationId = actividad.Id,
            Title = "Tu actividad está por comenzar",
            Description = $"{sedeNombre} · inicia a las {horaTexto}",
            Schedule =
            {
                // NotifyTime en v12 es DateTime (hora local del dispositivo).
                NotifyTime = horaAviso
            }
        };

        try
        {
            await LocalNotificationCenter.Current.Show(solicitud);
        }
        catch (Exception ex)
        {
            // Si el sistema no acepta la notificacion, no rompemos el flujo del favorito.
            System.Diagnostics.Debug.WriteLine(
                $"[Recordatorios] No se pudo programar el recordatorio de la actividad {actividad.Id}: {ex}");
        }
    }

    // Cancela el recordatorio programado de una actividad (si existia). Idempotente y seguro:
    // cancelar un id que no tiene notificacion pendiente no hace nada. Usa el mismo id que
    // ProgramarRecordatorioActividadAsync (actividad.Id) para una cancelacion deterministica.
    public void CancelarRecordatorioActividad(int actividadId)
    {
        try
        {
            LocalNotificationCenter.Current.Cancel(actividadId);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Recordatorios] No se pudo cancelar el recordatorio de la actividad {actividadId}: {ex}");
        }
    }

    // Parseo robusto de la hora "HH:mm" (o "HH:mm:ss") que entrega el backend a un TimeSpan.
    // Mismo criterio que usa la pagina (ActividadFormato.NormalizarHora / MinutosDesdeMedianoche):
    // se toman las dos primeras partes (hora y minuto). null/vacio/invalido -> null (sin hora).
    private static TimeSpan? ParsearHora(string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm))
        {
            return null;
        }

        var partes = hhmm.Split(':');
        if (partes.Length < 2)
        {
            return null;
        }

        if (int.TryParse(partes[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)
            && int.TryParse(partes[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var m)
            && h is >= 0 and <= 23
            && m is >= 0 and <= 59)
        {
            return new TimeSpan(h, m, 0);
        }

        return null;
    }
}
