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
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
    }
}
