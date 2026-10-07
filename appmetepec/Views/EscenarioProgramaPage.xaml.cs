using System.Globalization;
using appmetepec.Models;
using appmetepec.Services;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Devices;
using Microsoft.Maui.Devices.Sensors;

namespace appmetepec.Views;

// Programacion de un escenario. Por defecto muestra los eventos del DIA DE HOY; un icono
// de calendario permite saltar a cualquier dia con eventos y un boton "Hoy" regresa al
// dia actual. Diseno premium: el usuario ve "hoy" al frente y explora con un toque.
public partial class EscenarioProgramaPage : ContentPage
{
    private readonly NavigationState _navigationState;

    private BackendEscenarioDto? _escenario;
    private DateTime _diaSeleccionado;

    private static readonly CultureInfo Es = new("es-MX");

    // Color base del escenario (del backend); por defecto el morado institucional.
    private static readonly Color ColorPorDefecto = Color.FromArgb("#5B2A86");
    private Color _colorEscenario = ColorPorDefecto;

    public EscenarioProgramaPage(NavigationState navigationState)
    {
        InitializeComponent();
        _navigationState = navigationState;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        if (_escenario is not null)
        {
            // CAUSA RAIZ DEL BUG "cambio de dia y se pierde la animacion": el calendario para
            // elegir otro dia (CalendarioEventosPage.PickAsync) hace PushModalAsync, lo que
            // dispara OnDisappearing() de esta pagina -> DetenerImagen3D() mata la flotacion, el
            // lazo de parallax y desuscribe el acelerometro. Al cerrar el modal, OnAppearing()
            // vuelve a entrar PERO antes salia aqui mismo (return temprano) sin RE-ARRANCAR el
            // efecto, por lo que la imagen quedaba estatica el resto de la sesion.
            // FIX: cuando reaparecemos con la pagina ya inicializada y hay imagen, re-arrancamos
            // el efecto 3D. IniciarImagen3D() es idempotente (los flags _imgLoopActivo/
            // _imgFlotarActivo y la (re)suscripcion guardada por _imgAcelerometroActivo evitan
            // duplicar lazos o suscripciones aunque se entre y salga del calendario varias veces).
            if (ImagenEscenario.IsVisible)
            {
                IniciarImagen3D();
            }
            return;
        }

        _escenario = _navigationState.SelectedEscenario;
        if (_escenario is null)
        {
            Shell.Current.GoToAsync("..");
            return;
        }

        EscenarioNombre.Text = _escenario.Nombre;
        EscenarioDireccion.Text = _escenario.Direccion ?? "";
        EscenarioDireccion.IsVisible = !string.IsNullOrWhiteSpace(_escenario.Direccion);

        // "Como llegar" tiene sentido si el escenario trae coordenadas (punto exacto) o,
        // en su defecto, una direccion en texto para que el mapa la geolocalice.
        ComoLlegarBtn.IsVisible = TieneCoordenadas() || !string.IsNullOrWhiteSpace(_escenario.Direccion);

        // Micro-ayuda tenue: solo menciona acciones que REALMENTE existen en esta pantalla.
        // Tocar una actividad y el calendario 📅 siempre estan; el fragmento 📍 "como llegar"
        // se omite cuando el escenario no tiene coordenadas ni direccion (boton oculto).
        PistaAcciones.Text = ComoLlegarBtn.IsVisible
            ? "Toca una actividad para ver su detalle · 📅 otros días · 📍 cómo llegar"
            : "Toca una actividad para ver su detalle · 📅 otros días";

        // Imagen propia de la sede (opcional, la sube el admin): si viene, se muestra a la
        // derecha de la programacion como ilustracion premium de la sede.
        MostrarImagenEscenario(_escenario.ImagenUrl);

        // El escenario trae su propio color (lo define el admin en el front): tenimos
        // el encabezado y los acentos con ese color para que cada sede sea identificable.
        _colorEscenario = ResolverColor(_escenario.Color);
        AplicarColorEscenario();

        // Arranca en el dia de hoy.
        MostrarDia(DateTime.Today);
    }

