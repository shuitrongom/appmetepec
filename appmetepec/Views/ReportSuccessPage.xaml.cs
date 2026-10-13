using appmetepec.Services;

namespace appmetepec.Views;

[QueryProperty(nameof(ReportId), "reportId")]
[QueryProperty(nameof(IsBache), "isBache")]
public partial class ReportSuccessPage : ContentPage
{
    // Para los servicios sin Servicio.MensajeCierre capturado en la web (documento "Frases de
    // cierre para reportes de la app Metepec *7311", punto 37).
    private const string MensajeGenerico = "La Gerencia de la Ciudad revisará el caso y lo canalizará al área competente.";

    private readonly NavigationState _navigationState;

    public string ReportId { get; set; } = "";
    // Ya no se usa (ver mensaje anterior comentado en OnAppearing); se conserva para no romper la
    // navegacion desde ReportPage.
    public string IsBache { get; set; } = "";

    public ReportSuccessPage(NavigationState navigationState)
    {
        InitializeComponent();
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        // Mensajes anteriores, reemplazados por la frase de cierre de cada servicio:
        // var isBache = bool.TryParse(IsBache, out var value) && value;
        // MessageLabel.Text = isBache
        //     ? "Tu solicitud fue recibida. Personal del Juzgado Civico de Metepec revisara el caso y dara seguimiento por correo."
        //     : "En breve nos comunicaremos contigo para darle seguimiento a tu solicitud.";
        // ReportIdLabel.Text = ReportId is "0" or "" ? "" : $"Folio: {ReportId}";

        MessageLabel.Text = _navigationState.SelectedReport?.MensajeCierre ?? MensajeGenerico;
        ReportIdLabel.Text = ReportId is "0" or "" ? "" : $"Ticket: {ReportId}";
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        await IrAlInicioAsync();
    }

    // El UNICO camino de salida de esta pantalla es ir al INICIO (ruta absoluta //HomePage, que
    // RESETEA el stack de navegacion). Nunca se vuelve a ReportPage: hacerlo mostraba el reporte
    // con los datos ya capturados y permitia pulsar ENVIAR otra vez, generando tickets DUPLICADOS
    // en segundos (el bug reportado). Un reporte nuevo debe empezar siempre en blanco.
    private async Task IrAlInicioAsync()
    {
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
    }

    // Intercepta el boton/gesto "atras" del sistema (Android) y el gesto de regreso: en vez de
    // volver a ReportPage con los datos capturados (que permitiria reenviar el mismo ticket), lo
    // redirige SIEMPRE al inicio. Devolver true indica que ya manejamos el "atras" y Shell NO hace
    // el pop por defecto. La flecha de la barra de Shell ya se oculto con Shell.NavBarIsVisible.
    protected override bool OnBackButtonPressed()
    {
        _ = IrAlInicioAsync();
        return true;
    }
}
