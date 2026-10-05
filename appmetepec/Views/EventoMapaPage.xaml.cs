using appmetepec.Models;
using appmetepec.Services;
using Microsoft.Maui.Layouts;

namespace appmetepec.Views;

// Mapa de un evento: la imagen del plano se muestra fija y completa (sin zoom ni pan), con los
// numeros de cada escenario posicionados por PORCENTAJE sobre la imagen. Los pines flotan con
// sombra para dar sensacion 3D y, al tocarlos, rebotan y navegan a la programacion del escenario.
// Funciona en iOS/Android y cualquier tamano porque las posiciones son relativas (%).
public partial class EventoMapaPage : ContentPage
{
    private const double PinDiametro = 36;      // tamano visual del pin
    private const double AreaTactil = 44;       // area minima de toque (accesibilidad)

    // Color por defecto del pin cuando el backend no define uno por escenario.
    private static readonly Color PinColorPorDefecto = Color.FromArgb("#5B2A86");

    private readonly EventosService _eventos;
    private readonly NavigationState _navigationState;

    private BackendEventoDto? _evento;
    private readonly List<View> _pines = [];
    // Relaciona cada escenario con su pin para reposicionarlo cuando cambie el tamano del lienzo.
    private readonly List<(BackendEscenarioDto Escenario, View Pin)> _pinPorEscenario = [];

    // Dimensiones REALES del archivo de imagen (medidas al cargar), no las del backend. Se usan
    // para la proporcion del lienzo: asi coincide exactamente con la imagen y los pines caen 1:1,
    // sin el minimo desfase que daban los enteros ImagenAncho/ImagenAlto si no eran exactos.
    private double _imgRealW;
    private double _imgRealH;

    // Duracion de la intro de portada antes de pasar sola al mapa.
    private static readonly TimeSpan DuracionIntro = TimeSpan.FromMilliseconds(2200);
    // La intro se muestra una sola vez por instancia de pagina (al cargar el evento).
    private bool _introMostrada;
    private CancellationTokenSource? _introCts;

    // Evita suscribir el SizeChanged del Viewport mas de una vez.
    private bool _viewportSuscrito;

    public EventoMapaPage(EventosService eventos, NavigationState navigationState)
    {
        InitializeComponent();
        _eventos = eventos;
        _navigationState = navigationState;
    }

    // Al cambiar el tamano del area del mapa (orientacion, primer layout, etc.) se recalcula el
    // lienzo para que la imagen siga entrando entera. El reposicionamiento de los pines se
    // dispara solo cuando el MapaLayout cambia de tamano (OnMapaLayoutSizeChanged).
    private void OnViewportSizeChanged(object? sender, EventArgs e)
    {
        if (_evento is null) return;
        DimensionarLienzo();
    }

