using appmetepec.Models;
using appmetepec.Services;
using Microsoft.Maui.Devices.Sensors;
using Microsoft.Maui.Layouts;

namespace appmetepec.Views;

// Mapa de un evento: la imagen del plano se muestra completa (sin zoom ni pan) con un efecto
// PARALLAX 3D que reacciona al giroscopio/acelerometro del telefono: al inclinar el dispositivo,
// el mapa rota sutilmente en 3D y los pines se desplazan a otra profundidad, dando la ilusion de
// una maqueta. Los numeros de cada escenario se posicionan por PORCENTAJE sobre la imagen, flotan
// con sombra y rebotan al tocarlos antes de navegar a la programacion. Funciona en iOS/Android.
public partial class EventoMapaPage : ContentPage
{
    private const double PinDiametro = 36;      // tamano visual del pin
    private const double AreaTactil = 44;       // area minima de toque (accesibilidad)

    // --- Parametros del efecto parallax 3D ---
    // Inclinacion maxima del mapa en grados (topes suaves para que NUNCA se vea deforme).
    // 9 en vez de 10: un grado menos de tope reduce la sensacion de deformacion en los bordes
    // manteniendo una profundidad clara.
    private const double TiltMaxGrados = 9;
    // Factor BASE de parallax: desplazamiento maximo (px) de la capa MAS cercana (el pin del
    // frente). Las capas al fondo se mueven menos, segun la formula por profundidad (ver
    // CalcularFactorPin). Da el efecto 3D por capas: >0 = flotan por encima del mapa.
    private const double PinParallaxFactor = 16;
    // Suavizado: fraccion del movimiento que se aplica por frame (0-1). Mas bajo = mas suave.
    // 0.14: un pelo mas de respuesta ("mantequilla" sin lag), sigue sintiendose suave.
    private const double Suavizado = 0.14;
    // Zoom sutil (<=3%) al inclinar: refuerza la sensacion de "maqueta" sin deformar, porque es
    // una escala UNIFORME (no estira). A tilt 0 queda en Scale=1 exacto.
    private const double EscalaMaxExtra = 0.03;
    // Desplazamiento maximo (px) del offset de la sombra de los pines al inclinar: luz dinamica
    // discreta. A tilt 0 vuelve al offset base (0,5).
    private const double SombraOffsetMax = 4;
    // Umbral de frame (grados): si el delta de inclinacion por frame es menor a esto en ambos
    // ejes y ya se alcanzo el reposo, no se reasigna nada (evita trabajo redundante por frame).
    private const double UmbralFrame = 0.05;
    // Offset vertical base de la sombra de los pines (coincide con el Offset del Shadow al crear).
    private const double SombraBaseY = 5;

    // Color por defecto del pin cuando el backend no define uno por escenario.
    private static readonly Color PinColorPorDefecto = Color.FromArgb("#5B2A86");

    private readonly EventosService _eventos;
    private readonly NavigationState _navigationState;

    private BackendEventoDto? _evento;
    private readonly List<View> _pines = [];
    // Relaciona cada escenario con su pin (contenedor tactil), el circulo interior (Border) y su
    // factor de parallax por capa (derivado de PosY). El contenedor recibe el parallax; el circulo
    // interior recibe el flotar y la sombra dinamica. Son elementos DISTINTOS: cada animacion
    // escribe su propia propiedad, por lo que no pelean por TranslationX/Y ni por el centrado.
    private readonly List<(BackendEscenarioDto Escenario, View Contenedor, Border Circulo, double FactorPin)> _pinPorEscenario = [];

    // Dimensiones REALES del archivo de imagen (medidas al cargar), no las del backend. Se usan
    // para la proporcion del lienzo: asi coincide exactamente con la imagen y los pines caen 1:1,
    // sin el minimo desfase que daban los enteros ImagenAncho/ImagenAlto si no eran exactos.
    private double _imgRealW;
    private double _imgRealH;

    // Tamano REAL del lienzo (MapaLayout) ya calculado por DimensionarLienzo: imgW*escala x
    // imgH*escala. Es la FUENTE DE VERDAD para posicionar los pines. En iOS leer MapaLayout.Width
    // dentro de SizeChanged devuelve el tamano final, pero en Android ese valor puede llegar en
    // 0/provisional o en otro orden (ver dotnet/maui #10747: en Android SizeChanged se levanta
    // tras OnLoaded, no antes). Posicionar leyendo el tamano arreglado llevaba a colocar los pines
    // con w=h=0 -> todos apilados en (0,0) y, al estar apilados y fuera de su area real, el toque
    // en reposo no caia sobre ellos. Guardando aqui el tamano calculado posicionamos SIEMPRE con
    // el valor correcto, sin depender del readback de layout de la plataforma.
    private double _lienzoW;
    private double _lienzoH;

