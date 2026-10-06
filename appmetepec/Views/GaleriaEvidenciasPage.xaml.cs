using appmetepec.Models;

namespace appmetepec.Views;

// Visor a pantalla completa de las fotos de evidencia de una respuesta del ticket.
// Se abre como pagina modal desde TicketDetailPage (no se registra en Shell ni en DI).
public partial class GaleriaEvidenciasPage : ContentPage
{
    private readonly IReadOnlyList<BackendTicketObservacionEvidenciaDto> _fotos;
    private readonly int _indiceInicial;
    private bool _primeraVez = true;

    public GaleriaEvidenciasPage(IReadOnlyList<BackendTicketObservacionEvidenciaDto> fotos, int indiceInicial)
    {
        InitializeComponent();
        _fotos = fotos;
        _indiceInicial = Math.Clamp(indiceInicial, 0, Math.Max(fotos.Count - 1, 0));

        Carrusel.ItemsSource = _fotos;
        Indicadores.IsVisible = _fotos.Count > 1;
        ActualizarTextos(_indiceInicial);
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (!_primeraVez) return;
        _primeraVez = false;

        // En Android el CarouselView ignora Position si se asigna antes de medirse.
        Carrusel.ScrollTo(_indiceInicial, animate: false);
        Carrusel.Position = _indiceInicial;
        await Carrusel.FadeTo(1, 220, Easing.CubicOut);
    }

    private void OnPositionChanged(object? sender, PositionChangedEventArgs e) => ActualizarTextos(e.CurrentPosition);

    private void ActualizarTextos(int indice)
    {
        if (indice < 0 || indice >= _fotos.Count) return;

        ContadorLabel.Text = _fotos.Count > 1 ? $"{indice + 1} / {_fotos.Count}" : "";
        DescripcionLabel.Text = _fotos[indice].Descripcion ?? "";
        DescripcionLabel.IsVisible = _fotos[indice].HasDescripcion;
    }

    // Con zoom: se bloquea el deslizamiento entre fotos y se ocultan las barras para ver la imagen completa.
    private void OnZoomChanged(object? sender, bool ampliada)
    {
        Carrusel.IsSwipeEnabled = !ampliada;
        _ = BarraSuperior.FadeTo(ampliada ? 0 : 1, 150);
        _ = Pie.FadeTo(ampliada ? 0 : 1, 150);
        BarraSuperior.InputTransparent = ampliada;
    }

    private async void OnCerrarTapped(object? sender, TappedEventArgs e) => await Navigation.PopModalAsync();
}
