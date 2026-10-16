using System.Globalization;
using appmetepec.Models;
using Plugin.LocalNotification;
// v14 (.NET 10): NotificationRequest y su Schedule se movieron a Core.Models.
using Plugin.LocalNotification.Core.Models;

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
    // TRES recordatorios por actividad favorita, a distinta antelacion de la hora de inicio:
    // un DIA antes, una HORA antes y 15 MINUTOS antes. El ciudadano recibe el aviso con tiempo
    // para planear (un dia), para trasladarse (una hora) y justo antes de empezar (15 min). Solo
    // se programan los que caen en el futuro al momento de marcar el favorito (ver Programar...).
    private sealed record Antelacion(int Offset, TimeSpan Antes, string Titulo, Func<string, string, string> Cuerpo);

    // Offset del NotificationId para que cada antelacion tenga un id UNICO y DETERMINISTA por
    // actividad: id_notificacion = actividad.Id * MultiplicadorId + Offset. Asi, de la misma
    // actividad salen tres ids estables y distintos, y se pueden cancelar con exactitud. El
    // multiplicador deja hueco (10) entre actividades para que los offsets (0,1,2) nunca choquen
    // con el id base de otra actividad.
    private const int MultiplicadorId = 10;

    private static readonly Antelacion[] Antelaciones =
    [
        new(Offset: 0, Antes: TimeSpan.FromDays(1), Titulo: "Mañana tienes una actividad",
            Cuerpo: (sede, hora) => $"{sede} · mañana a las {hora}"),
        new(Offset: 1, Antes: TimeSpan.FromHours(1), Titulo: "Tu actividad es en una hora",
            Cuerpo: (sede, hora) => $"{sede} · inicia a las {hora}"),
        new(Offset: 2, Antes: TimeSpan.FromMinutes(15), Titulo: "Tu actividad está por comenzar",
            Cuerpo: (sede, hora) => $"{sede} · inicia a las {hora}"),
    ];

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

    // Programa los recordatorios LOCALES de una actividad: un DIA antes, una HORA antes y 15 MIN
    // antes de su hora de inicio.
    //
    // Reglas (documentadas, parte de "a la primera"):
    //  - Si la actividad NO tiene HoraInicio valida ("HH:mm"), NO se programa nada: sin hora no
    //    hay cuando avisar.
    //  - Para CADA una de las tres antelaciones se calcula su hora de aviso. Solo se programan las
    //    que caen en el FUTURO (> ahora): si al marcar el favorito faltan 30 min para la actividad,
    //    el aviso de "un dia antes" y "una hora antes" ya pasaron -> se omiten, y solo entra el de
    //    15 min. Asi nunca llega un aviso con la hora en el pasado.
    //  - Cada aviso usa un NotificationId UNICO y DETERMINISTA (actividad.Id * 10 + offset) para
    //    poder cancelarlo luego con exactitud.
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
        // Hora de inicio en texto "HH:mm" para el cuerpo del aviso (sin datos sensibles).
        var horaTexto = inicio.ToString("HH:mm", CultureInfo.InvariantCulture);

        foreach (var antelacion in Antelaciones)
        {
            var horaAviso = inicio - antelacion.Antes;
            if (horaAviso <= DateTime.Now)
            {
                // Este aviso en particular ya quedo en el pasado: se omite (los otros si pueden
                // entrar; p. ej. el de 15 min aunque el de 1 dia ya haya pasado).
                continue;
            }

            var solicitud = new NotificationRequest
            {
                NotificationId = (actividad.Id * MultiplicadorId) + antelacion.Offset,
                Title = antelacion.Titulo,
                Description = antelacion.Cuerpo(sedeNombre, horaTexto),
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
                // Si el sistema no acepta una notificacion, no rompemos el flujo del favorito ni
                // el resto de avisos de la misma actividad.
                System.Diagnostics.Debug.WriteLine(
                    $"[Recordatorios] No se pudo programar el aviso ({antelacion.Antes}) de la actividad {actividad.Id}: {ex}");
            }
        }
    }

    // Cancela TODOS los recordatorios programados de una actividad (un dia, una hora, 15 min), si
    // existian. Idempotente y seguro: cancelar un id sin notificacion pendiente no hace nada. Usa
    // el mismo esquema de ids (actividad.Id * 10 + offset) que al programar, para una cancelacion
    // deterministica de los tres.
    public void CancelarRecordatorioActividad(int actividadId)
    {
        try
        {
            foreach (var antelacion in Antelaciones)
            {
                LocalNotificationCenter.Current.Cancel((actividadId * MultiplicadorId) + antelacion.Offset);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine(
                $"[Recordatorios] No se pudo cancelar los recordatorios de la actividad {actividadId}: {ex}");
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
