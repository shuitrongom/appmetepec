using appmetepec.Models;
using Microsoft.Maui.Devices;

namespace appmetepec.Views;

// Modal/popup PREMIUM de detalle de una actividad. Mismo patron de modal que
// CalendarioEventosPage: pagina modal via Navigation.PushModalAsync + TaskCompletionSource,
// instanciada con "new" dentro de PickAsync (NO esta registrada en MauiProgram ni en Shell).
//
// Muestra: nombre de la sede + rango de hora + las lineas con formato de la actividad
// (reconstruidas en su PROPIA instancia de FormattedString con ActividadFormato.Construir,
// porque un mismo FormattedString no puede tener dos Labels padre) + la seccion Descripcion.
// Entra y sale con animacion (fade del scrim + scale de la tarjeta). Responsive: el ancho y
// alto maximos se calculan segun la pantalla para que no desborde ni se corte en ningun
// dispositivo. Respeta el safe area de iOS (ios:Page.UseSafeArea="True").
public partial class ActividadDetallePage : ContentPage
{
    private readonly TaskCompletionSource<bool> _resultado;
    private bool _cerrando;

    private ActividadDetallePage(string sedeNombre, string horaRango, BackendActividadDto actividad,
        Color acento, TaskCompletionSource<bool> resultado)
    {
        InitializeComponent();
        _resultado = resultado;

        // Textos del encabezado.
        SedeLabel.Text = sedeNombre;
        HoraLabel.Text = horaRango;
        HoraLabel.IsVisible = !string.IsNullOrWhiteSpace(horaRango);

        // Acento del escenario: barra superior, nombre de sede y boton Cerrar.
        AcentoBar.Color = acento;
        SedeLabel.TextColor = acento;
        CerrarBtn.TextColor = acento;

        // Lineas con formato: instancia propia (independiente de la de la lista).
        ContenidoLabel.FormattedText = EscenarioProgramaPage.ActividadFormato.Construir(actividad);

        // Seccion Descripcion: solo si la actividad trae descripcion con texto (por regla,
        // cuando el modal abre, siempre existe; aun asi se valida para ser robustos).
        var hayDescripcion = !string.IsNullOrWhiteSpace(actividad.Descripcion);
        DescripcionContenedor.IsVisible = hayDescripcion;
        DescripcionTexto.Text = actividad.Descripcion ?? "";

        // Tamano responsive de la tarjeta: ancho ~92% de la pantalla con tope de 520 DIP;
        // alto maximo ~80% de la pantalla (el ScrollView se encarga si el contenido es largo).
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var anchoDip = info.Density > 0 ? info.Width / info.Density : 360;
        var altoDip = info.Density > 0 ? info.Height / info.Density : 640;

        Tarjeta.MaximumWidthRequest = Math.Min(anchoDip * 0.92, 520);
        Tarjeta.MaximumHeightRequest = altoDip * 0.80;
    }

    // Abre el modal de detalle y espera a que el usuario lo cierre. Mismo contrato que
    // CalendarioEventosPage.PickAsync: new + PushModalAsync + await del TCS.
    public static async Task PickAsync(INavigation navigation, string sedeNombre, string horaRango,
        BackendActividadDto actividad, Color acento)
    {
        var tcs = new TaskCompletionSource<bool>();
        var page = new ActividadDetallePage(sedeNombre, horaRango, actividad, acento, tcs);
        await navigation.PushModalAsync(page);
        await tcs.Task;
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

    private async void OnCerrarClicked(object sender, EventArgs e) => await CerrarAsync();

    // Cierre con animacion inversa (fade + leve encogimiento) y luego PopModalAsync. Protegido
    // contra dobles invocaciones (boton + boton atras).
    private async Task CerrarAsync()
    {
        if (_cerrando) return;
        _cerrando = true;

        await Task.WhenAll(
            Tarjeta.ScaleTo(0.92, 160, Easing.CubicIn),
            Tarjeta.FadeTo(0, 160, Easing.CubicIn),
            this.FadeTo(0, 160, Easing.CubicIn));

        await Navigation.PopModalAsync();
        _resultado.TrySetResult(true);
    }

    // Boton atras (Android): cierra el modal igual que el boton Cerrar.
    protected override bool OnBackButtonPressed()
    {
        if (_cerrando) return true;
        _ = CerrarAsync();
        return true;
    }
}