    // Cuando el lienzo ya tiene (o cambia) su tamano real, recoloca los pines con coordenadas
    // absolutas exactas. Esto hace el posicionamiento inmune al timing del layout.
    private void OnMapaLayoutSizeChanged(object? sender, EventArgs e)
    {
        if (_evento is null) return;
        PosicionarHotspots();
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
            if (_evento is null || string.IsNullOrWhiteSpace(_evento.ImagenMapaUrl))
            {
                MostrarError();
                return;
            }

            // Recalcula el lienzo y reposiciona los pines ante cambios de tamano (una suscripcion).
            if (!_viewportSuscrito)
            {
                Viewport.SizeChanged += OnViewportSizeChanged;
                MapaLayout.SizeChanged += OnMapaLayoutSizeChanged;
                _viewportSuscrito = true;
            }

            // Mide las dimensiones REALES del archivo de imagen (no las del backend) para que la
            // proporcion del lienzo sea exacta. Si no se pudieran medir, cae a las del backend.
            await MedirImagenRealAsync();

            DimensionarLienzo();

            // La imagen puede venir como URL del backend (http...) o como nombre de recurso
            // local de la app (datos de ejemplo). MAUI resuelve ambos: una URI absoluta se baja
            // de la red; cualquier otro valor se trata como recurso empaquetado (FromFile).
            MapaImagen.Source = Uri.TryCreate(_evento.ImagenMapaUrl, UriKind.Absolute, out var uri)
                                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                ? ImageSource.FromUri(uri)
                : ImageSource.FromFile(_evento.ImagenMapaUrl);

            DibujarHotspots();

            // Intro de portada: una sola vez al abrir el evento, si lo trae.
            await MostrarIntroPortadaAsync();
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

    // Dimensiona el lienzo (MapaLayout) al rectangulo EXACTO que ocupa la imagen completa
    // dentro del area visible (Viewport), con la proporcion real de la imagen. Esto replica el
    // comportamiento de "fitBounds" del admin (Leaflet): la imagen entra entera, limitada por el
    // ancho O por el alto segun cual sea mas restrictivo, y el lienzo queda con la MISMA
    // proporcion que la imagen. Como la imagen llena el lienzo sin margenes (no sobra espacio
    // por AspectFit), los pines -posicionados en % sobre el lienzo- caen 1:1 sobre la imagen,
    // identico al admin. Se recalcula en cada cambio de tamano del Viewport (ver suscripcion en
    // CargarEventoAsync) para no depender del momento exacto de layout.
    private void DimensionarLienzo()
    {
        // Proporcion REAL del archivo si se pudo medir; si no, los enteros del backend.
        double imgW = _imgRealW > 0 ? _imgRealW : _evento?.ImagenAncho ?? 0;
        double imgH = _imgRealH > 0 ? _imgRealH : _evento?.ImagenAlto ?? 0;
        if (imgW <= 0 || imgH <= 0)
        {
            return;
        }

        // Tamano disponible del area del mapa. Si aun no esta medido, se difiere (SizeChanged
        // volvera a llamar cuando ya tenga dimensiones reales).
        var dispW = Viewport.Width;
        var dispH = Viewport.Height;
        if (dispW <= 0 || dispH <= 0)
        {
            return;
        }

        // "Fit": escala para que la imagen COMPLETA quepa en el viewport, limitada por el lado
        // mas restrictivo (igual que object-fit: contain / Leaflet fitBounds).
        var escala = Math.Min(dispW / imgW, dispH / imgH);

        MapaLayout.WidthRequest = imgW * escala;
        MapaLayout.HeightRequest = imgH * escala;
    }

    // Descarga/lee el archivo de imagen y obtiene su tamano REAL (ancho/alto en px) decodificando
    // con PlatformImage. Esto evita depender de ImagenAncho/ImagenAlto del backend, que pueden
    // tener redondeo y causar un leve desfase vertical de los pines. Best-effort: si falla, se
    // queda en 0 y DimensionarLienzo cae a los valores del backend.
    private async Task MedirImagenRealAsync()
    {
        _imgRealW = 0;
        _imgRealH = 0;
        var url = _evento?.ImagenMapaUrl;
        if (string.IsNullOrWhiteSpace(url)) return;

        try
        {
            Stream stream;
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                using var http = new HttpClient();
                var bytes = await http.GetByteArrayAsync(uri);
                stream = new MemoryStream(bytes);
            }
            else
            {
                stream = await FileSystem.OpenAppPackageFileAsync(url);
            }

            await using (stream)
            {
                var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(stream);
                if (image is not null && image.Width > 0 && image.Height > 0)
                {
                    _imgRealW = image.Width;
                    _imgRealH = image.Height;
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Eventos] No se pudo medir la imagen real: {ex}");
        }
    }

    private void MostrarError()
    {
        ErrorPanel.IsVisible = true;
        BusyIndicator.IsVisible = BusyIndicator.IsRunning = false;
    }

    // --- Intro de portada (una sola vez al abrir el evento) ---

    // Si el evento trae imagenPortadaUrl, muestra una intro a pantalla completa con fade +
    // zoom suave y la oculta sola tras DuracionIntro (o cuando el usuario toca). Si no hay
    // portada, no hace nada y el usuario ve el mapa directamente.
    private async Task MostrarIntroPortadaAsync()
    {
        if (_introMostrada || _evento is null) return;
        _introMostrada = true;

        var portada = _evento.ImagenPortadaUrl;
        if (string.IsNullOrWhiteSpace(portada))
        {
            return; // sin portada -> directo al mapa
        }

        // Igual que el mapa: URL absoluta -> red; cualquier otro valor -> recurso local.
        PortadaImagen.Source = Uri.TryCreate(portada, UriKind.Absolute, out var uri)
                               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? ImageSource.FromUri(uri)
            : ImageSource.FromFile(portada);

        // Token para que un toque pueda saltar la intro en cualquier momento (incluso durante
        // la animacion de entrada).
        _introCts = new CancellationTokenSource();

        // Estado inicial de la animacion: invisible y ligeramente ampliada (zoom-in al entrar).
        IntroOverlay.Opacity = 0;
        IntroOverlay.Scale = 1.08;
        IntroOverlay.IsVisible = true;

        await Task.WhenAll(
            IntroOverlay.FadeTo(1, 450, Easing.CubicOut),
            IntroOverlay.ScaleTo(1.0, 2200, Easing.CubicOut));

        // Espera el resto del tiempo de lucimiento; si el usuario toca, se cancela y sale ya.
        // El token puede estar ya cancelado si el toque llegó durante la animación de entrada.
        try
        {
            await Task.Delay(DuracionIntro, _introCts.Token);
        }
        catch (TaskCanceledException)
        {
            // El usuario saltó la intro con un toque: salimos sin esperar.
        }

        await OcultarIntroAsync();
    }

    // El toque para saltar la intro puede llegar en cualquier momento (incluso durante la
    // animacion de entrada). Cancelar el token hace que la espera termine ya; si el toque
    // llega antes del Delay, el token queda cancelado y el Delay sale de inmediato.
    private void OnSaltarIntroTapped(object sender, TappedEventArgs e)
    {
        _introCts?.Cancel();
    }

    private async Task OcultarIntroAsync()
    {
        if (!IntroOverlay.IsVisible) return;

        await IntroOverlay.FadeTo(0, 300, Easing.CubicIn);
        IntroOverlay.IsVisible = false;
        PortadaImagen.Source = null; // libera la imagen
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
        _pinPorEscenario.Clear();

        foreach (var escenario in _evento.Escenarios)
        {
            var pin = CrearHotspot(escenario);
            MapaLayout.Children.Add(pin);
            _pines.Add(pin);
            _pinPorEscenario.Add((escenario, pin));
        }

        PosicionarHotspots();
    }

    // Coloca cada pin con coordenadas ABSOLUTAS sobre el MapaLayout: el CENTRO del pin cae
    // exactamente en (posX%, posY%) del lienzo (que coincide 1:1 con la imagen). Se centra
    // restando media area tactil, el mismo criterio que el admin (left:x% + translate(-50%,-50%)).
    // NO se usa PositionProportional porque este introduce el tamano del pin en la ecuacion y
    // descuadra el punto. Se recalcula cuando cambia el tamano del lienzo (ver SizeChanged).
    private void PosicionarHotspots()
    {
        var w = MapaLayout.Width;
        var h = MapaLayout.Height;
        if (w <= 0 || h <= 0) return;

        foreach (var (escenario, pin) in _pinPorEscenario)
        {
            // Clamp defensivo: datos del admin fuera de 0-100 no sacan el pin del mapa.
            var fx = Math.Clamp(escenario.PosX, 0, 100) / 100.0;
            var fy = Math.Clamp(escenario.PosY, 0, 100) / 100.0;

            var cx = fx * w;
            var cy = fy * h;

            AbsoluteLayout.SetLayoutFlags(pin, AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(pin,
                new Rect(cx - (AreaTactil / 2), cy - (AreaTactil / 2), AreaTactil, AreaTactil));
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
            BackgroundColor = ResolverColor(escenario.Color),
            StrokeThickness = 2,
            Stroke = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = (float)(PinDiametro / 2) },
            Content = numero,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            // Sombra proyectada: da sensacion de que el pin FLOTA sobre el mapa (efecto 3D).
            Shadow = new Shadow
            {
                Brush = Brush.Black,
                Opacity = 0.45f,
                Radius = 8,
                Offset = new Point(0, 5)
            }
        };

        // Contenedor del area tactil. El centrado sobre el punto exacto lo hace DibujarHotspots
        // via los bounds absolutos (restando media area), NO con TranslationX/Y: asi hay un solo
        // mecanismo de centrado y el pin cae justo en posX%/posY% de la imagen, igual que el admin.
        var contenedor = new Grid
        {
            WidthRequest = AreaTactil,
            HeightRequest = AreaTactil,
            BackgroundColor = Colors.Transparent,
            Children = { pinVisual }
        };

        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            // Rebote de confirmacion al tocar (crece y vuelve) antes de abrir la programacion.
            await pinVisual.ScaleTo(1.35, 110, Easing.CubicOut);
            await pinVisual.ScaleTo(1.0, 90, Easing.CubicIn);
            await AbrirProgramacionAsync(escenario);
        };
        contenedor.GestureRecognizers.Add(tap);

        // Flotar sutil continuo sobre el circulo (no el contenedor, para no mover el area tactil
        // ni el posicionamiento). Da sensacion de que el pin flota sobre el plano (3D).
        IniciarFlotar(pinVisual);

        return contenedor;
    }

