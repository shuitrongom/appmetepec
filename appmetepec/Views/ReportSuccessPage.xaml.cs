namespace appmetepec.Views;

[QueryProperty(nameof(ReportId), "reportId")]
[QueryProperty(nameof(IsBache), "isBache")]
public partial class ReportSuccessPage : ContentPage
{
    public string ReportId { get; set; } = "";
    public string IsBache { get; set; } = "";

    public ReportSuccessPage()
    {
        InitializeComponent();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        var isBache = bool.TryParse(IsBache, out var value) && value;
        MessageLabel.Text = isBache
            ? "Tu solicitud fue recibida. Personal del Juzgado Civico de Metepec revisara el caso y dara seguimiento por correo."
            : "En breve nos comunicaremos contigo para darle seguimiento a tu solicitud.";
        ReportIdLabel.Text = ReportId is "0" or "" ? "" : $"Folio: {ReportId}";
    }

    private async void OnHomeClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
    }
}
