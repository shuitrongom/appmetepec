using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class TicketDetailPage : ContentPage
{
    private static readonly TimeSpan EsperaEncuestaPendiente = TimeSpan.FromMinutes(2);

    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private BackendTicketDto? _ticket;
    private bool _paginaVisible;
    private bool _encuestaPromptMostrado;

    public TicketDetailPage(MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _api = api;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _paginaVisible = true;
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
        IniciarTemporizadorEncuestaPendiente();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _paginaVisible = false;
    }

    protected override bool OnBackButtonPressed()
    {
        _ = SalirConEncuestaPendienteAsync();
        return true;
    }

    // Ya no se muestra la encuesta al entrar al detalle: se espera EsperaEncuestaPendiente
    // mientras el ciudadano sigue en la pantalla, o se dispara antes al intentar regresar
    // (boton de la app o boton fisico de Android), lo que ocurra primero.
    private void IniciarTemporizadorEncuestaPendiente()
    {
        Dispatcher.StartTimer(EsperaEncuestaPendiente, () =>
        {
            if (_paginaVisible)
            {
                _ = VerificarEncuestaPendienteAsync();
            }

            return false;
        });
    }

    private async Task SalirConEncuestaPendienteAsync()
    {
        var abrioEncuesta = await VerificarEncuestaPendienteAsync();
        if (!abrioEncuesta)
        {
            await Shell.Current.GoToAsync("..");
        }
    }

    private async Task<bool> VerificarEncuestaPendienteAsync()
    {
        if (_encuestaPromptMostrado)
        {
            return false;
        }

        if (_ticket is null ||
            !string.Equals(_ticket.Claveestatus, AppConstants.ClaveEstatusResuelto, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var tipoEncuesta = await _api.GetTipoEncuestaByClaveAsync(AppConstants.ClaveEncuestaSolucionTicket);
            if (tipoEncuesta is null)
            {
                return false;
            }

            var yaContestada = await _api.ExisteEncuestaTicketAsync(_ticket.Id, tipoEncuesta.Id);
            if (yaContestada)
            {
                return false;
            }

            _encuestaPromptMostrado = true;

            var deseaContestar = await DisplayAlert(
                "Encuesta de satisfaccion",
                "Tu reporte ya fue resuelto. ¿Deseas contestar una breve encuesta sobre la solucion?",
                "Si", "Ahora no");

            if (!deseaContestar)
            {
                return false;
            }

            _navigationState.EncuestaClave = AppConstants.ClaveEncuestaSolucionTicket;
            await Shell.Current.GoToAsync(nameof(EncuestaPage));
            return true;
        }
        catch
        {
            // No bloquea la vista del ticket si falla la verificacion de encuesta.
            return false;
        }
    }

    private async Task LoadObservacionesAsync(int idTicket)
    {
        try
        {
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;
            var observaciones = await _api.GetTicketObservacionesAsync(idTicket);
            ObservacionesView.ItemsSource = observaciones
                .Where(obs => string.Equals(obs.Clavetipomensaje, AppConstants.ClaveRespuestaPublica, StringComparison.OrdinalIgnoreCase))
                .ToList();
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
        await SalirConEncuestaPendienteAsync();
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
