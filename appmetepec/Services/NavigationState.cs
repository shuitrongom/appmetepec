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
}
