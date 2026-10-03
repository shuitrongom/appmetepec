using System.Globalization;

namespace appmetepec.Views;

// Calendario visual del mes para elegir un dia dentro de un evento. Marca los dias que
// tienen eventos y resalta el dia seleccionado. Autocontenido y sin dependencias externas
// (todo con MAUI nativo). Devuelve el dia elegido via PickAsync (mismo patron que
// MapPickerPage), o null si el usuario cierra sin elegir.
public partial class CalendarioEventosPage : ContentPage
{
    private static readonly CultureInfo Es = new("es-MX");

    // Color institucional por defecto si el escenario no define uno.
    private static readonly Color ColorPorDefecto = Color.FromArgb("#5B2A86");

    private readonly HashSet<DateTime> _diasConEventos;
    private readonly DateTime _diaSeleccionado;
    private readonly DateTime _minMes;   // primer mes navegable (mes del inicio del evento)
    private readonly DateTime _maxMes;   // ultimo mes navegable (mes del fin del evento)
    private readonly TaskCompletionSource<DateTime?> _resultado = new();

    // Color del escenario (del backend): tine los dias con evento, el dia seleccionado, las
    // flechas de navegacion, el titulo del mes y el punto de la leyenda, para que el calendario
    // sea coherente con la sede que se esta viendo (igual que el encabezado de la programacion).
    private readonly Color _acento;
    // Version clara del acento para el fondo de los dias con evento NO seleccionados.
    private readonly Color _acentoClaro;

    private DateTime _mesVisible;

    private CalendarioEventosPage(IEnumerable<DateTime> diasConEventos, DateTime diaSeleccionado,
        DateTime fechaInicio, DateTime fechaFin, Color acento)
    {
        InitializeComponent();

        _diasConEventos = [.. diasConEventos.Select(d => d.Date)];
        _diaSeleccionado = diaSeleccionado.Date;
        _minMes = new DateTime(fechaInicio.Year, fechaInicio.Month, 1);
        _maxMes = new DateTime(fechaFin.Year, fechaFin.Month, 1);
        _mesVisible = new DateTime(_diaSeleccionado.Year, _diaSeleccionado.Month, 1);
        _acento = acento;
        // El dia seleccionado va un poco mas claro que el acento para distinguirse del resto
        // de dias con evento (que van en el acento pleno).
        _acentoClaro = _acento.WithLuminosity(Math.Min(1f, _acento.GetLuminosity() + 0.12f));

        AplicarAcento();
        Render();
    }

    // Abre el calendario modal y espera la seleccion del usuario. El color de acento es el del
    // escenario; si no se pasa, usa el institucional.
    public static async Task<DateTime?> PickAsync(INavigation navigation,
        IEnumerable<DateTime> diasConEventos, DateTime diaSeleccionado,
        DateTime fechaInicio, DateTime fechaFin, Color? acento = null)
    {
        var page = new CalendarioEventosPage(diasConEventos, diaSeleccionado, fechaInicio, fechaFin,
            acento ?? ColorPorDefecto);
        await navigation.PushModalAsync(page);
        return await page._resultado.Task;
    }

    // Tine con el color del escenario los elementos fijos del calendario (flechas, titulo del
    // mes y punto de la leyenda). Los que dependen de cada dia se pintan en CrearCelda.
    private void AplicarAcento()
    {
        MesLabel.TextColor = _acento;
        MesAnteriorLabel.TextColor = _acento;
        MesSiguienteLabel.TextColor = _acento;
        LeyendaDot.BackgroundColor = _acento;
        CerrarBtn.TextColor = _acento;
    }

    private void Render()
    {
        MesLabel.Text = Capitalizar(_mesVisible.ToString("MMMM yyyy", Es));

        // Limpiar dias previos.
        DiasGrid.Children.Clear();

        var primerDiaMes = new DateTime(_mesVisible.Year, _mesVisible.Month, 1);
        var diasEnMes = DateTime.DaysInMonth(_mesVisible.Year, _mesVisible.Month);
        // Columna del primer dia (Domingo=0 ... Sabado=6).
        var columnaInicio = (int)primerDiaMes.DayOfWeek;

        var fila = 0;
        var columna = columnaInicio;

        for (var dia = 1; dia <= diasEnMes; dia++)
        {
            var fecha = new DateTime(_mesVisible.Year, _mesVisible.Month, dia);
            var celda = CrearCelda(fecha);

            Grid.SetRow(celda, fila);
            Grid.SetColumn(celda, columna);
            DiasGrid.Children.Add(celda);

            columna++;
            if (columna > 6)
            {
                columna = 0;
                fila++;
            }
        }
    }

    private View CrearCelda(DateTime fecha)
    {
        var tieneEventos = _diasConEventos.Contains(fecha);
        var esSeleccionado = fecha == _diaSeleccionado;
        var esHoy = fecha == DateTime.Today;

        var label = new Label
        {
            Text = fecha.Day.ToString(),
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            FontSize = 15,
            TextColor = tieneEventos ? Colors.White : Color.FromArgb("#CCCCCC"),
            FontAttributes = (tieneEventos || esHoy) ? FontAttributes.Bold : FontAttributes.None
        };

        // Colores con el acento del escenario: dia seleccionado en acento claro, dias con
        // evento en acento pleno, borde del dia de hoy en el acento.
        var celda = new Border
        {
            BackgroundColor = esSeleccionado ? _acentoClaro
                : tieneEventos ? _acento
                : Colors.Transparent,
            StrokeThickness = esHoy ? 2 : 0,
            Stroke = esHoy ? _acento : Colors.Transparent,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Content = label,
            Padding = 0
        };

        // Solo los dias con eventos son tocables (los demas no llevan a ningun lado).
        if (tieneEventos)
        {
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await CerrarAsync(fecha);
            celda.GestureRecognizers.Add(tap);
        }

        return celda;
    }

    private void OnMesAnteriorTapped(object sender, TappedEventArgs e)
    {
        var anterior = _mesVisible.AddMonths(-1);
        if (anterior >= _minMes)
        {
            _mesVisible = anterior;
            Render();
        }
    }

    private void OnMesSiguienteTapped(object sender, TappedEventArgs e)
    {
        var siguiente = _mesVisible.AddMonths(1);
        if (siguiente <= _maxMes)
        {
            _mesVisible = siguiente;
            Render();
        }
    }

    private async void OnCerrarClicked(object sender, EventArgs e) => await CerrarAsync(null);

    private async Task CerrarAsync(DateTime? seleccion)
    {
        _resultado.TrySetResult(seleccion);
        await Navigation.PopModalAsync();
    }

    protected override bool OnBackButtonPressed()
    {
        _resultado.TrySetResult(null);
        return base.OnBackButtonPressed();
    }

    private static string Capitalizar(string texto) =>
        texto.Length > 0 ? char.ToUpper(texto[0], Es) + texto[1..] : texto;
}
