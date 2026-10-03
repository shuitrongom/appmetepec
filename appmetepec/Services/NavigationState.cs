using appmetepec.Models;

namespace appmetepec.Services;

public sealed class NavigationState
{
    public ScreenReport? SelectedReport { get; set; }
    public NewsLetter? SelectedNews { get; set; }
    public BackendArticuloConocimientoDto? SelectedArticulo { get; set; }
    public BackendTicketDto? SelectedTicket { get; set; }
    public string? EncuestaClave { get; set; }
    // Precarga el correo en RecoverAccountPage cuando se llega desde "este correo ya existe" en el registro.
    public string? RecoverAccountEmail { get; set; }

    // Modulo de Eventos: evento elegido (para el mapa) y escenario tocado (para su programacion).
    public BackendEventoDto? SelectedEvento { get; set; }
    public BackendEscenarioDto? SelectedEscenario { get; set; }

    // Datos capturados en PanicoPage, para la segunda confirmacion en PanicoConfirmarPage antes
    // de detonar (crear el ticket y llamar). Null si se entra a PanicoConfirmarPage sin pasar
    // por PanicoPage primero (no deberia pasar, pero esa pagina lo valida igual).
    public PanicoDatos? PanicoDatos { get; set; }
}

public sealed record PanicoDatos(string Name, string Phone, string Email, string Address, string Coordinates);