    // Duracion de la intro de portada antes de pasar sola al mapa.
    private static readonly TimeSpan DuracionIntro = TimeSpan.FromMilliseconds(2200);
    // La intro se muestra una sola vez por instancia de pagina (al cargar el evento).
    private bool _introMostrada;
    // true si el evento trae portada y por tanto se mostro el overlay (hay que esperarlo/ocultarlo).
    private bool _introConPortada;
    private CancellationTokenSource? _introCts;

    // Evita suscribir el SizeChanged del Viewport mas de una vez.
    private bool _viewportSuscrito;

    // --- Estado del parallax 3D ---
    // Inclinacion objetivo (segun el sensor) y la actual (suavizada hacia el objetivo).
    private double _tiltObjetivoX, _tiltObjetivoY;
    private double _tiltActualX, _tiltActualY;
    private bool _acelerometroActivo;
    // Lazo de animacion que interpola la inclinacion actual hacia la objetivo (suave).
    private bool _loopParallaxActivo;
    // Generacion del lazo: cada arranque la incrementa. Un lazo viejo que siga en vuelo tras un
    // await detecta que su generacion caduco y termina solo. Blinda contra loops DUPLICADOS si
    // OnAppearing/CargarEventoAsync se repiten o tras un reintento.
    private int _generacionLoop;
    // El efecto ya se inicializo al menos una vez (hay pines dibujados). Permite distinguir
    // "arrancar por primera vez" de "reanudar" al volver de un escenario.
    private bool _parallaxInicializado;
    // Controla el flotar de los circulos: al pausar la pagina se baja para detener los lazos de
    // flotar (y no trabar la transicion); al reanudar se sube y se relanzan.
    private bool _flotarActivo;
    // Generacion del flotar: cada arranque/relanzamiento la incrementa. Igual que _generacionLoop
    // del parallax, blinda contra lazos de flotar DUPLICADOS: un lazo viejo que siga en vuelo tras
    // un await TranslateTo detecta que su generacion caduco y termina solo, aunque _flotarActivo
    // ya se haya vuelto a subir en ReanudarParallax3D. Evita que el flote se acelere al entrar y
    // volver de un escenario varias veces.
    private int _generacionFlotar;
    // El dispositivo tiene acelerometro utilizable. Si es false (emulador), el mapa queda PLANO
    // y estable y ni siquiera se corre el lazo (ahorro en emulador).
    private bool _sensorDisponible;
    // La animacion de ENTRADA premium se ejecuta una sola vez por instancia de pagina.
    private bool _entradaMostrada;
    // Ultimos valores de inclinacion con los que se recalculo la sombra (para gatear por umbral).
    private double _tiltSombraX = double.NaN, _tiltSombraY = double.NaN;

    public EventoMapaPage(EventosService eventos, NavigationState navigationState)
    {
        InitializeComponent();
        _eventos = eventos;
        _navigationState = navigationState;

        // El tope de alto de la lista es PROPORCIONAL al alto de la pagina (~33%), recalculado
        // ante cambios de tamano/orientacion. No es un alto fijo por numero de escenarios.
        SizeChanged += OnPageSizeChanged;
    }

