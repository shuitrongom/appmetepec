using System.Globalization;
using appmetepec.Models;
using appmetepec.Services;

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
            .OrderBy(a => a.HoraInicio ?? TimeSpan.Zero)
            .Select(a => new ActividadItem(a))
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

        // Calendario visual del mes: marca los dias con eventos y resalta el actual.
        var elegido = await CalendarioEventosPage.PickAsync(Navigation, dias, _diaSeleccionado, inicio, fin);
        if (elegido is { } dia)
        {
            MostrarDia(dia);
        }
    }

    private void OnHoyClicked(object sender, EventArgs e) => MostrarDia(DateTime.Today);

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    private static string CapitalizarFecha(DateTime d)
    {
        // "martes 13 de octubre" -> "Martes 13 de octubre"
        var texto = d.ToString("dddd d 'de' MMMM", Es);
        return texto.Length > 0 ? char.ToUpper(texto[0], Es) + texto[1..] : texto;
    }

    // Item de presentacion para el CollectionView.
    private sealed class ActividadItem
    {
        public ActividadItem(BackendActividadDto a)
        {
            HoraTexto = a.HoraInicio is { } h
                ? (a.HoraFin is { } f ? $"{Formato(h)}\n{Formato(f)}" : Formato(h))
                : "";
            // Si hay pais, se antepone como etiqueta corta (ej. "FR").
            Titulo = string.IsNullOrWhiteSpace(a.Pais) ? a.Titulo : $"{a.Titulo}  ·  {a.Pais}";
            Descripcion = a.Descripcion ?? "";
            TieneDescripcion = !string.IsNullOrWhiteSpace(a.Descripcion);
        }

        public string HoraTexto { get; }
        public string Titulo { get; }
        public string Descripcion { get; }
        public bool TieneDescripcion { get; }

        // Formateo nativo del TimeSpan: no construye un DateTime (que lanzaria excepcion si
        // el backend manda una hora fuera de 0-24h). Normaliza al rango de un dia por seguridad.
        private static string Formato(TimeSpan t)
        {
            var normal = t;
            if (normal < TimeSpan.Zero) normal = TimeSpan.Zero;
            if (normal >= TimeSpan.FromDays(1)) normal = new TimeSpan(normal.Hours % 24, normal.Minutes, 0);
            return normal.ToString(@"hh\:mm") + " h";
        }
    }
}
