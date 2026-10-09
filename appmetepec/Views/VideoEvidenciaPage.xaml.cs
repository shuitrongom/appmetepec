using System.Net;
using appmetepec.Models;

namespace appmetepec.Views;

// Reproductor a pantalla completa de un video de evidencia de una respuesta del ticket.
// Se abre como pagina modal desde TicketDetailPage (no se registra en Shell ni en DI).
// Usa el <video> HTML5 de un WebView en lugar de MediaElement (que truena en Android al
// conectar la superficie de video).
public partial class VideoEvidenciaPage : ContentPage
{
    // El <video> avisa sus eventos navegando a este esquema; OnNavigating los intercepta.
    private const string EsquemaAviso = "videoevidencia://";

    private readonly Uri _uri;
    private readonly bool _cerrarAlTerminar;
    private bool _fallo;

    public VideoEvidenciaPage(BackendTicketObservacionEvidenciaDto video, Uri uri)
        : this(uri, video.HasDescripcion ? video.Descripcion : null)
    {
    }

    // Tambien se usa como "pantalla completa" del video de una noticia (NewsDetailPage), con
    // cerrarAlTerminar = true: al acabar el video se cierra sola y regresa a la misma noticia.
    public VideoEvidenciaPage(Uri uri, string? descripcion = null, bool cerrarAlTerminar = false)
    {
        InitializeComponent();
        _uri = uri;
        _cerrarAlTerminar = cerrarAlTerminar;

        DescripcionLabel.Text = descripcion ?? "";
        DescripcionLabel.IsVisible = !string.IsNullOrWhiteSpace(descripcion);

        // La fuente se asigna ya con el control nativo creado, para permitir la autorreproduccion.
        Web.HandlerChanged += OnWebHandlerChanged;
    }

    private void OnWebHandlerChanged(object? sender, EventArgs e)
    {
        if (Web.Handler is null) return;
        Web.HandlerChanged -= OnWebHandlerChanged;

#if ANDROID
        if (Web.Handler.PlatformView is Android.Webkit.WebView nativo)
        {
            nativo.Settings.MediaPlaybackRequiresUserGesture = false;
            nativo.Settings.JavaScriptEnabled = true;
        }
#endif

        Web.Source = new HtmlWebViewSource { Html = CrearHtml(_uri) };
    }

    private static string CrearHtml(Uri uri)
    {
        var src = WebUtility.HtmlEncode(uri.AbsoluteUri);
        return $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1" />
              <style>
                html, body { margin: 0; height: 100%; background: #000; overflow: hidden; }
                video { width: 100%; height: 100%; object-fit: contain; background: #000; }
              </style>
            </head>
            <body>
              <video id="v" src="{{src}}" controls autoplay playsinline preload="auto"></video>
              <script>
                var v = document.getElementById('v');
                function avisar(evento) { location.href = '{{EsquemaAviso}}' + evento; }
                v.addEventListener('playing', function () { avisar('listo'); });
                v.addEventListener('loadeddata', function () { avisar('listo'); });
                v.addEventListener('error', function () { avisar('error'); });
                v.addEventListener('ended', function () { avisar('fin'); });
              </script>
            </body>
            </html>
            """;
    }

    private void OnNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (!e.Url.StartsWith(EsquemaAviso, StringComparison.OrdinalIgnoreCase)) return;
        e.Cancel = true;

        var evento = e.Url[EsquemaAviso.Length..].TrimEnd('/');
        if (evento == "listo")
        {
            Cargando.IsVisible = Cargando.IsRunning = false;
        }
        else if (evento == "error")
        {
            _ = MostrarFalloAsync();
        }
        else if (evento == "fin" && _cerrarAlTerminar)
        {
            _ = CerrarAsync();
        }
    }

    // Respaldo: si el video no manda 'listo' (p. ej. autoplay bloqueado), se quita el indicador
    // al cargar la pagina para que los controles queden visibles.
    private async void OnNavigated(object? sender, WebNavigatedEventArgs e)
    {
        await Task.Delay(1500);
        Cargando.IsVisible = Cargando.IsRunning = false;
    }

    // Formato no soportado por el telefono: se ofrece abrirlo con otra app.
    private async Task MostrarFalloAsync()
    {
        if (_fallo) return;
        _fallo = true;

        Cargando.IsVisible = Cargando.IsRunning = false;
        var abrir = await DisplayAlert("No se pudo reproducir",
            "El video no se pudo reproducir en la app. ¿Quieres abrirlo con otra aplicación?",
            "Abrir", "Cerrar");
        if (abrir)
        {
            try { await Launcher.Default.OpenAsync(_uri); } catch { /* sin app para abrirlo */ }
        }
        await CerrarAsync();
    }

    private async void OnCerrarTapped(object? sender, TappedEventArgs e) => await CerrarAsync();

    protected override bool OnBackButtonPressed()
    {
        _ = CerrarAsync();
        return true;
    }

    private async Task CerrarAsync()
    {
        if (Navigation.ModalStack.LastOrDefault() != this) return;
        await Navigation.PopModalAsync();
    }

    // Al salir: detener el video (que no siga sonando) y liberar el WebView.
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { Web.Source = new UrlWebViewSource { Url = "about:blank" }; } catch { /* best-effort */ }
    }
}
