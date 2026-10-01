using appmetepec.Models;
using appmetepec.Services;
using Microsoft.Maui.Layouts;

namespace appmetepec.Views;

// Mapa interactivo de un evento: imagen con zoom (pinch) y desplazamiento (pan) acotado,
// y los numeros de cada escenario posicionados por PORCENTAJE sobre la imagen. Al tocar
// un numero se navega a la programacion de ese escenario. Funciona en iOS/Android y
// cualquier tamano porque las posiciones son relativas (%).
public partial class EventoMapaPage : ContentPage
{
    private const double PinDiametro = 36;      // tamano visual del pin
    private const double AreaTactil = 44;       // area minima de toque (accesibilidad)
    private const double EscalaMin = 1;
    private const double EscalaMax = 5;

    private readonly EventosService _eventos;
    private readonly NavigationState _navigationState;

    private BackendEventoDto? _evento;
    private readonly List<View> _pines = [];

    // Estado de zoom/pan.
    private double _escala = 1;
    private double _escalaInicio = 1;
    private double _panXInicio;
    private double _panYInicio;

    public EventoMapaPage(EventosService eventos, NavigationState navigationState)
    {
        InitializeComponent();
        _eventos = eventos;
        _navigationState = navigationState;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Con DI Transient cada navegacion crea una instancia nueva; cargamos una vez.
        if (_evento is not null)
        {
            return;
        }

        var seleccionado = _navigationState.SelectedEvento;
        if (seleccionado is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = seleccionado.Nombre;
        await CargarEventoAsync(seleccionado.Id);
    }

    private async Task CargarEventoAsync(int idEvento)
    {
        try
        {
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = true;
            ErrorPanel.IsVisible = false;

            _evento = await _eventos.GetEventoDetalleAsync(idEvento);
            if (_evento is null || string.IsNullOrWhiteSpace(_evento.ImagenUrl))
            {
                MostrarError();
                return;
            }

            DimensionarLienzo();

            // La imagen puede venir como URL del backend (http...) o como nombre de recurso
            // local de la app (datos de ejemplo). MAUI resuelve ambos: una URI absoluta se baja
            // de la red; cualquier otro valor se trata como recurso empaquetado (FromFile).
            MapaImagen.Source = Uri.TryCreate(_evento.ImagenUrl, UriKind.Absolute, out var uri)
                                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? ImageSource.FromUri(uri)
                : ImageSource.FromFile(_evento.ImagenUrl);

            DibujarHotspots();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Eventos] Error cargando evento: {ex}");
            MostrarError();
        }
        finally
        {
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
        }
    }

    // Fija el tamano del lienzo manteniendo la proporcion real de la imagen, para que los
    // porcentajes de los pines caigan donde deben. Si el backend no manda dimensiones, usa
    // una proporcion por defecto para no dejar el alto indefinido.
    private void DimensionarLienzo()
    {
        var ancho = Viewport.Width > 0 ? Viewport.Width
            : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;

        double proporcion = 1.3; // alto/ancho por defecto
        if (_evento is { ImagenAncho: > 0, ImagenAlto: > 0 })
        {
            proporcion = (double)_evento.ImagenAlto / _evento.ImagenAncho;
        }

        MapaLayout.WidthRequest = ancho;
        MapaLayout.HeightRequest = ancho * proporcion;
    }

    private void MostrarError()
    {
        ErrorPanel.IsVisible = true;
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
    }

    private async void OnReintentarClicked(object sender, EventArgs e)
    {
        var id = _navigationState.SelectedEvento?.Id ?? 0;
        _evento = null;
        if (id > 0)
        {
            await CargarEventoAsync(id);
        }
    }