    private void MostrarDia(DateTime dia)
    {
        if (_escenario is null) return;

        _diaSeleccionado = dia.Date;

        var esHoy = _diaSeleccionado == DateTime.Today;
        DiaLabel.Text = esHoy
            ? $"Hoy · {CapitalizarFecha(_diaSeleccionado)}"
            : CapitalizarFecha(_diaSeleccionado);
        HoyButton.IsVisible = !esHoy;

        var items = _escenario.Actividades
            .Where(a => a.Fecha.Date == _diaSeleccionado)
            .OrderBy(a => MinutosDesdeMedianoche(a.HoraInicio))
            .Select(a => new ActividadItem(a, _colorEscenario, _escenario!.Nombre))
            .ToList();

        ActividadesView.ItemsSource = items;
        var hay = items.Count > 0;
        ActividadesView.IsVisible = hay;
        VacioLabel.IsVisible = !hay;
    }

    private async void OnCalendarioTapped(object sender, TappedEventArgs e)
    {
        if (_escenario is null) return;

        // Dias que SI tienen eventos en este escenario.
        var dias = _escenario.Actividades
            .Select(a => a.Fecha.Date)
            .Distinct()
            .ToList();

        if (dias.Count == 0)
        {
            await DisplayAlert("Calendario", "Este escenario aún no tiene eventos programados.", "Aceptar");
            return;
        }

        // Rango de meses navegables: del evento (si lo tenemos) o, en su defecto, el minimo
        // y maximo de los dias con eventos.
        var inicio = _navigationState.SelectedEvento?.FechaInicio ?? dias.Min();
        var fin = _navigationState.SelectedEvento?.FechaFin ?? dias.Max();

        // Calendario visual del mes: marca los dias con eventos y resalta el actual, con el
        // color de la sede para que sea coherente con el encabezado de la programacion.
        var elegido = await CalendarioEventosPage.PickAsync(Navigation, dias, _diaSeleccionado, inicio, fin, _colorEscenario);
        if (elegido is { } dia)
        {
            MostrarDia(dia);
        }
    }

    private void OnHoyClicked(object sender, EventArgs e) => MostrarDia(DateTime.Today);

