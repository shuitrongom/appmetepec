using System.Globalization;
using appmetepec.Models;
using Microsoft.Maui.Devices;

namespace appmetepec.Views;

// Selector PREMIUM de evento. Mismo patron de modal que ActividadDetallePage /
// CalendarioEventosPage: pagina modal via Navigation.PushModalAsync + TaskCompletionSource,
// instanciada con "new" dentro de PickAsync (NO esta registrada en MauiProgram ni en Shell).
//
// Se abre solo cuando hay 2 o mas eventos activos; lista cada evento con su miniatura
// (portada -> imagen de mapa -> placeholder), nombre y rango de fechas. Al elegir uno,
// devuelve ese BackendEventoDto; al cerrar sin elegir devuelve null. Entra y sale con
// animacion (fade del scrim + scale de la tarjeta). Responsive: el ancho y alto maximos se
// calculan segun la pantalla para que no desborde ni se corte en ningun dispositivo.
// Respeta el safe area de iOS (ios:Page.UseSafeArea="True").
public partial class EventoSelectorPage : ContentPage
{
    private static readonly CultureInfo Es = new("es-MX");

    private readonly TaskCompletionSource<BackendEventoDto?> _resultado;
    private bool _cerrando;

    private EventoSelectorPage(IReadOnlyList<BackendEventoDto> eventos,
        TaskCompletionSource<BackendEventoDto?> resultado)
    {
        InitializeComponent();
        _resultado = resultado;

        // Items de presentacion: cada uno resuelve su miniatura y texto de fechas una sola
        // vez, conservando el BackendEventoDto original para devolverlo al elegir.
        EventosView.ItemsSource = eventos.Select(e => new EventoItem(e)).ToList();

        // Tamano responsive de la tarjeta: ancho ~92% de la pantalla con tope de 520 DIP;
        // alto maximo ~80% de la pantalla (el CollectionView hace scroll si hay muchos eventos).
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var anchoDip = info.Density > 0 ? info.Width / info.Density : 360;
        var altoDip = info.Density > 0 ? info.Height / info.Density : 640;

        Tarjeta.MaximumWidthRequest = Math.Min(anchoDip * 0.92, 520);
        Tarjeta.MaximumHeightRequest = altoDip * 0.80;
    }

    // Abre el selector modal y espera a que el usuario elija un evento (o cierre). Mismo
    // contrato que CalendarioEventosPage.PickAsync: new + PushModalAsync + await del TCS.
    public static async Task<BackendEventoDto?> PickAsync(INavigation navigation,
        IReadOnlyList<BackendEventoDto> eventos)
    {
        var tcs = new TaskCompletionSource<BackendEventoDto?>();
        var page = new EventoSelectorPage(eventos, tcs);
        await navigation.PushModalAsync(page);
        return await tcs.Task;
    }

    // Animacion de entrada: el scrim aparece y la tarjeta crece suave (0.92 -> 1).
    protected override async void OnAppearing()
    {
        base.OnAppearing();

        this.Opacity = 0;
        Tarjeta.Scale = 0.92;
        Tarjeta.Opacity = 0;

        await Task.WhenAll(
            this.FadeTo(1, 180, Easing.CubicOut),
            Tarjeta.ScaleTo(1, 220, Easing.CubicOut),
            Tarjeta.FadeTo(1, 200, Easing.CubicOut));
    }

    // Al elegir un evento: se limpia la seleccion (para no dejar la fila marcada) y se cierra
    // el modal devolviendo el evento elegido.
    private async void OnEventoSeleccionado(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not EventoItem item)
        {
            return;
        }

        EventosView.SelectedItem = null;
        await CerrarAsync(item.Evento);
    }

    private async void OnCerrarClicked(object sender, EventArgs e) => await CerrarAsync(null);

    // Cierre con animacion inversa (fade + leve encogimiento) y luego PopModalAsync. Protegido
    // contra dobles invocaciones (eleccion + boton Cerrar + boton atras).
    private async Task CerrarAsync(BackendEventoDto? seleccion)
    {
        if (_cerrando) return;
        _cerrando = true;

        await Task.WhenAll(
            Tarjeta.ScaleTo(0.92, 160, Easing.CubicIn),
            Tarjeta.FadeTo(0, 160, Easing.CubicIn),
            this.FadeTo(0, 160, Easing.CubicIn));

        await Navigation.PopModalAsync();
        _resultado.TrySetResult(seleccion);
    }

    // Boton atras (Android): cierra el modal sin elegir (devuelve null).
    protected override bool OnBackButtonPressed()
    {
        if (_cerrando) return true;
        _ = CerrarAsync(null);
        return true;
    }

    // Item de presentacion para cada evento del selector: resuelve la miniatura y el texto de
    // fechas y conserva el DTO original. La miniatura usa portada -> imagen de mapa ->
    // placeholder, resolviendo la URL igual que EventoMapaPage (URL http/https absoluta ->
    // ImageSource.FromUri; cualquier otro valor -> recurso empaquetado via ImageSource.FromFile).
    private sealed class EventoItem
    {
        public EventoItem(BackendEventoDto evento)
        {
            Evento = evento;
            Nombre = string.IsNullOrWhiteSpace(evento.Nombre) ? "Evento" : evento.Nombre;
            Fechas = $"{evento.FechaInicio.ToString("d MMM", Es)} - {evento.FechaFin.ToString("d MMM", Es)}";

            var url = !string.IsNullOrWhiteSpace(evento.ImagenPortadaUrl)
                ? evento.ImagenPortadaUrl
                : !string.IsNullOrWhiteSpace(evento.ImagenMapaUrl)
                    ? evento.ImagenMapaUrl
                    : null;

            ImagenFuente = string.IsNullOrWhiteSpace(url)
                ? ImageSource.FromFile("placeholder.png")
                : Uri.TryCreate(url, UriKind.Absolute, out var uri)
                  && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)
                    ? ImageSource.FromUri(uri)
                    : ImageSource.FromFile(url);
        }

        public BackendEventoDto Evento { get; }
        public string Nombre { get; }
        public string Fechas { get; }
        public ImageSource ImagenFuente { get; }
    }
}
