using Microsoft.Maui.Devices;

namespace appmetepec.Views;

// Dialogo de confirmacion con estilo de la app (alternativa a DisplayAlert): icono en circulo,
// titulo, mensaje, dato destacado opcional, nota y botones de color (principal relleno,
// secundario con contorno). Mismo patron de modal que ActividadDetallePage: se abre con
// MostrarAsync (new + PushModalAsync + TaskCompletionSource), no se registra en Shell.
// Devuelve true si se elige el boton principal; false con el secundario o el boton atras.
public partial class DialogoPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _resultado = new();
    private bool _cerrando;

    private DialogoPage(string titulo, string mensaje, string botonPrincipal, string? botonSecundario,
        string? destacado, string? nota, string icono, Color acento, Color acentoTenue)
    {
        InitializeComponent();

        TituloLabel.Text = titulo;
        MensajeLabel.Text = mensaje;

        DestacadoLabel.Text = destacado ?? "";
        DestacadoContenedor.IsVisible = !string.IsNullOrWhiteSpace(destacado);

        NotaLabel.Text = nota ?? "";
        NotaLabel.IsVisible = !string.IsNullOrWhiteSpace(nota);

        IconoLabel.Text = icono;
        IconoFondo.BackgroundColor = acento;
        IconoHalo.BackgroundColor = acentoTenue;

        PrincipalButton.Text = botonPrincipal;
        PrincipalButton.BackgroundColor = acento;
        PrincipalSombra.Brush = new SolidColorBrush(acento);
        SecundarioButton.Text = botonSecundario ?? "";
        SecundarioButton.IsVisible = !string.IsNullOrWhiteSpace(botonSecundario);

        // Ancho responsive: ~90% de la pantalla con tope de 420 DIP.
        var info = DeviceDisplay.Current.MainDisplayInfo;
        var anchoDip = info.Density > 0 ? info.Width / info.Density : 360;
        Tarjeta.WidthRequest = Math.Min(anchoDip * 0.90, 420);
    }

    // Acentos predefinidos: naranja institucional (pregunta/accion), rojo (aviso/bloqueo) y verde
    // (exito).
    public static readonly (Color Acento, Color Tenue) Naranja = (Color.FromArgb("#F89A1C"), Color.FromArgb("#FDEBD3"));
    public static readonly (Color Acento, Color Tenue) Rojo = (Color.FromArgb("#C0392B"), Color.FromArgb("#F8DEDB"));
    public static readonly (Color Acento, Color Tenue) Verde = (Color.FromArgb("#1E9E5A"), Color.FromArgb("#D8F2E4"));

    public static async Task<bool> MostrarAsync(INavigation navigation, string titulo, string mensaje,
        string botonPrincipal, string? botonSecundario = null, string? destacado = null, string? nota = null,
        string icono = "?", (Color Acento, Color Tenue)? colores = null)
    {
        var (acento, tenue) = colores ?? Naranja;
        var page = new DialogoPage(titulo, mensaje, botonPrincipal, botonSecundario, destacado, nota, icono, acento, tenue);
        await navigation.PushModalAsync(page, false);
        return await page._resultado.Task;
    }

    // Entrada: el scrim aparece y la tarjeta crece suave (0.92 -> 1).
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

    private async void OnPrincipalClicked(object sender, EventArgs e) => await CerrarAsync(true);
    private async void OnSecundarioClicked(object sender, EventArgs e) => await CerrarAsync(false);

    // Salida con animacion inversa y luego PopModalAsync; el resultado se entrega ya cerrado para
    // que quien espera pueda navegar sin chocar con el modal. Protegido contra dobles toques.
    private async Task CerrarAsync(bool resultado)
    {
        if (_cerrando) return;
        _cerrando = true;

        await Task.WhenAll(
            Tarjeta.ScaleTo(0.92, 150, Easing.CubicIn),
            Tarjeta.FadeTo(0, 150, Easing.CubicIn),
            this.FadeTo(0, 150, Easing.CubicIn));

        await Navigation.PopModalAsync(false);
        _resultado.TrySetResult(resultado);
    }

    // Boton atras (Android): equivale al boton secundario.
    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync(false);
        return true;
    }
}