    // Fija el tope de alto de la zona de lista como ~33% del alto de la pagina (clamp defensivo a
    // un minimo razonable). Con pocos escenarios la lista mide menos que el tope (fila Auto chica,
    // mapa grande); con muchos llega al tope y scrollea internamente, sin empujar el mapa fuera de
    // pantalla. Al cambiar el alto del Viewport, su SizeChanged re-dispara DimensionarLienzo.
    private void OnPageSizeChanged(object? sender, EventArgs e)
    {
        if (Height <= 0) return;
        ListaEscenariosScroll.MaximumHeightRequest = Math.Max(120, Height * 0.33);
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

    // En Android el arbol nativo puede quedar montado (Loaded) antes de que el tamano del lienzo
    // se refleje via SizeChanged. Al dispararse Loaded recalculamos el lienzo (que a su vez
    // reposiciona los pines con el tamano real) y, por si acaso, reposicionamos explicitamente.
    // Garantiza que el PRIMER render en Android ya tenga los pines en su sitio.
    private void OnMapaLayoutLoaded(object? sender, EventArgs e)
    {
        if (_evento is null) return;
        DimensionarLienzo();
        PosicionarHotspots();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Con DI Transient cada navegacion crea una instancia nueva; cargamos una vez. Cuando se
        // vuelve de un escenario (misma instancia reutilizada por el Shell), _evento ya existe:
        // en ese caso NO recargamos, pero reanudamos el efecto SUAVE (continua desde el tilt
        // actual, sin reiniciar a 0 ni parpadear).
        if (_evento is not null)
        {
            ReanudarParallax3D();
            return;
        }

        var seleccionado = _navigationState.SelectedEvento;
        if (seleccionado is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = seleccionado.Nombre;

        // PRIMERO la portada (si el evento la trae): se muestra de inmediato, a pantalla completa,
        // mientras el mapa carga por DETRAS. Asi el orden que ve el ciudadano es siempre
        // Portada -> Mapa, nunca Mapa(cargando) -> Portada -> Mapa. La portada la conocemos ya
        // desde el listado (SelectedEvento), sin esperar el detalle.
        MostrarIntroPortadaInmediata(seleccionado.ImagenPortadaUrl);

        await CargarEventoAsync(seleccionado.Id);
    }

    private async Task CargarEventoAsync(int idEvento)
    {
        try
        {
            // El mapa arranca invisible: no mostramos spinner sobre la portada; mientras la
            // portada luce, el mapa carga detras y se revela con la entrada premium.
            BusyIndicator.IsVisible = BusyIndicator.IsRunning = !IntroOverlay.IsVisible;
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
                // En Android el SizeChanged se levanta DESPUES de Loaded (no antes como en otras
                // plataformas, ver dotnet/maui #10747). Nos enganchamos tambien a Loaded del
                // lienzo para recalcular tamano y reposicionar los pines en cuanto el arbol nativo
                // esta montado, cerrando el hueco de orden que dejaba los pines en (0,0) en Android.
                MapaLayout.Loaded += OnMapaLayoutLoaded;
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

            // Arma la lista superior de escenarios (numero + punto de color + nombre) del mismo
            // _evento.Escenarios que los pines, en el mismo punto del ciclo de vida.
            DibujarListaEscenarios();

            // El mapa arranca INVISIBLE: la entrada premium (AnimarEntradaMapaAsync) lo revela
            // con fade al final. Asi, cuando hay intro de portada, al ocultarse el overlay NO se
            // ve de golpe el mapa a opacidad plena para luego hacerle fade (evita el parpadeo).
            if (!_entradaMostrada)
            {
                MapaLayout.Opacity = 0;
            }

            // Prepara el ancla de rotacion y arranca el FLOTAR de los pines, pero NO el lazo de
            // parallax todavia: primero corre el barrido 3D de entrada sin que el lazo le pelee
            // las rotaciones. El lazo (y el acelerometro) se enganchan despues de la entrada.
            MapaLayout.AnchorX = 0.5;
            MapaLayout.AnchorY = 0.5;
            _parallaxInicializado = true;

            // La portada ya se mostro al entrar (MostrarIntroPortadaInmediata). Esperamos a que
            // termine su lucimiento y se oculte ANTES de revelar el mapa, para que el orden sea
            // Portada -> Mapa, sin que el mapa asome por detras antes de tiempo.
            await EsperarYOcultarIntroAsync();

            // Entrada premium del mapa (fade + leve asentamiento 3D + barrido 3D automatico que
            // hace VISIBLE el efecto aunque el telefono este quieto). Una sola vez.
            await AnimarEntradaMapaAsync();

            // Ahora SI: engancha el acelerometro y arranca el lazo de parallax (continua desde el
            // reposo que dejo el barrido). Si no hay sensor, deja el mapa plano y estable.
            IniciarParallax3D();
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

        var lienzoW = imgW * escala;
        var lienzoH = imgH * escala;

        MapaLayout.WidthRequest = lienzoW;
        MapaLayout.HeightRequest = lienzoH;

        // Guarda el tamano REAL del lienzo como fuente de verdad para PosicionarHotspots. Si ya
        // hay pines dibujados, los reposiciona de inmediato con este tamano recien calculado
        // (sin esperar al SizeChanged del layout, que en Android puede no re-dispararse o llegar
        // con un valor provisional). Asi el primer render en Android ya cae en su sitio.
        if (lienzoW > 0 && lienzoH > 0)
        {
            var cambio = Math.Abs(lienzoW - _lienzoW) > 0.5 || Math.Abs(lienzoH - _lienzoH) > 0.5;
            _lienzoW = lienzoW;
            _lienzoH = lienzoH;
            if (cambio && _pinPorEscenario.Count > 0)
            {
                PosicionarHotspots();
            }
        }
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

    // --- Parallax 3D (giroscopio/acelerometro) ---

    // Arranque por PRIMERA vez: fija el ancla, detecta el sensor y, si existe, engancha el
    // acelerometro y lanza el lazo de suavizado. Si NO hay sensor (emulador), el mapa queda
    // PLANO y estable y NO se corre el lazo (ahorro). El efecto es decorativo: nunca bloquea.
    private void IniciarParallax3D()
    {
        // Punto de rotacion: centro del lienzo, para que incline como una maqueta.
        MapaLayout.AnchorX = 0.5;
        MapaLayout.AnchorY = 0.5;

        _parallaxInicializado = true;
        // El flotar de los circulos ya se arranco en DibujarHotspots (ArrancarFlotar) con su
        // generacion; aqui no se vuelve a tocar para no duplicarlo.

        // Deteccion del sensor envuelta en try/catch: un emulador que lance al consultar
        // IsSupported no debe romper la carga del mapa.
        _sensorDisponible = false;
        try
        {
            _sensorDisponible = Accelerometer.Default.IsSupported;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Parallax] No se pudo consultar el sensor: {ex}");
            _sensorDisponible = false;
        }

        if (!_sensorDisponible)
        {
            // Sin acelerometro: mapa plano y estable, fijado UNA sola vez (sin lazo).
            FijarMapaPlano();
            return;
        }

        SuscribirAcelerometro();
        ArrancarLoop();
    }

    // Deja el mapa en reposo exacto (sin inclinacion, sin zoom, sombras base). Se usa cuando no
    // hay sensor: una sola escritura, nada de lazo por frame.
    private void FijarMapaPlano()
    {
        _tiltObjetivoX = _tiltObjetivoY = 0;
        _tiltActualX = _tiltActualY = 0;
        MapaLayout.RotationX = 0;
        MapaLayout.RotationY = 0;
        MapaLayout.Scale = 1;
        foreach (var (_, _, circulo, _) in _pinPorEscenario)
        {
            circulo.TranslationX = 0;
        }
        AplicarSombraDinamica(forzar: true);
    }

    // Suscribe el acelerometro si hay sensor y no esta ya activo (evita doble suscripcion).
    private void SuscribirAcelerometro()
    {
        if (!_sensorDisponible || _acelerometroActivo) return;
        try
        {
            Accelerometer.Default.ReadingChanged += OnAcelerometroLeido;
            Accelerometer.Default.Start(SensorSpeed.Game);
            _acelerometroActivo = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Parallax] Acelerometro no disponible: {ex}");
        }
    }

    // Lanza el lazo de suavizado con una nueva generacion. El guard _loopParallaxActivo evita
    // relanzarlo si ya corre; la generacion garantiza que, si un lazo viejo sigue en vuelo tras
    // un await, termine solo al detectar que caduco (nunca quedan DOS lazos activos).
    private void ArrancarLoop()
    {
        if (_loopParallaxActivo) return;
        _loopParallaxActivo = true;
        var generacion = ++_generacionLoop;
        _ = LoopParallaxAsync(generacion);
    }

    // Convierte la lectura del acelerometro (vector de gravedad) en una inclinacion objetivo.
    // AccelerationX/Y van ~[-1,1] cuando el telefono se inclina; se escalan a grados con tope.
    private void OnAcelerometroLeido(object? sender, AccelerometerChangedEventArgs e)
    {
        var ax = e.Reading.Acceleration.X; // + derecha / - izquierda
        var ay = e.Reading.Acceleration.Y; // + arriba  / - abajo

        // RotationY (giro sobre el eje vertical) sigue la inclinacion lateral del telefono;
        // RotationX (giro sobre el eje horizontal) sigue la inclinacion frontal.
        _tiltObjetivoY = Math.Clamp(ax * TiltMaxGrados, -TiltMaxGrados, TiltMaxGrados);
        _tiltObjetivoX = Math.Clamp(ay * TiltMaxGrados, -TiltMaxGrados, TiltMaxGrados);
    }

    // Interpola suavemente la inclinacion ACTUAL hacia la OBJETIVO y la aplica al mapa (rotacion
    // 3D + zoom sutil = maqueta) y a los pines (parallax POR CAPAS sobre el CONTENEDOR tactil).
    // El flotar vertical vive en el CIRCULO interior (elemento distinto), por lo que no pelea
    // con el parallax por TranslationX/Y. Incluye una guarda de frame: cuando el movimiento es
    // despreciable y ya se alcanzo el reposo, no reasigna propiedades (evita trabajo redundante).
    private async Task LoopParallaxAsync(int generacion)
    {
        while (_loopParallaxActivo && generacion == _generacionLoop)
        {
            var deltaX = (_tiltObjetivoX - _tiltActualX) * Suavizado;
            var deltaY = (_tiltObjetivoY - _tiltActualY) * Suavizado;

            // Si el movimiento por frame es despreciable en ambos ejes, saltamos el trabajo: no
            // reasignamos rotacion/escala/offset (ya estamos en reposo o casi).
            if (Math.Abs(deltaX) < UmbralFrame && Math.Abs(deltaY) < UmbralFrame)
            {
                await Task.Delay(16);
                continue;
            }

            _tiltActualX += deltaX;
            _tiltActualY += deltaY;

            // Rotacion 3D del lienzo (mapa + pines rotan juntos como una maqueta).
            MapaLayout.RotationX = _tiltActualX;
            MapaLayout.RotationY = _tiltActualY;

            // Zoom sutil (<=EscalaMaxExtra) proporcional a cuanto se inclina: refuerza "maqueta"
            // sin deformar (escala uniforme). A tilt 0 -> Scale=1 exacto.
            var inclinacionNorm = (Math.Abs(_tiltActualX) + Math.Abs(_tiltActualY)) / (2 * TiltMaxGrados);
            MapaLayout.Scale = 1 + (EscalaMaxExtra * inclinacionNorm);

            // Parallax POR CAPAS: cada pin se desplaza segun su factor de profundidad cacheado.
            // Se escribe SOLO sobre el CONTENEDOR (area tactil, posicionado por bounds absolutos);
            // el centrado base no se toca -> en reposo (tilt 0) el offset es 0 y el pin queda
            // EXACTAMENTE en su punto.
            foreach (var (_, contenedor, _, factorPin) in _pinPorEscenario)
            {
                contenedor.TranslationX = (-_tiltActualY / TiltMaxGrados) * factorPin;
                contenedor.TranslationY = (_tiltActualX / TiltMaxGrados) * factorPin;
            }

            // Luz/sombra dinamica: barata (solo cuando el tilt cambia de forma apreciable).
            AplicarSombraDinamica(forzar: false);

            await Task.Delay(16); // ~60 fps
        }
    }

    // Recalcula el offset de la sombra de cada circulo en funcion de la inclinacion, de forma
    // que la "luz" parezca venir desde un lado al inclinar. Gated: solo recalcula si el tilt
    // cambio mas que UmbralFrame respecto a la ultima vez (o si se fuerza), para no escribir a
    // 60 fps. A tilt 0 vuelve al offset base (0, SombraBaseY).
    private void AplicarSombraDinamica(bool forzar)
    {
        if (!forzar
            && !double.IsNaN(_tiltSombraX)
            && Math.Abs(_tiltActualX - _tiltSombraX) < UmbralFrame
            && Math.Abs(_tiltActualY - _tiltSombraY) < UmbralFrame)
        {
            return;
        }

        _tiltSombraX = _tiltActualX;
        _tiltSombraY = _tiltActualY;

        var offsetX = (-_tiltActualY / TiltMaxGrados) * SombraOffsetMax;
        var inclinacionNorm = (Math.Abs(_tiltActualX) + Math.Abs(_tiltActualY)) / (2 * TiltMaxGrados);
        var offsetY = SombraBaseY + (inclinacionNorm * (SombraOffsetMax / 2.0));

        foreach (var (_, _, circulo, _) in _pinPorEscenario)
        {
            if (circulo.Shadow is not null)
            {
                circulo.Shadow.Offset = new Point(offsetX, offsetY);
            }
        }
    }

    // Pausa SUAVE (al ocultar la pagina): detiene el lazo, el flotar y el acelerometro, pero
    // CONSERVA _tiltActual*, la rotacion, la escala y las sombras tal como estan (no las pone a
    // 0). Asi, al volver, la reanudacion es continua: sin salto a 0 ni flash al reaparecer.
    private void PausarParallax3D()
    {
        _loopParallaxActivo = false; // el lazo en vuelo termina al ver el flag/generacion
        _generacionLoop++;           // invalida cualquier lazo viejo que siga tras un await

        _flotarActivo = false;       // detiene los lazos de flotar (no traban la transicion)
        _generacionFlotar++;         // invalida cualquier lazo de flotar viejo que siga tras un await
        foreach (var (_, _, circulo, _) in _pinPorEscenario)
        {
            circulo.CancelAnimations(); // corta un TranslateTo en vuelo del flotar
        }

        if (_acelerometroActivo)
        {
            try
            {
                Accelerometer.Default.ReadingChanged -= OnAcelerometroLeido;
                Accelerometer.Default.Stop();
            }
            catch { /* best-effort */ }
            _acelerometroActivo = false;
        }
    }

    // Reanuda al volver de un escenario, partiendo del estado ACTUAL (sin reiniciar a 0). Solo
    // actua si el efecto ya se inicializo (hay pines). Si no hay sensor, deja el mapa plano.
    private void ReanudarParallax3D()
    {
        if (!_parallaxInicializado) return;

        // Relanza el flotar con una generacion NUEVA: cualquier lazo viejo aun en vuelo caduca y
        // termina solo, de modo que no se acumulan lazos al entrar y volver varias veces.
        ArrancarFlotar();

        if (!_sensorDisponible)
        {
            FijarMapaPlano();
            return;
        }

        SuscribirAcelerometro();
        ArrancarLoop(); // continua desde _tiltActual* -> transicion fluida, sin tiron
    }

    // Detiene por completo el efecto (al cerrar la pagina). Igual que la pausa pero sin intencion
    // de reanudar; conserva igualmente el estado para no provocar un salto visible.
    private void DetenerParallax3D()
    {
        PausarParallax3D();
    }

    // Entrada premium del mapa: UNA sola vez por instancia. Fade de 0->1 con un leve asentamiento
    // de maqueta (zoom sutil 1.04->1.0). No toca el IntroOverlay ni su logica; se dispara DESPUES
    // de ocultar la intro de portada (cuando la hubo) o justo tras dibujar los pines (sin portada),
    // por lo que nunca se superpone con la intro. Gated por _entradaMostrada para que NO se repita
    // al volver de un escenario (evita el re-fade que causaba el parpadeo percibido).
    private async Task AnimarEntradaMapaAsync()
    {
        if (_entradaMostrada) return;
        _entradaMostrada = true;

        // Estado inicial: ligeramente ampliado e invisible, y con una leve inclinacion 3D de
        // partida (como una maqueta ladeada) para que la entrada ya "entre" en 3D.
        MapaLayout.Opacity = 0;
        MapaLayout.Scale = 1.04;
        MapaLayout.RotationY = -TiltMaxGrados;
        MapaLayout.RotationX = TiltMaxGrados * 0.5;

        // Fade-in + asentamiento de escala.
        await Task.WhenAll(
            MapaLayout.FadeTo(1, 400, Easing.CubicOut),
            MapaLayout.ScaleTo(1.0, 450, Easing.CubicOut));

        // BARRIDO 3D AUTOMATICO: inclina la maqueta de un lado al otro y la asienta en plano.
        // Hace el efecto 3D VISIBLE de inmediato aunque el telefono este quieto (en el emulador
        // o si el usuario no lo mueve), que era justo lo que no se notaba. Si hay sensor, al
        // terminar el lazo de parallax retoma el control segun la inclinacion real.
        await MapaLayout.RotateYTo(TiltMaxGrados, 500, Easing.SinInOut);
        await MapaLayout.RotateYTo(-TiltMaxGrados * 0.6, 450, Easing.SinInOut);
        await Task.WhenAll(
            MapaLayout.RotateYTo(0, 450, Easing.SinInOut),
            MapaLayout.RotateXTo(0, 450, Easing.SinInOut));

        // Sincroniza el estado del parallax con el reposo tras el barrido, para que el lazo
        // continue suave desde 0 (sin salto) cuando llegue la primera lectura del sensor.
        _tiltActualX = 0;
        _tiltActualY = 0;
    }

    // --- Intro de portada (una sola vez al abrir el evento) ---

    // Muestra la portada DE INMEDIATO al entrar (antes de cargar el mapa), a pantalla completa,
    // con fade + zoom suave. No espera: solo la pone en pantalla y arranca su animacion de
    // entrada. La espera y el ocultado los hace EsperarYOcultarIntroAsync una vez el mapa ya
    // cargo por detras. Si no hay portada, no hace nada (flujo directo al mapa).
    private void MostrarIntroPortadaInmediata(string? portadaUrl)
    {
        if (_introMostrada) return;
        _introMostrada = true;

        if (string.IsNullOrWhiteSpace(portadaUrl))
        {
            return; // sin portada -> directo al mapa
        }

        _introConPortada = true;

        // URL absoluta -> red; cualquier otro valor -> recurso local.
        PortadaImagen.Source = Uri.TryCreate(portadaUrl, UriKind.Absolute, out var uri)
                               && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
            ? ImageSource.FromUri(uri)
            : ImageSource.FromFile(portadaUrl);

        _introCts = new CancellationTokenSource();

        // Fondo negro SOLO durante la intro (letterbox del AspectFill); al ocultarse vuelve a
        // transparente para que no quede ningun cuadro negro residual.
        IntroOverlay.BackgroundColor = Colors.Black;
        IntroOverlay.Opacity = 0;
        IntroOverlay.Scale = 1.08;
        IntroOverlay.IsVisible = true;

        // Animacion de entrada de la portada (no se await: luce mientras el mapa carga detras).
        _ = Task.WhenAll(
            IntroOverlay.FadeTo(1, 450, Easing.CubicOut),
            IntroOverlay.ScaleTo(1.0, 2200, Easing.CubicOut));
    }

    // Espera el tiempo de lucimiento de la portada (o a que el usuario toque) y la oculta. Si no
    // hubo portada, retorna de inmediato. Se llama cuando el mapa YA cargo por detras.
    private async Task EsperarYOcultarIntroAsync()
    {
        if (!_introConPortada) return;

        try
        {
            await Task.Delay(DuracionIntro, _introCts!.Token);
        }
        catch (TaskCanceledException)
        {
            // El usuario salto la intro con un toque: salimos sin esperar.
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
        IntroOverlay.BackgroundColor = Colors.Transparent; // sin residuo negro al terminar
        PortadaImagen.Source = null; // libera la imagen
    }

    private async void OnReintentarClicked(object sender, EventArgs e)
    {
        var id = _navigationState.SelectedEvento?.Id ?? 0;
        _evento = null;
        // Reinicia el tamano cacheado del lienzo para que el reintento vuelva a medir y
        // posicionar desde cero (no reaprovechar un tamano de un intento fallido anterior).
        _lienzoW = _lienzoH = 0;
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
            var (contenedor, circulo) = CrearHotspot(escenario);
            MapaLayout.Children.Add(contenedor);
            _pines.Add(contenedor);
            // Factor de parallax por capa segun la profundidad aparente (PosY): el pin del fondo
            // (arriba) se mueve menos; el del frente (abajo) se mueve mas.
            _pinPorEscenario.Add((escenario, contenedor, circulo, CalcularFactorPin(escenario.PosY)));
        }

        PosicionarHotspots();

        // Arranca el flotar de todos los circulos con una sola generacion (anti duplicacion).
        ArrancarFlotar();
    }

    // Construye la LISTA superior de escenarios: una fila por escenario, ordenada por Numero
    // ascendente, con un circulo del COLOR del escenario (via ResolverColor -> coincide EXACTO con
    // el pin del mapa), el numero (blanco dentro del circulo, como en los pines) y el nombre
    // (morado tenue, con elipsis si es largo). Se genera por codigo por coherencia con el resto de
    // la UI de esta pagina (los pines tambien se dibujan por codigo). Las filas heredan la fuente
    // "Como" del Style de la pagina. Idempotente: limpia las filas previas antes de redibujar y
    // conserva el subtitulo tenue (PistaSuperior), que es el primer hijo del host.
    private void DibujarListaEscenarios()
    {
        if (_evento is null) return;

        // Deja solo el subtitulo (PistaSuperior) y quita filas de un dibujado anterior.
        for (var i = ListaEscenariosHost.Children.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(ListaEscenariosHost.Children[i], PistaSuperior))
            {
                ListaEscenariosHost.Children.RemoveAt(i);
            }
        }

        foreach (var escenario in _evento.Escenarios.OrderBy(e => e.Numero))
        {
            ListaEscenariosHost.Children.Add(CrearFilaEscenario(escenario));
        }
    }

    // Una fila de la lista: circulo con el color del escenario y el numero en blanco, mas el
    // nombre al lado en morado tenue. Envuelta en un Border con fondo blanco y esquinas
    // redondeadas para el look premium. Coherente con los pines y el tema morado.
    private View CrearFilaEscenario(BackendEscenarioDto escenario)
    {
        var numero = new Label
        {
            Text = escenario.Numero.ToString(),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var circulo = new Border
        {
            WidthRequest = 26,
            HeightRequest = 26,
            BackgroundColor = ResolverColor(escenario.Color),
            StrokeThickness = 2,
            Stroke = Colors.White,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 13 },
            Content = numero,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };

        var nombre = new Label
        {
            Text = escenario.Nombre,
            TextColor = Color.FromArgb("#3A2B4A"),
            FontSize = 14,
            VerticalTextAlignment = TextAlignment.Center,
            LineBreakMode = LineBreakMode.TailTruncation,
            MaxLines = 2
        };

        var fila = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Auto },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 10,
            Padding = new Thickness(10, 7)
        };
        Grid.SetColumn(circulo, 0);
        Grid.SetColumn(nombre, 1);
        fila.Children.Add(circulo);
        fila.Children.Add(nombre);