    private void DibujarHotspots()
    {
        if (_evento is null) return;

        foreach (var pin in _pines)
        {
            MapaLayout.Children.Remove(pin);
        }
        _pines.Clear();

        foreach (var escenario in _evento.Escenarios)
        {
            var pin = CrearHotspot(escenario);

            // Clamp defensivo: datos del admin fuera de 0-100 no sacan el pin del mapa.
            var x = Math.Clamp(escenario.PosX, 0, 100) / 100.0;
            var y = Math.Clamp(escenario.PosY, 0, 100) / 100.0;

            AbsoluteLayout.SetLayoutFlags(pin, AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(pin,
                new Rect(x, y, AreaTactil, AreaTactil));

            MapaLayout.Children.Add(pin);
            _pines.Add(pin);
        }
    }

    // Area tactil de 44px (accesibilidad) con el pin visual de 36px centrado dentro.
    private View CrearHotspot(BackendEscenarioDto escenario)
    {
        var numero = new Label
        {
            Text = escenario.Numero.ToString(),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 15,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var pinVisual = new Border
        {
            WidthRequest = PinDiametro,
            HeightRequest = PinDiametro,
            BackgroundColor = Color.FromArgb("#5B2A86"),
            StrokeThickness = 2,
            Stroke = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(PinDiametro / 2) },
            Content = numero,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        // Contenedor del area tactil; se centra sobre el punto exacto desplazando media area.
        var contenedor = new Grid
        {
            WidthRequest = AreaTactil,
            HeightRequest = AreaTactil,
            BackgroundColor = Colors.Transparent,
            TranslationX = -AreaTactil / 2,
            TranslationY = -AreaTactil / 2,
            Children = { pinVisual }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await AbrirProgramacionAsync(escenario);
        contenedor.GestureRecognizers.Add(tap);

        return contenedor;
    }

    private async Task AbrirProgramacionAsync(BackendEscenarioDto escenario)
    {
        _navigationState.SelectedEscenario = escenario;
        await Shell.Current.GoToAsync(nameof(EscenarioProgramaPage));
    }

    // --- Zoom (pinch): escala la imagen; los pines se mantienen de tamano constante. ---
    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _escalaInicio = _escala;
                break;
            case GestureStatus.Running:
                _escala = Math.Clamp(_escalaInicio * e.Scale, EscalaMin, EscalaMax);
                AplicarEscala();
                AcotarPan();
                break;
        }
    }

    private void AplicarEscala()
    {
        MapaImagen.Scale = _escala;
        // Pines: escala inversa para que NO crezcan con el zoom (tamano constante en pantalla).
        var inversa = 1.0 / _escala;
        foreach (var pin in _pines)
        {
            pin.Scale = inversa;
        }
    }

    // --- Pan acotado al area visible segun la escala ---
    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_escala <= 1)
        {
            MapaLayout.TranslationX = 0;
            MapaLayout.TranslationY = 0;
            return;
        }

        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panXInicio = MapaLayout.TranslationX;
                _panYInicio = MapaLayout.TranslationY;
                break;
            case GestureStatus.Running:
                MapaLayout.TranslationX = _panXInicio + e.TotalX;
                MapaLayout.TranslationY = _panYInicio + e.TotalY;
                AcotarPan();
                break;
        }
    }

    // Limita la traslacion para que la imagen ampliada no se salga de la vista.
    private void AcotarPan()
    {
        var maxX = Math.Max(0, (MapaLayout.Width * (_escala - 1)) / 2);
        var maxY = Math.Max(0, (MapaLayout.Height * (_escala - 1)) / 2);
        MapaLayout.TranslationX = Math.Clamp(MapaLayout.TranslationX, -maxX, maxX);
        MapaLayout.TranslationY = Math.Clamp(MapaLayout.TranslationY, -maxY, maxY);
    }

    private void OnResetZoomTapped(object sender, TappedEventArgs e)
    {
        _escala = 1;
        MapaImagen.Scale = 1;
        MapaLayout.TranslationX = 0;
        MapaLayout.TranslationY = 0;
        foreach (var pin in _pines)
        {
            pin.Scale = 1;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