    // "Como llegar": abre la app de mapas nativa (Google Maps en Android, Apple Maps en iOS)
    // en modo navegacion/ruta hacia la sede. El mapa calcula la ruta desde la ubicacion del
    // usuario (lo pide el propio mapa, no la app).
    //   1) Si el escenario trae coordenadas (lat/lng, las define el admin en el front), se usa
    //      el PUNTO EXACTO -> la ruta llega justo a la sede, sin ambiguedad de geocoder.
    //   2) Si no, se usa la direccion en texto para que el mapa la geolocalice.
    private async void OnComoLlegarTapped(object sender, EventArgs e)
    {
        if (_escenario is null) return;

        try
        {
            var url = ConstruirUrlComoLlegar(_escenario);
            if (url is null)
            {
                return;
            }

            var abierto = await Launcher.Default.TryOpenAsync(url);
            if (!abierto)
            {
                // Fallback: mapa nativo del sistema. Con coordenadas usa el punto exacto;
                // si no, la direccion en texto.
                var opciones = new MapLaunchOptions { Name = _escenario.Nombre, NavigationMode = NavigationMode.Driving };
                if (TieneCoordenadas())
                {
                    var ubicacion = new Location((double)_escenario.Latitud!.Value, (double)_escenario.Longitud!.Value);
                    await Map.Default.OpenAsync(ubicacion, opciones);
                }
                else
                {
                    await Map.Default.OpenAsync(new Placemark
                    {
                        CountryName = "México",
                        AdminArea = "Estado de México",
                        Locality = "Metepec",
                        Thoroughfare = _escenario.Direccion
                    }, opciones);
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ComoLlegar] No se pudo abrir el mapa: {ex}");
            await DisplayAlert("Cómo llegar",
                "No se pudo abrir la aplicación de mapas en este dispositivo.", "Aceptar");
        }
    }

    // Arma la URL universal de Google Maps en modo ruta. Prioriza coordenadas exactas; si no
    // hay, cae a la direccion en texto (añadiendo el municipio para desambiguar). null si no
    // hay ni coordenadas ni direccion.
    private string? ConstruirUrlComoLlegar(BackendEscenarioDto escenario)
    {
        if (TieneCoordenadas())
        {
            // lat,lng con punto decimal (InvariantCulture) y sin espacios.
            var lat = escenario.Latitud!.Value.ToString(CultureInfo.InvariantCulture);
            var lng = escenario.Longitud!.Value.ToString(CultureInfo.InvariantCulture);
            return $"https://www.google.com/maps/dir/?api=1&destination={lat},{lng}";
        }

        var direccion = escenario.Direccion;
        if (string.IsNullOrWhiteSpace(direccion))
        {
            return null;
        }

        // Añade "Metepec, Estado de Mexico, Mexico" si la direccion no menciona el municipio,
        // para que el geocoder no la confunda con una calle homonima de otra ciudad.
        var destino = direccion.Trim();
        if (!destino.Contains("Metepec", StringComparison.OrdinalIgnoreCase))
        {
            destino = $"{destino}, Metepec, Estado de México, México";
        }

        return $"https://www.google.com/maps/dir/?api=1&destination={Uri.EscapeDataString(destino)}";
    }

    // true si el escenario trae coordenadas geograficas validas (no nulas y no 0,0).
    private bool TieneCoordenadas()
    {
        return _escenario is { Latitud: { } lat, Longitud: { } lng }
               && !(lat == 0 && lng == 0);
    }

    // Muestra (o esconde) la ilustracion lateral de la sede. Solo visible si el admin subio una
    // imagen; si no, la programacion ocupa todo el ancho. Cuando hay imagen, la programacion
    // reserva una franja a la derecha (Margin) para que la ilustracion -alargada y anclada
    // abajo-derecha- nunca tape el texto, y se arranca el efecto 3D (flotacion + giroscopio).
    private void MostrarImagenEscenario(string? imagenUrl)
    {
        var hay = !string.IsNullOrWhiteSpace(imagenUrl);
        ImagenEscenario.IsVisible = hay;

        if (hay)
        {
            ImagenEscenario.Source = imagenUrl;
            DimensionarImagenEscenario();
            IniciarImagen3D();
        }
        else
        {
            ProgramaStack.Padding = new Thickness(16);
        }
    }

    // Tamano de la ilustracion PROPORCIONAL al dispositivo: se calcula a partir del alto de la
    // pantalla para que luzca igual de grande en cualquier telefono (no un tamano fijo que se
    // ve chico en pantallas grandes). Alto ~48% de la pantalla (acotado), ancho ~62% de ese
    // alto (proporcion vertical tipica de las ilustraciones). Tambien ajusta el espacio que
    // reserva la programacion a la derecha segun ese ancho, para que nunca tape el texto.
    private void DimensionarImagenEscenario()
    {
        // Alto en px independientes del dispositivo (DIP). DeviceDisplay da pixeles fisicos, se
        // divide por la densidad para trabajar en DIP (las unidades de MAUI).
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var altoPantallaDip = info.Density > 0 ? info.Height / info.Density : 640;

        // Alto objetivo MAS GENEROSO: 62% de la pantalla, acotado entre 300 y 560 DIP, para que
        // la ilustracion se vea GRANDE (es una capa de fondo, ya no compite con el texto).
        var alto = Math.Clamp(altoPantallaDip * 0.62, 300, 560);
        var ancho = alto * 0.62;

        ImagenEscenario.HeightRequest = alto;
        ImagenEscenario.WidthRequest = ancho;

        // La imagen REBASA el borde derecho para que ~1/4 de su ANCHO quede FUERA de pantalla por
        // la derecha (efecto pedido: "que se corte un poco a la derecha"), de forma CONSISTENTE en
        // cualquier tamano de telefono porque el empuje es PROPORCIONAL al ancho (no un valor fijo).
        //
        // Factor 0.60 (antes 0.52, 0.38, 0.25 original): el empuje base es ancho*0.60. En las ultimas
        // 5 capturas de TestFlight de distintos escenarios la silueta del ave AUN invadia ~1 columna
        // de texto y se encimaba con el final de lineas de los titulos ("anochecio", "Rivero",
        // "Torres", "Nava", "Dande"), haciendo que esas palabras se "perdieran". Como la imagen es
        // AspectFit dentro de su caja de ancho 'ancho', el PNG renderizado queda CENTRADO con margen
        // transparente a cada lado; con 0.52 el borde visible del ave todavia alcanzaba la columna
        // de texto. Subimos un escalon a 0.60*ancho para que esa porcion que invade el texto quede
        // FUERA de pantalla por la derecha; es aceptable y deseado que una porcion MAYOR del ave se
        // recorte, porque el usuario prioriza que el TEXTO NUNCA se pierda. El empuje sigue siendo
        // PROPORCIONAL al ancho real -> consistente en todos los tamanos de telefono.
        // ContenidoGrid tiene IsClippedToBounds para un recorte limpio. Se guarda la base para que
        // el parallax del giroscopio oscile ALREDEDOR de ella y no "regrese" la imagen al centro.
        _imgTranslationXBase = ancho * 0.60;
        ImagenEscenario.TranslationX = _imgTranslationXBase;

        // La programacion ya NO reserva franja a la derecha: ocupa TODO el ancho para que los
        // titulos se lean en lineas normales. La imagen va DEBAJO (capa de fondo), no la tapa.
        ProgramaStack.Padding = new Thickness(16);
    }

    // --- Efecto 3D de la ilustracion de la sede (flotacion continua + parallax por giroscopio) ---
    // Igual que el mapa: la imagen "levita" suave (sube/baja + leve balanceo 3D) y, al inclinar
    // el telefono, se inclina y desplaza un poco (parallax), dando sensacion de que flota sobre
    // la pantalla. Es decorativo: si no hay acelerometro (emulador), solo queda la flotacion y,
    // si tampoco, la imagen queda estatica, sin errores.

    // Inclinacion maxima en grados (un poco mas marcada para que el 3D se note mas).
    private const double ImgTiltMaxGrados = 12;
    // Desplazamiento (px) del parallax al inclinar (mayor = flota mas sobre la pantalla).
    private const double ImgParallaxFactor = 16;
    // Suavizado del seguimiento del sensor (0-1): mas bajo = mas suave.
    private const double ImgSuavizado = 0.14;
    // Balanceo 3D AUTOMATICO continuo (grados) para que el efecto se vea aunque el telefono este
    // quieto. Se suma a la inclinacion del sensor; muy sutil para no marear.
    private const double ImgBalanceoAuto = 4;

    private double _imgTiltObjetivoX, _imgTiltObjetivoY;
    private double _imgTiltActualX, _imgTiltActualY;
    private bool _imgAcelerometroActivo;
    private bool _imgLoopActivo;
    private int _imgGeneracionLoop;
    private bool _imgFlotarActivo;
    private int _imgGeneracionFlotar;
    // Fase del balanceo automatico (avanza cada frame del lazo de parallax).
    private double _imgFaseBalanceo;
    // Base de TranslationX (empuje fuera de pantalla ~1/4 del ancho). La calcula
    // DimensionarImagenEscenario segun el ancho real; el parallax del giroscopio suma su delta
    // sobre esta base para no "regresar" la imagen al borde.
    private double _imgTranslationXBase = 34;

    private void IniciarImagen3D()
    {
        // Punto de rotacion: abajo-centro, para que "crezca/incline" desde su base apoyada.
        ImagenEscenario.AnchorX = 0.7;
        ImagenEscenario.AnchorY = 1.0;

        try
        {
            if (Accelerometer.Default.IsSupported && !_imgAcelerometroActivo)
            {
                Accelerometer.Default.ReadingChanged += OnImgAcelerometroLeido;
                Accelerometer.Default.Start(SensorSpeed.Game);
                _imgAcelerometroActivo = true;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[ImagenSede] Acelerometro no disponible: {ex}");
        }

        // Lazo de parallax suavizado (corre aunque no haya sensor; sin lecturas, queda plano).
        if (!_imgLoopActivo)
        {
            _imgLoopActivo = true;
            var generacion = ++_imgGeneracionLoop;
            _ = LoopImagenParallaxAsync(generacion);
        }

        // Flotacion continua (independiente del sensor).
        if (!_imgFlotarActivo)
        {
            _imgFlotarActivo = true;
            var generacion = ++_imgGeneracionFlotar;
            _ = FlotarImagenAsync(generacion);
        }
    }

    private void OnImgAcelerometroLeido(object? sender, AccelerometerChangedEventArgs e)
    {
        var ax = e.Reading.Acceleration.X;
        var ay = e.Reading.Acceleration.Y;
        _imgTiltObjetivoY = Math.Clamp(ax * ImgTiltMaxGrados, -ImgTiltMaxGrados, ImgTiltMaxGrados);
        _imgTiltObjetivoX = Math.Clamp(ay * ImgTiltMaxGrados, -ImgTiltMaxGrados, ImgTiltMaxGrados);
    }

    // Interpola la inclinacion hacia la objetivo y la aplica como rotacion 3D + desplazamiento
    // (parallax). El flotar vertical lo maneja FlotarImagenAsync sobre TranslationY, asi que aqui
    // el parallax actua sobre RotationX/Y y TranslationX para no pelear por la misma propiedad.
    private async Task LoopImagenParallaxAsync(int generacion)
    {
        while (_imgLoopActivo && generacion == _imgGeneracionLoop)
        {
            _imgTiltActualX += (_imgTiltObjetivoX - _imgTiltActualX) * ImgSuavizado;
            _imgTiltActualY += (_imgTiltObjetivoY - _imgTiltActualY) * ImgSuavizado;

            // Balanceo 3D automatico continuo (seno): la imagen se mece sola suave, asi el efecto
            // 3D se ve aunque el telefono este quieto. Se suma a la inclinacion del sensor.
            _imgFaseBalanceo += 0.03;
            var balanceoY = Math.Sin(_imgFaseBalanceo) * ImgBalanceoAuto;
            var balanceoX = Math.Cos(_imgFaseBalanceo * 0.8) * (ImgBalanceoAuto * 0.4);

            ImagenEscenario.RotationY = _imgTiltActualY + balanceoY;
            ImagenEscenario.RotationX = _imgTiltActualX + balanceoX;

            // Respiro de escala muy sutil (±2%) acompasado al balanceo: da sensacion de volumen.
            ImagenEscenario.Scale = 1 + (Math.Sin(_imgFaseBalanceo) * 0.02);

            // Parallax horizontal: parte de la base (empuje fuera de pantalla ~1/4 del ancho,
            // calculada en DimensionarImagenEscenario) y se suma el delta del parallax.
            ImagenEscenario.TranslationX = _imgTranslationXBase + ((-_imgTiltActualY / ImgTiltMaxGrados) * ImgParallaxFactor);

            await Task.Delay(16); // ~60 fps
        }
    }

    // Flotacion continua: la imagen sube y baja unos px, suave e infinita, mientras siga visible
    // y la generacion sea la vigente (evita duplicar lazos al volver a la pagina).
    private async Task FlotarImagenAsync(int generacion)
    {
        try
        {
            while (_imgFlotarActivo && generacion == _imgGeneracionFlotar && ImagenEscenario.IsVisible)
            {
                await ImagenEscenario.TranslateTo(ImagenEscenario.TranslationX, -12, 1500, Easing.SinInOut);
                await ImagenEscenario.TranslateTo(ImagenEscenario.TranslationX, 0, 1500, Easing.SinInOut);
            }
        }
        catch
        {
            // Si la imagen se libera a mitad de animacion, se ignora.
        }
    }

    private void DetenerImagen3D()
    {
        _imgLoopActivo = false;
        _imgGeneracionLoop++;
        _imgFlotarActivo = false;
        _imgGeneracionFlotar++;

        if (_imgAcelerometroActivo)
        {
            try
            {
                Accelerometer.Default.ReadingChanged -= OnImgAcelerometroLeido;
                Accelerometer.Default.Stop();
            }
            catch { /* best-effort */ }
            _imgAcelerometroActivo = false;
        }
    }

    // Tine el encabezado y los acentos con el color del escenario. Una barra del dia con
    // una version muy clara del mismo color da un acabado premium y coherente.
    private void AplicarColorEscenario()
    {
        HeaderGrid.BackgroundColor = _colorEscenario;
        DiaLabel.TextColor = _colorEscenario;
        HoyButton.BackgroundColor = _colorEscenario;
        DiaBar.BackgroundColor = _colorEscenario.WithAlpha(0.12f);
    }

    // Convierte el hex del backend ("#RRGGBB") en Color, con fallback al color institucional.
    private static Color ResolverColor(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex))
        {
            return ColorPorDefecto;
        }

        try
        {
            return Color.FromArgb(hex);
        }
        catch
        {
            return ColorPorDefecto;
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    // Toque sobre una fila de la programacion (via TapGestureRecognizer, NO seleccion nativa:
    // asi no aparece el resaltado gris que se veia feo). REGLA DEL USUARIO: el modal de detalle
    // abre SOLO si la actividad tiene descripcion con texto (no null, no solo espacios). Si no
    // tiene descripcion, tocar la fila NO abre nada (no-op).
    private async void OnActividadTocada(object sender, TappedEventArgs e)
    {
        // El sender es el elemento visual (Grid de la fila); su BindingContext es el ActividadItem.
        if (sender is BindableObject vista && vista.BindingContext is ActividadItem item
            && item.TieneDescripcion)
        {
            await ActividadDetallePage.PickAsync(
                Navigation, item.SedeNombre, item.HoraRango, item.Actividad, _colorEscenario);
        }
    }

    // Al salir de la pagina: detiene el efecto 3D de la imagen (libera el acelerometro y para
    // los lazos) para no gastar bateria ni dejar animaciones corriendo.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        DetenerImagen3D();
    }

    private static string CapitalizarFecha(DateTime d)
    {
        // "martes 13 de octubre" -> "Martes 13 de octubre"
        var texto = d.ToString("dddd d 'de' MMMM", Es);
        return texto.Length > 0 ? char.ToUpper(texto[0], Es) + texto[1..] : texto;
    }

    // Convierte "HH:mm" a minutos desde medianoche para ordenar. null/invalido -> int.MaxValue
    // (las actividades sin hora van al final).
    private static int MinutosDesdeMedianoche(string? hhmm)
    {
        if (string.IsNullOrWhiteSpace(hhmm)) return int.MaxValue;
        var partes = hhmm.Split(':');
        if (partes.Length < 2) return int.MaxValue;
        if (int.TryParse(partes[0], out var h) && int.TryParse(partes[1], out var m))
        {
            return (h * 60) + m;
        }
        return int.MaxValue;
    }

    // Item de presentacion para el CollectionView.
    private sealed class ActividadItem
    {
        public ActividadItem(BackendActividadDto a, Color colorEscenario, string sedeNombre)
        {
            Actividad = a;
            SedeNombre = sedeNombre;
            HoraTexto = ConstruirHora(a.HoraInicio, a.HoraFin);
            HoraRango = ConstruirHoraRango(a.HoraInicio, a.HoraFin);
            Color = colorEscenario;
            Contenido = ActividadFormato.Construir(a);
            Descripcion = a.Descripcion;
            TieneDescripcion = !string.IsNullOrWhiteSpace(a.Descripcion);
        }

        // Actividad original: la conserva para reconstruir el contenido con formato en el
        // modal (un mismo FormattedString no puede tener dos Labels padre, por eso el modal
        // construye su propia instancia a partir de este DTO) y para leer la descripcion.
        public BackendActividadDto Actividad { get; }

        // Nombre de la sede (escenario): encabezado del modal.
        public string SedeNombre { get; }

        public string HoraTexto { get; }

        // Rango de hora en UNA sola linea con guion ("HH:mm - HH:mm"), para el encabezado del
        // modal (en la lista se usa HoraTexto, que va en dos renglones).
        public string HoraRango { get; }

        public Color Color { get; }

        // Descripcion opcional (= Actividad.Descripcion) y bandera derivada que la lista usa
        // para el gesto (abrir modal) y para el indicador sutil de "ver mas".
        public string? Descripcion { get; }
        public bool TieneDescripcion { get; }

        // Contenido con formato para la LISTA: su propia instancia de FormattedString (el modal
        // construye otra aparte con ActividadFormato.Construir).
        public FormattedString Contenido { get; }

        // Rango horario para la LISTA: inicio y fin en dos renglones (como el programa impreso);
        // si solo hay inicio, muestra solo ese; si no hay hora, cadena vacia.
        private static string ConstruirHora(string? inicio, string? fin)
        {
            var ini = ActividadFormato.NormalizarHora(inicio);
            var f = ActividadFormato.NormalizarHora(fin);
            if (ini is not null && f is not null) return $"{ini}\n{f}";
            if (ini is not null) return ini;
            if (f is not null) return f;
            return "";
        }

        // Rango horario en UNA sola linea con guion para el encabezado del modal.
        private static string ConstruirHoraRango(string? inicio, string? fin)
        {
            var ini = ActividadFormato.NormalizarHora(inicio);
            var f = ActividadFormato.NormalizarHora(fin);
            if (ini is not null && f is not null) return $"{ini} - {f}";
            if (ini is not null) return ini;
            if (f is not null) return f;
            return "";
        }
    }

    // Helper estatico reutilizable para armar las lineas con formato de una actividad. Tanto la
    // lista como el modal lo llaman para construir su PROPIA instancia de FormattedString (un
    // mismo FormattedString no puede estar asignado a dos Labels a la vez -> error de "parent").
    internal static class ActividadFormato
    {
        // Color por defecto de una linea cuando el backend no define uno.
        private static readonly Color TextoPorDefecto = Color.FromArgb("#222222");

        // Construye el FormattedString: cada linea de la actividad se vuelve un Span con su
        // propio estilo (negrita, cursiva, color, fuente, tamano), separados por salto de linea.
        // Si la actividad no trae lineas, muestra un texto discreto por defecto.
        public static FormattedString Construir(BackendActividadDto a)
        {
            var fs = new FormattedString();

            if (a.Lineas.Count == 0)
            {
                fs.Spans.Add(new Span
                {
                    Text = "Actividad sin descripción",
                    TextColor = Color.FromArgb("#999999"),
                    FontSize = 13
                });
                return fs;
            }

            for (var i = 0; i < a.Lineas.Count; i++)
            {
                var linea = a.Lineas[i];
                var prefijo = i == 0 ? "" : "\n";

                var span = new Span
                {
                    Text = prefijo + linea.Texto,
                    TextColor = ResolverColorLinea(linea.Color),
                    FontAttributes = AtributosDe(linea)
                };

                if (linea.Tamano is { } t && t > 0)
                {
                    span.FontSize = t;
                }

                if (!string.IsNullOrWhiteSpace(linea.Fuente))
                {
                    span.FontFamily = linea.Fuente;
                }

                fs.Spans.Add(span);
            }

            return fs;
        }

        private static FontAttributes AtributosDe(BackendActividadLineaDto l)
        {
            var attrs = FontAttributes.None;
            if (l.Negrita) attrs |= FontAttributes.Bold;
            if (l.Cursiva) attrs |= FontAttributes.Italic;
            return attrs;
        }

        private static Color ResolverColorLinea(string? hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return TextoPorDefecto;
            try { return Color.FromArgb(hex); }
            catch { return TextoPorDefecto; }
        }

        // Deja la hora "HH:mm" tal cual (ya viene formateada del backend). Si viniera con
        // segundos "HH:mm:ss", recorta a "HH:mm". null/vacio -> null.
        public static string? NormalizarHora(string? hhmm)
        {
            if (string.IsNullOrWhiteSpace(hhmm)) return null;
            var partes = hhmm.Split(':');
            if (partes.Length >= 2) return $"{partes[0]}:{partes[1]}";
            return hhmm;
        }
    }
}