        var tarjeta = new Border
        {
            BackgroundColor = Color.FromArgb("#FFFFFF"),
            Stroke = Color.FromArgb("#ECE6F3"),
            StrokeThickness = 1,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Content = fila
        };

        // Toda la fila es tocable y navega a la MISMA programacion que el pin del mapa
        // (AbrirProgramacionAsync, mismo escenario). Micro-feedback discreto y premium: un leve
        // "hundido" (ScaleTo a 0.97 y de regreso a 1.0) coherente con el rebote del pin pero mas
        // sutil, por tratarse de una fila de lista.
        var tap = new TapGestureRecognizer();
        tap.Tapped += async (_, _) =>
        {
            await tarjeta.ScaleTo(0.97, 90, Easing.CubicOut);
            await tarjeta.ScaleTo(1.0, 110, Easing.CubicIn);
            await AbrirProgramacionAsync(escenario);
        };
        tarjeta.GestureRecognizers.Add(tap);

        return tarjeta;
    }

    // Coloca cada pin con coordenadas ABSOLUTAS sobre el MapaLayout: el CENTRO del pin cae
    // exactamente en (posX%, posY%) del lienzo (que coincide 1:1 con la imagen). Se centra
    // restando media area tactil, el mismo criterio que el admin (left:x% + translate(-50%,-50%)).
    // NO se usa PositionProportional porque este introduce el tamano del pin en la ecuacion y
    // descuadra el punto. Se recalcula cuando cambia el tamano del lienzo (ver SizeChanged).
    private void PosicionarHotspots()
    {
        // Tamano del lienzo: usamos el CALCULADO por DimensionarLienzo (_lienzoW/_lienzoH) como
        // fuente de verdad, no MapaLayout.Width/Height. En Android el tamano arreglado puede
        // leerse en 0/provisional cuando se posiciona (ver dotnet/maui #10747), lo que apilaba
        // todos los pines en (0,0). Como respaldo, si aun no hay tamano calculado, caemos al
        // tamano arreglado del layout (comportamiento previo).
        var w = _lienzoW > 0 ? _lienzoW : MapaLayout.Width;
        var h = _lienzoH > 0 ? _lienzoH : MapaLayout.Height;
        if (w <= 0 || h <= 0) return;

        foreach (var (escenario, contenedor, _, _) in _pinPorEscenario)
        {
            // Clamp defensivo: datos del admin fuera de 0-100 no sacan el pin del mapa.
            var fx = Math.Clamp(escenario.PosX, 0, 100) / 100.0;
            var fy = Math.Clamp(escenario.PosY, 0, 100) / 100.0;

            var cx = fx * w;
            var cy = fy * h;

            AbsoluteLayout.SetLayoutFlags(contenedor, AbsoluteLayoutFlags.None);
            AbsoluteLayout.SetLayoutBounds(contenedor,
                new Rect(cx - (AreaTactil / 2), cy - (AreaTactil / 2), AreaTactil, AreaTactil));
        }
    }

    // Factor de parallax por capa a partir de la profundidad aparente (PosY, 0-100):
    //   depth 0 = fondo (arriba) -> se mueve menos; depth 1 = frente (abajo) -> se mueve mas.
    // Rango resultante: [0.4 .. 1.0] * PinParallaxFactor. Se calcula UNA vez al dibujar y se cachea.
    private static double CalcularFactorPin(double posY)
    {
        var depth = Math.Clamp(posY, 0, 100) / 100.0;
        return PinParallaxFactor * (0.4 + (0.6 * depth));
    }

    // Area tactil de 44px (accesibilidad) con el pin visual de 36px centrado dentro.
    // Devuelve el CONTENEDOR (area tactil, recibe el parallax) y el CIRCULO interior (Border,
    // recibe el flotar y la sombra dinamica). Son elementos distintos a proposito.
    private (View Contenedor, Border Circulo) CrearHotspot(BackendEscenarioDto escenario)
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

        // El flotar sutil del circulo se arranca de forma centralizada en DibujarHotspots (via
        // ArrancarFlotar) una vez que _pinPorEscenario esta completo, para que todos los lazos
        // compartan la misma generacion y no se dupliquen al reanudar.
        return (contenedor, pinVisual);
    }

    // Relanza el flotar de todos los circulos con una generacion NUEVA. Incrementar la generacion
    // invalida cualquier lazo de flotar viejo que siga en vuelo tras un await (termina solo al ver
    // que su generacion caduco), por lo que nunca quedan DOS lazos sobre el mismo circulo aunque
    // _flotarActivo ya se haya vuelto a subir. Centraliza el arranque del flotar.
    private void ArrancarFlotar()
    {
        _flotarActivo = true;
        var generacion = ++_generacionFlotar;
        foreach (var (_, _, circulo, _) in _pinPorEscenario)
        {
            IniciarFlotar(circulo, generacion);
        }
    }

    // Lazo suave e infinito: el circulo del pin sube y baja unos pocos px. Se detiene cuando el
    // pin deja de estar en pantalla (pagina cerrada -> Parent nulo), cuando _flotarActivo es false
    // (pagina pausada) o cuando su generacion caduca (relanzado tras volver de un escenario), para
    // no trabar la transicion ni duplicar el movimiento.
    private async void IniciarFlotar(View pinVisual, int generacion)
    {
        try
        {
            // Pequeño desfase aleatorio para que los pines no floten todos al unisono.
            await Task.Delay(Random.Shared.Next(0, 600));
            while (_flotarActivo && generacion == _generacionFlotar && pinVisual.Parent is not null)
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

        // Detiene el efecto parallax y libera el acelerometro (importante para la bateria).
        DetenerParallax3D();

        // Libera los handlers de tamano.
        if (_viewportSuscrito)
        {
            Viewport.SizeChanged -= OnViewportSizeChanged;
            MapaLayout.SizeChanged -= OnMapaLayoutSizeChanged;
            MapaLayout.Loaded -= OnMapaLayoutLoaded;
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
