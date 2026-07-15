using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class TicketDetailPage : ContentPage
{
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private BackendTicketDto? _ticket;

    public TicketDetailPage(MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _api = api;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _ticket = _navigationState.SelectedTicket;
        if (_ticket is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        AsuntoLabel.Text = _ticket.Asunto;
        EstatusLabel.Text = _ticket.Descestatus ?? "";
        EstatusChip.BackgroundColor = TryParseColor(_ticket.Colorestatus) ?? Color.FromArgb("#8A9BA8");
        ServicioLabel.Text = _ticket.Descservicio ?? "";
        DependenciaLabel.Text = _ticket.Dependencia ?? "";
        FolioLabel.Text = string.IsNullOrWhiteSpace(_ticket.Folio)
            ? _ticket.Fechaalta.ToString("dd/MM/yyyy HH:mm")
            : $"{_ticket.Folio}  •  {_ticket.Fechaalta:dd/MM/yyyy HH:mm}";

        await LoadObservacionesAsync(_ticket.Id);
        await VerificarEncuestaPendienteAsync();
    }

    private async Task VerificarEncuestaPendienteAsync()
    {
        if (_ticket is null ||
            !string.Equals(_ticket.Claveestatus, AppConstants.ClaveEstatusResuelto, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var tipoEncuesta = await _api.GetTipoEncuestaByClaveAsync(AppConstants.ClaveEncuestaSolucionTicket);
            if (tipoEncuesta is null)
            {
                return;
            }

            var yaContestada = await _api.ExisteEncuestaTicketAsync(_ticket.Id, tipoEncuesta.Id);
            if (yaContestada)
            {
                return;
            }

            var deseaContestar = await DisplayAlert(
                "Encuesta de satisfaccion",
                "Tu reporte ya fue resuelto. ¿Deseas contestar una breve encuesta sobre la solucion?",
                "Si", "Ahora no");

            if (deseaContestar)
            {
                await Shell.Current.GoToAsync(nameof(EncuestaPage));
            }
        }
        catch
        {
            // No bloquea la vista del ticket si falla la verificacion de encuesta.
        }
    }

    private async Task LoadObservacionesAsync(int idTicket)
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            ObservacionesView.ItemsSource = await _api.GetTicketObservacionesAsync(idTicket);
        }
        catch (Exception ex)
        {
            ObservacionesView.ItemsSource = Array.Empty<BackendTicketObservacionDto>();
            await DisplayAlert("No se pudieron cargar las respuestas", ex.Message, "Aceptar");
        }
        finally
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private static Color? TryParseColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return null;
        }

        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return null;
        }
    }
}