    // Lazo suave e infinito: el circulo del pin sube y baja unos pocos px. Se detiene solo
    // cuando el pin deja de estar en pantalla (pagina cerrada -> Parent nulo).
    private static async void IniciarFlotar(View pinVisual)
    {
        try
        {
            // Pequeño desfase aleatorio para que los pines no floten todos al unisono.
            await Task.Delay(Random.Shared.Next(0, 600));
            while (pinVisual.Parent is not null)
            {
                await pinVisual.TranslateTo(0, -4, 1200, Easing.SinInOut);
                await pinVisual.TranslateTo(0, 0, 1200, Easing.SinInOut);
            }
        }
        catch
        {
            // Si el pin se libera a mitad de la animacion, se ignora.
        }
    }

    private async Task AbrirProgramacionAsync(BackendEscenarioDto escenario)
    {
        _navigationState.SelectedEscenario = escenario;
        await Shell.Current.GoToAsync(nameof(EscenarioProgramaPage));
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        // Si la pagina se cierra con la intro aun en curso, cancela su espera y libera el token.
        _introCts?.Cancel();
        _introCts?.Dispose();
        _introCts = null;

        // Libera los handlers de tamano.
        if (_viewportSuscrito)
        {
            Viewport.SizeChanged -= OnViewportSizeChanged;
            MapaLayout.SizeChanged -= OnMapaLayoutSizeChanged;
            _viewportSuscrito = false;
        }
    }

    // Convierte el hex del backend ("#RRGGBB") en Color. Si viene vacio o invalido,
    // usa el color por defecto para no dejar pines sin pintar.
    private static Color ResolverColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return PinColorPorDefecto;
        }

        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return PinColorPorDefecto;
        }
    }
}
