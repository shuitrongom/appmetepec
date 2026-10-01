using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

// Mapa interactivo de un evento: muestra la imagen (con zoom/pan) y los numeros de cada
// escenario posicionados por PORCENTAJE sobre la imagen (hotspots). Al tocar un numero se
// navega a la programacion de ese escenario. Funciona en iOS/Android y cualquier tamano
// porque las posiciones son relativas (%), no pixeles fijos.
public partial class EventoMapaPage : ContentPage
{
    private readonly EventosService _eventos;
    private readonly NavigationState _navigationState;

    private BackendEventoDto? _evento;
    private readonly List<(View Hotspot, BackendEscenarioDto Escenario)> _hotspots = [];

    // Estado de zoom/pan.
    private double _escalaActual = 1;
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

        // Si ya cargamos este evento, no recargar (p.ej. al volver de la programacion).
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

            _evento = await _eventos.GetEventoDetalleAsync(idEvento);
            if (_evento is null)
            {
                await DisplayAlert("Eventos", "No se pudo cargar el evento.", "Aceptar");
                await Shell.Current.GoToAsync("..");
                return;
            }

            MapaImagen.Source = _evento.ImagenUrl;

            // Mantener la proporcion real de la imagen en el layout, para que los % de los
            // hotspots caigan donde deben.
            if (_evento.ImagenAncho > 0 && _evento.ImagenAlto > 0)
            {
                var anchoPantalla = Width > 0 ? Width : DeviceDisplay.MainDisplayInfo.Width / DeviceDisplay.MainDisplayInfo.Density;
                var alto = anchoPantalla * _evento.ImagenAlto / _evento.ImagenAncho;
                MapaLayout.WidthRequest = anchoPantalla;
                MapaLayout.HeightRequest = alto;
            }

            DibujarHotspots();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Eventos", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
        }
    }

    private void DibujarHotspots()
    {
        if (_evento is null) return;

        // Limpiar hotspots previos (deja la imagen, que es el primer hijo).
        foreach (var (hotspot, _) in _hotspots)
        {
            MapaLayout.Children.Remove(hotspot);
        }
        _hotspots.Clear();

        foreach (var escenario in _evento.Escenarios)
        {
            var boton = CrearHotspot(escenario);
            // Posicion por proporcion (0-1) usando el % del escenario. LayoutFlags=All hace
            // que X/Y sean relativos al tamano del contenedor, asi escala con zoom y pantalla.
            AbsoluteLayout.SetLayoutFlags(boton, Microsoft.Maui.Layouts.AbsoluteLayoutFlags.PositionProportional);
            AbsoluteLayout.SetLayoutBounds(boton,
                new Rect(escenario.PosX / 100.0, escenario.PosY / 100.0,
                         AbsoluteLayout.AutoSize, AbsoluteLayout.AutoSize));

            MapaLayout.Children.Add(boton);
            _hotspots.Add((boton, escenario));
        }
    }

    // Un numero circular, estilo "pin", que responde al toque.
    private View CrearHotspot(BackendEscenarioDto escenario)
    {
        var label = new Label
        {
            Text = escenario.Numero.ToString(),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 16,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var circulo = new Border
        {
            WidthRequest = 34,
            HeightRequest = 34,
            BackgroundColor = Color.FromArgb("#5B2A86"),
            StrokeThickness = 2,
            Stroke = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 17 },
            Content = label,
            // El pin se centra sobre el punto exacto (desplazamos media medida).
            TranslationX = -17,
            TranslationY = -17
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) => await AbrirProgramacionAsync(escenario);
        circulo.GestureRecognizers.Add(tap);

        return circulo;
    }

    private async Task AbrirProgramacionAsync(BackendEscenarioDto escenario)
    {
        _navigationState.SelectedEscenario = escenario;
        await Shell.Current.GoToAsync(nameof(EscenarioProgramaPage));
    }

    // --- Zoom (pinch) ---
    private void OnPinchUpdated(object? sender, PinchGestureUpdatedEventArgs e)
    {
        switch (e.Status)
        {
            case GestureStatus.Started:
                _escalaInicio = _escalaActual;
                break;
            case GestureStatus.Running:
                // Limitar el zoom entre 1x y 5x.
                _escalaActual = Math.Clamp(_escalaInicio * e.Scale, 1, 5);
                MapaLayout.Scale = _escalaActual;
                break;
        }
    }

    // --- Desplazamiento (pan) cuando esta ampliado ---
    private void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (_escalaActual <= 1)
        {
            // Sin zoom no hay nada que desplazar.
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
                break;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }
}
