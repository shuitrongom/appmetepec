using appmetepec.Views;

namespace appmetepec.Services;

// Al tocar una notificacion push que trae "idTicket" (respuesta publica o invitacion a la encuesta
// al cerrar el reporte, ver TicketService.InvitarEncuestaSiAplicaAsync) se abre el detalle de ese
// reporte; si su estatus permite encuesta, TicketDetailPage la ofrece ahi mismo.
// El toque puede llegar antes de que haya sesion o de que Shell este listo (app cerrada): el id se
// guarda como pendiente y se procesa en cuanto HomePage aparece (con la app abierta, de inmediato).
public sealed class AperturaTicketPushService
{
    private readonly MetepecApiService _api;
    private readonly PreferencesService _preferences;
    private readonly NavigationState _navigationState;
    private int? _idTicketPendiente;
    private bool _pendienteEsEncuesta;
    private bool _procesando;

    public AperturaTicketPushService(MetepecApiService api, PreferencesService preferences, NavigationState navigationState)
    {
        _api = api;
        _preferences = preferences;
        _navigationState = navigationState;
    }

    public void RegistrarToque(IDictionary<string, string>? datos)
    {
        if (datos is null || !datos.TryGetValue("idTicket", out var valor) || !int.TryParse(valor, out var idTicket)) return;
        _idTicketPendiente = idTicket;
        _pendienteEsEncuesta = datos.TryGetValue("encuesta", out var encuesta) && encuesta == "1";
        MainThread.BeginInvokeOnMainThread(async () => await ProcesarPendienteAsync());
    }

    // true si abrio un reporte (quien llama no debe navegar a otra parte en ese momento).
    public async Task<bool> ProcesarPendienteAsync()
    {
        if (_procesando || _idTicketPendiente is not { } idTicket) return false;
        // Sin sesion (o Shell aun sin cargar): se queda pendiente para despues del login.
        if (string.IsNullOrWhiteSpace(_preferences.JwtToken) || Shell.Current is null) return false;
        // Mientras se muestra el splash, el login o el registro, se espera a llegar a Home.
        if (Shell.Current.CurrentPage is null or SplashPage or LoginPage or RegisterPage or RecoverAccountPage) return false;

        _procesando = true;
        try
        {
            var ticket = (await _api.GetMyTicketsAsync()).FirstOrDefault(t => t.Id == idTicket);
            _idTicketPendiente = null;
            if (ticket is null) return false; // No es de este ciudadano o ya no existe.

            _navigationState.SelectedTicket = ticket;
            _navigationState.OfrecerEncuestaAlEntrar = _pendienteEsEncuesta;
            await Shell.Current.GoToAsync(nameof(TicketDetailPage));
            return true;
        }
        catch
        {
            // Sin conexion: se descarta para no reintentar en bucle; el reporte sigue en "Mis reportes".
            _idTicketPendiente = null;
            return false;
        }
        finally
        {
            _procesando = false;
        }
    }
}
