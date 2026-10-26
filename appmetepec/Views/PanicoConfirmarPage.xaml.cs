using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

// Segunda confirmacion del boton de panico (ver PanicoPage para el primer paso, captura de
// datos): solo aqui se "detona" de verdad -- se crea el ticket hacia C2 y se inicia la llamada.
// Igual que ReportPage/AlertaNaranjaPage, el ticket es best-effort (no bloquea la llamada si
// falla, es una emergencia real): si la creacion del ticket truena, se avisa pero se llama
// de todas formas.
public partial class PanicoConfirmarPage : ContentPage
{
    // Aviso de un solo boton con el estilo de la app (DialogoPage). Naranja con "!" por defecto.
    private Task AvisoAsync(string titulo, string? mensaje, string? nota = null, string icono = "!",
        (Color Acento, Color Tenue)? colores = null, string boton = "Entendido") =>
        DialogoPage.MostrarAsync(Navigation, titulo, mensaje ?? "", boton, nota: nota, icono: icono, colores: colores);

    private readonly PreferencesService _preferences;
    private readonly MetepecApiService _api;
    private readonly NavigationState _navigationState;
    private PanicoDatos? _datos;

    private const int IdServicioPanico = 42; // "Servicio de emergencia"
    // Numero al que se marca al lanzar el servicio de emergencia (antes *7311).
    private const string NumeroLlamadaPanico = "7222084857";

    public PanicoConfirmarPage(PreferencesService preferences, MetepecApiService api, NavigationState navigationState)
    {
        InitializeComponent();
        _preferences = preferences;
        _api = api;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _datos = _navigationState.PanicoDatos;
        if (_datos is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        NombreLabel.Text = string.IsNullOrWhiteSpace(_datos.Name) ? "Sin nombre capturado" : _datos.Name;
        TelefonoLabel.Text = $"Tel: {_datos.Phone}";
        DireccionLabel.Text = string.IsNullOrWhiteSpace(_datos.Address) ? "Sin direccion capturada" : _datos.Address;
    }

    private async void OnConfirmarClicked(object sender, EventArgs e)
    {
        if (_datos is null) return;

        try
        {
            ConfirmarButton.IsEnabled = false;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = true;

            string? ticketId = null;
            try
            {
                ticketId = await CrearTicketPanicoAsync(_datos);
            }
            catch
            {
                // No bloquea la llamada: es una emergencia real, la prioridad es que se marque.
            }

            try
            {
                await Launcher.Default.OpenAsync($"tel:{NumeroLlamadaPanico}");
            }
            catch (Exception ex)
            {
                await AvisoAsync("No se pudo iniciar la llamada", ErrorMessageHelper.Traducir(ex), nota: "Marca directamente al 911 si la emergencia continúa.", colores: DialogoPage.Rojo);
            }

            _navigationState.PanicoDatos = null;

            // Con folio: exito (verde, folio destacado). Sin folio: aviso (rojo), la llamada si se hizo.
            if (ticketId is not null)
            {
                await DialogoPage.MostrarAsync(
                    Navigation,
                    titulo: "Emergencia reportada",
                    mensaje: "Se notificó a C2 con tu ubicación y datos de contacto. Tu folio es:",
                    destacado: $"{ticketId}",
                    botonPrincipal: "Entendido",
                    icono: "✓",
                    colores: DialogoPage.Verde);
            }
            else
            {
                await DialogoPage.MostrarAsync(
                    Navigation,
                    titulo: "Emergencia reportada",
                    mensaje: "No se pudo registrar el ticket, pero se inició la llamada de emergencia.",
                    botonPrincipal: "Entendido",
                    icono: "!",
                    colores: DialogoPage.Rojo);
            }
            await Shell.Current.GoToAsync($"//{nameof(HomePage)}");
        }
        finally
        {
            ConfirmarButton.IsEnabled = true;
            BusyIndicator.IsRunning = BusyIndicator.IsVisible = false;
        }
    }

    private async Task<string> CrearTicketPanicoAsync(PanicoDatos datos)
    {
        if (_preferences.CiudadanoId == 0)
        {
            _preferences.CiudadanoId = await _api.GetMyCiudadanoAsync() ?? 0;
        }

        var idCanalIngreso = 1; // APP: fallback si falla la consulta del catalogo
        try
        {
            var canalIngreso = await _api.GetCanalIngresoByClaveAsync(AppConstants.ClaveCanalIngresoApp);
            if (canalIngreso is not null)
            {
                idCanalIngreso = canalIngreso.Id;
            }
        }
        catch
        {
            // Sin bloquear el envio del ticket si el catalogo de canal de ingreso no responde.
        }

        var (latitud, longitud) = ParseCoordinates(datos.Coordinates);
        var request = new BackendCreateTicketRequest
        {
            Idciudadano = _preferences.CiudadanoId,
            IdCanalIngreso = idCanalIngreso,
            Asunto = "SERVICIO DE EMERGENCIA",
            Descripcion = "Servicio de emergencia activado desde la app.",
            Correoelectronico = datos.Email,
            Numerotelefonico = datos.Phone,
            Dependencia = ZendeskDependencia.C2.DisplayName(),
            Idservicio = IdServicioPanico,
            Ubicacion = new BackendTicketUbicacionRequest
            {
                Direccion = datos.Address,
                Coordenadas = datos.Coordinates,
                Latitud = latitud,
                Longitud = longitud
            },
            Servicios = [new BackendTicketServicioItemRequest { IdServicio = IdServicioPanico, EsPrincipal = true }],
            Observacion = new BackendTicketObservacionRequest
            {
                IdTipoMensaje = 1,
                Observaciones = "SERVICIO DE EMERGENCIA",
                VisibleCiudadano = true
            }
        };

        return await _api.CreateTicketAsync(request);
    }

    private async void OnCancelarClicked(object sender, EventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private static (decimal? Latitud, decimal? Longitud) ParseCoordinates(string coordinates)
    {
        var parts = coordinates.Split(',');
        if (parts.Length == 2
            && decimal.TryParse(parts[0].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var lat)
            && decimal.TryParse(parts[1].Trim(), System.Globalization.CultureInfo.InvariantCulture, out var lng))
        {
            return (lat, lng);
        }

        return (null, null);
    }
}
