using System.Globalization;
using System.Text.RegularExpressions;
using appmetepec.Models;
using appmetepec.Services;

namespace appmetepec.Views;

public partial class NewsDetailPage : ContentPage
{
    private static readonly CultureInfo SpanishCulture = new("es-MX");

    private readonly NavigationState _navigationState;
    private readonly MetepecApiService _api;
    private readonly PreferencesService _preferences;
    private NewsLetter? _news;
    private string? _miReaccion;
    // Comentario raiz al que se le esta redactando una respuesta (null = comentario nuevo,
    // sin padre). Se reutiliza el mismo compositor de arriba en vez de uno por comentario.
    private long? _respondiendoAId;

    private sealed class ComentarioItem
    {
        public long Id { get; init; }
        public string NombreCiudadano { get; init; } = "Ciudadano";
        public DateTime FechaComentario { get; init; }
        public string Comentario { get; init; } = "";
        public bool EsPropio { get; init; }
        public bool EsRespuestaAdmin { get; init; }
        // true para cualquier respuesta dentro de un hilo (de un ciudadano o del admin), no
        // solo las del admin -- controla la indentacion/tinte visual de "es una respuesta".
        public bool EsRespuesta { get; init; }
    }

    public NewsDetailPage(NavigationState navigationState, MetepecApiService api, PreferencesService preferences)
    {
        InitializeComponent();
        _navigationState = navigationState;
        _api = api;
        _preferences = preferences;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _news = _navigationState.SelectedNews;
        if (_news is null)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        TitleLabel.Text = _news.title;
        RenderContenido(_news.content ?? _news.shortContent);
        OpenUrlButton.IsVisible = !string.IsNullOrWhiteSpace(_news.url);

        var tieneResumen = !string.IsNullOrWhiteSpace(_news.subtitle);
        SubtitleBlock.IsVisible = tieneResumen;
        SubtitleLabel.Text = _news.subtitle;

        var textoFecha = FormatearFechaEvento(_news.fechaInicioEvento, _news.fechaFinEvento);
        EventDateRow.IsVisible = textoFecha is not null;
        EventDateLabel.Text = textoFecha;

        if (!string.IsNullOrWhiteSpace(_news.image))
        {
            NewsImage.Source = ImageSource.FromUri(new Uri(_news.image));
            NewsImage.IsVisible = true;
        }

        // Video de la noticia (opcional): si la publicacion trae videoUrl, se muestra el
        // reproductor; si no, el bloque queda oculto. En runtime se elige el reproductor segun el
        // tipo de URL: YouTube embebido (WebView con iframe) o .mp4 (MediaElement). La fuente se
        // asigna una vez.
        ConfigurarVideo();

        var idCiudadano = _preferences.CiudadanoId;
        ReactionRow.IsVisible = idCiudadano > 0;
        // Ademas de tener sesion, la publicacion debe permitir comentarios (ver
        // Publicacion.PermiteComentarios, controlado desde el panel web) -- el back-end ya
        // rechaza el intento igual, esto solo evita mostrar un compositor que va a fallar.
        CommentInputRow.IsVisible = idCiudadano > 0 && _news.permiteComentarios;
        CommentEditor.Text = string.Empty;
        CancelarRespuesta();

        _ = _api.RegistrarVistaPublicacionAsync(_news.id, idCiudadano);

        if (idCiudadano > 0)
        {
            _miReaccion = await _api.GetMiReaccionPublicacionAsync(_news.id, idCiudadano);
            ActualizarBotonesReaccion();
        }

        await CargarComentariosAsync();
    }

    // Label con TextType="Html" no soporta <img>: una imagen insertada en el editor web (aparte
    // de la imagen principal, que ya se muestra arriba via NewsImage) se veia como un cuadrito
    // vacio. Aqui se parte el HTML en segmentos de texto e imagen alternados, respetando el
    // orden original, y se arman como hijos de ContentHost (Label para texto, Image para cada
    // <img>) en vez de un unico Label.
    private static readonly Regex ImgTagRegex = new(
        "<img[^>]*\\bsrc\\s*=\\s*[\"']([^\"']+)[\"'][^>]*/?>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // URL suelta (texto plano) dentro del contenido -- ej. el admin pega un link de YouTube sin
    // usar el boton "Insertar enlace" del editor web. Label con TextType="Html" no vuelve
    // tappable un <a href> incrustado (a diferencia de un WebView), asi que cada URL detectada
    // se separa en su propio Label con TapGestureRecognizer, igual que ImgTagRegex ya separa las
    // imagenes. El "(?<![\"'])" evita casar una URL que ya viene dentro de un atributo
    // href/src (un enlace o imagen real, no texto plano).
    private static readonly Regex UrlRegex = new(
        "(?<![\"'])(https?://[^\\s<>\"']+|www\\.[^\\s<>\"']+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private void RenderContenido(string? html)
    {
        ContentHost.Children.Clear();
        if (string.IsNullOrWhiteSpace(html))
        {
            return;
        }

        var estilo = (Style)Resources["ContentSegmentLabelStyle"];
        var posicion = 0;

        foreach (Match match in ImgTagRegex.Matches(html))
        {
            AgregarSegmentoTexto(html[posicion..match.Index], estilo);
            AgregarImagen(match.Groups[1].Value);
            posicion = match.Index + match.Length;
        }

        AgregarSegmentoTexto(html[posicion..], estilo);
    }

    private void AgregarSegmentoTexto(string segmentoHtml, Style estilo)
    {
        var posicion = 0;
        foreach (Match match in UrlRegex.Matches(segmentoHtml))
        {
            AgregarTextoPlano(segmentoHtml[posicion..match.Index], estilo);
            AgregarEnlace(match.Value, estilo);
            posicion = match.Index + match.Length;
        }

        AgregarTextoPlano(segmentoHtml[posicion..], estilo);
    }

    private void AgregarTextoPlano(string segmentoHtml, Style estilo)
    {
        // Entre dos imagenes/enlaces seguidos (o al inicio/fin del contenido) puede quedar un
        // segmento que, sin las etiquetas, es puro espacio en blanco -- no vale la pena un Label
        // vacio.
        var soloTexto = Regex.Replace(segmentoHtml, "<.*?>", string.Empty);
        if (string.IsNullOrWhiteSpace(soloTexto))
        {
            return;
        }

        ContentHost.Children.Add(new Label { Text = segmentoHtml, Style = estilo });
    }

    private void AgregarEnlace(string url, Style estilo)
    {
        var href = url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : $"https://{url}";
        var label = new Label
        {
            Text = url,
            Style = estilo,
            TextColor = Color.FromArgb("#1565C0"),
            TextDecorations = TextDecorations.Underline,
        };
        label.GestureRecognizers.Add(new TapGestureRecognizer
        {
            Command = new Command(async () =>
            {
                try { await Launcher.Default.OpenAsync(href); }
                catch { /* Best-effort: si no hay app para abrir el link, no debe tronar la pantalla. */ }
            }),
        });
        ContentHost.Children.Add(label);
    }

    private void AgregarImagen(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return;
        }

        ContentHost.Children.Add(new Image
        {
            Source = ImageSource.FromUri(uri),
            Aspect = Aspect.AspectFit,
            HorizontalOptions = LayoutOptions.Fill,
        });
    }

    private async Task CargarComentariosAsync()
    {
        if (_news is null) return;

        ComentariosBusyIndicator.IsRunning = ComentariosBusyIndicator.IsVisible = true;
        try
        {
            var idCiudadano = _preferences.CiudadanoId;
            var comentarios = await _api.GetComentariosPublicacionAsync(_news.id);
            ComentariosView.ItemsSource = comentarios.Select(c => new ComentarioItem
            {
                Id = c.Id,
                NombreCiudadano = !string.IsNullOrWhiteSpace(c.NombreAutor)
                    ? c.NombreAutor
                    : (c.EsRespuestaAdmin ? "Administrador" : "Ciudadano"),
                FechaComentario = c.FechaComentario,
                Comentario = c.Comentario,
                EsPropio = idCiudadano > 0 && c.IdCiudadano == idCiudadano,
                EsRespuestaAdmin = c.EsRespuestaAdmin,
                EsRespuesta = c.IdComentarioPadre.HasValue
            }).ToList();
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudieron cargar los comentarios", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            ComentariosBusyIndicator.IsRunning = ComentariosBusyIndicator.IsVisible = false;
        }
    }

    private async void OnEnviarComentarioClicked(object sender, EventArgs e)
    {
        if (_news is null) return;
        var idCiudadano = _preferences.CiudadanoId;
        if (idCiudadano <= 0) return;

        var texto = CommentEditor.Text?.Trim();
        if (string.IsNullOrWhiteSpace(texto))
        {
            return;
        }

        ((Button)sender).IsEnabled = false;
        try
        {
            await _api.ComentarPublicacionAsync(_news.id, idCiudadano, texto, _respondiendoAId, AnonimoCheckBox.IsChecked);
            CommentEditor.Text = string.Empty;
            AnonimoCheckBox.IsChecked = false;
            CancelarRespuesta();
            await CargarComentariosAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo publicar el comentario", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
        finally
        {
            ((Button)sender).IsEnabled = true;
        }
    }

    // Icono "Responder" en un comentario raiz: reutiliza el mismo compositor de arriba en vez
    // de abrir uno por comentario, mostrando un banner de "Respondiendo a X" para dar contexto.
    private void OnResponderComentarioTapped(object sender, TappedEventArgs e)
    {
        if (_news?.permiteComentarios == false) return;
        if (sender is not Element element || element.BindingContext is not ComentarioItem item) return;

        _respondiendoAId = item.Id;
        ReplyBannerLabel.Text = $"Respondiendo a {item.NombreCiudadano}";
        ReplyBanner.IsVisible = true;
        CommentEditor.Focus();
    }

    private void OnCancelarRespuestaTapped(object sender, TappedEventArgs e) => CancelarRespuesta();

    private void CancelarRespuesta()
    {
        _respondiendoAId = null;
        ReplyBanner.IsVisible = false;
    }

    // Deslizar hacia abajo para refrescar (mismo patron que HomePage.OnRefreshing): antes solo
    // se recargaban los comentarios al entrar a la pantalla o al enviar uno propio, asi que ver
    // una respuesta nueva del admin exigia salir y volver a entrar.
    private async void OnRefreshing(object sender, EventArgs e)
    {
        if (_news is not null)
        {
            var idCiudadano = _preferences.CiudadanoId;
            if (idCiudadano > 0)
            {
                _miReaccion = await _api.GetMiReaccionPublicacionAsync(_news.id, idCiudadano);
                ActualizarBotonesReaccion();
            }
        }

        await CargarComentariosAsync();
        ((RefreshView)sender).IsRefreshing = false;
    }

    private async void OnEliminarComentarioTapped(object sender, TappedEventArgs e)
    {
        if (sender is not Element element || element.BindingContext is not ComentarioItem item) return;
        var idCiudadano = _preferences.CiudadanoId;
        if (idCiudadano <= 0) return;

        var confirmar = await DisplayAlert("Eliminar comentario", "¿Quieres eliminar este comentario?", "Eliminar", "Cancelar");
        if (!confirmar) return;

        try
        {
            await _api.EliminarComentarioPublicacionAsync(item.Id, idCiudadano);
            await CargarComentariosAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("No se pudo eliminar el comentario", ErrorMessageHelper.Traducir(ex), "Aceptar");
        }
    }

    // Alternar: tocar el mismo tipo que ya estaba activo quita la reaccion; tocar el otro la
    // reemplaza (solo se puede tener una reaccion activa por publicacion, ver PublicacionReaccion).
    private async Task ReaccionarAsync(string tipo)
    {
        if (_news is null) return;
        var idCiudadano = _preferences.CiudadanoId;
        if (idCiudadano <= 0) return;

        LikeButton.IsEnabled = false;
        DislikeButton.IsEnabled = false;

        try
        {
            if (string.Equals(_miReaccion, tipo, StringComparison.OrdinalIgnoreCase))
            {
                await _api.QuitarReaccionPublicacionAsync(_news.id, idCiudadano);
                _miReaccion = null;
            }
            else
            {
                _miReaccion = await _api.ReaccionarPublicacionAsync(_news.id, idCiudadano, tipo);
            }
        }
        finally
        {
            ActualizarBotonesReaccion();
            LikeButton.IsEnabled = true;
            DislikeButton.IsEnabled = true;
        }
    }

    private void ActualizarBotonesReaccion()
    {
        var esLike = string.Equals(_miReaccion, "Like", StringComparison.OrdinalIgnoreCase);
        var esDislike = string.Equals(_miReaccion, "Dislike", StringComparison.OrdinalIgnoreCase);

        LikeButton.TextColor = esLike ? Colors.White : Color.FromArgb("#28113E");
        LikeButton.BackgroundColor = esLike ? Color.FromArgb("#F89A1C") : Color.FromArgb("#F1EEFB");
        DislikeButton.TextColor = esDislike ? Colors.White : Color.FromArgb("#28113E");
        DislikeButton.BackgroundColor = esDislike ? Color.FromArgb("#F89A1C") : Color.FromArgb("#F1EEFB");
    }

    private async void OnLikeTapped(object sender, EventArgs e) => await ReaccionarAsync("Like");

    private async void OnDislikeTapped(object sender, EventArgs e) => await ReaccionarAsync("Dislike");

    // FechaInicioEvento/FechaFinEvento son las fechas del evento que anuncia la publicacion
    // (no la fecha de publicacion). Si ambas existen y difieren se muestra como rango.
    private static string? FormatearFechaEvento(DateTime? inicio, DateTime? fin)
    {
        if (inicio is null) return null;

        var textoInicio = inicio.Value.ToString("d 'de' MMMM, yyyy", SpanishCulture);
        if (fin is null || fin.Value.Date == inicio.Value.Date)
        {
            return textoInicio;
        }

        var textoFin = fin.Value.ToString("d 'de' MMMM, yyyy", SpanishCulture);
        return $"Del {textoInicio} al {textoFin}";
    }

    private async void OnOpenUrlClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_news?.url))
        {
            await Launcher.Default.OpenAsync(_news.url);
        }
    }

    private async void OnBackTapped(object sender, TappedEventArgs e)
    {
        await Shell.Current.GoToAsync("..");
    }

    // --- Video: eleccion de reproductor en runtime (YouTube embebido vs .mp4) ---

    // Casa los formatos de URL de YouTube de los que se puede sacar el VIDEO_ID (11 chars):
    // youtube.com/watch?v=ID, youtu.be/ID, youtube.com/shorts/ID, youtube.com/embed/ID.
    private static readonly Regex YoutubeIdRegex = new(
        "(?:youtube\\.com/(?:watch\\?(?:[^&]*&)*v=|shorts/|embed/|v/)|youtu\\.be/)([A-Za-z0-9_-]{11})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Dominio de YouTube (youtube.com o youtu.be, con o sin www/http/https), sin exigir un
    // VIDEO_ID valido. Sirve para distinguir "esto es un enlace de YouTube que no supimos parsear"
    // (ocultar bloque, no mandarlo al reproductor .mp4) de "esto es otra cosa" (camino .mp4).
    private static readonly Regex YoutubeDominioRegex = new(
        "(?:^|//|\\.)(?:youtube\\.com|youtu\\.be)/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // true si la URL es de YouTube Y se le pudo extraer un VIDEO_ID valido (unico caso en que el
    // embed sirve). Para "parece YouTube pero no parsea" usar EsDominioYoutube.
    private static bool EsYoutube(string? url) =>
        !string.IsNullOrWhiteSpace(url) && ExtraerVideoId(url) is not null;

    private static bool EsDominioYoutube(string? url) =>
        !string.IsNullOrWhiteSpace(url) && YoutubeDominioRegex.IsMatch(url);

    // Devuelve el VIDEO_ID (11 chars) si la URL es de YouTube, o null si no lo es / no se pudo
    // extraer. Robusto frente a parametros extra (?t=, &list=, etc.).
    private static string? ExtraerVideoId(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var match = YoutubeIdRegex.Match(url);
        return match.Success ? match.Groups[1].Value : null;
    }

    // Decide en runtime que reproductor mostrar en el detalle de la noticia:
    //  - Sin videoUrl        -> no se muestra nada (bloque oculto).
    //  - videoUrl de YouTube -> WebView con iframe embebido (NO abre navegador externo); el
    //                           MediaElement inline queda oculto y SIN Source. El boton de
    //                           pantalla completa propio de la app se oculta porque el iframe de
    //                           YouTube ya trae el suyo (reutilizar el overlay con WebView es
    //                           fragil, por eso para YouTube solo se ofrece el inline embebido).
    //  - cualquier otra URL  -> camino actual .mp4 con MediaElement (inline + pantalla completa).
    private void ConfigurarVideo()
    {
        if (_news is null || string.IsNullOrWhiteSpace(_news.videoUrl))
        {
            VideoBlock.IsVisible = false;
            return;
        }

        var videoId = ExtraerVideoId(_news.videoUrl);
        if (videoId is not null)
        {
            // YouTube embebido: ocultar el MediaElement (sin asignarle Source) y mostrar el WebView.
            VideoInline.IsVisible = false;
            VideoFullscreenButton.IsVisible = false;
            VideoYoutube.IsVisible = true;
            VideoYoutube.Source = ConstruirHtmlYoutube(videoId);
            VideoBlock.IsVisible = true;
        }
        else if (EsDominioYoutube(_news.videoUrl))
        {
            // Es un enlace de YouTube pero NO se pudo sacar un VIDEO_ID valido (URL rara). Antes de
            // mostrar un recuadro negro vacio (WebView con un embed invalido), se oculta el bloque
            // de video por completo: mejor no mostrar nada que un cuadro negro muerto.
            VideoBlock.IsVisible = false;
        }
        else if (Uri.TryCreate(_news.videoUrl, UriKind.Absolute, out var videoUri))
        {
            // .mp4 u otra URL de video directa: camino actual con MediaElement.
            VideoYoutube.IsVisible = false;
            VideoInline.IsVisible = true;
            VideoFullscreenButton.IsVisible = true;
            VideoInline.Source = CommunityToolkit.Maui.Views.MediaSource.FromUri(videoUri);
            VideoBlock.IsVisible = true;
        }
        else
        {
            VideoBlock.IsVisible = false;
        }
    }

    // HTML del reproductor de YouTube embebido.
    //
    // CAUSA RAIZ del "recuadro negro total" en versiones anteriores: el HTML envolvia el iframe en
    // un contenedor con alto por "padding-top:56.25%" (truco 16:9 basado en el ANCHO). Dentro de un
    // WebView de MAUI en Android, en el primer render el ancho de referencia del <body> no esta
    // resuelto, el padding-top en % calcula 0 y la caja COLAPSA a altura 0 -> no se pinta nada
    // (negro). El poster como background-image sobre esa caja colapsada tampoco se veia.
    //
    // FIX de raiz: el WebView ya tiene una altura real y fija en XAML (HeightRequest=210). Aqui el
    // html/body ocupan el 100% de ESA altura (height:100%, no un % del ancho) y el iframe se ancla
    // con position:absolute; inset:0 para llenar TODO el WebView. Asi SIEMPRE hay una caja con alto
    // real donde pintar. Se deja que el propio reproductor de YouTube muestre su poster + boton de
    // play grande + controles (es lo mas robusto y lo que el usuario espera ver), sin autoplay
    // forzado: el usuario pulsa play sobre el reproductor de YouTube.
    //
    // La URL de embed es CANONICA y solo con el ID limpio de 11 chars:
    //   https://www.youtube.com/embed/VIDEO_ID?playsinline=1&rel=0&modestbranding=1
    // Funciona igual para videos normales y para SHORTS (el embed por ID es el mismo).
    //
    // BaseUrl = https://www.youtube.com es CLAVE: sin el, el HTML inline se carga con origen
    // "about:blank"/local y el reproductor de YouTube bloquea el embed. Con un BaseUrl del mismo
    // dominio que el embed, el documento tiene un origin valido y YouTube permite la reproduccion.
    private static HtmlWebViewSource ConstruirHtmlYoutube(string videoId)
    {
        var embedSrc = $"https://www.youtube.com/embed/{videoId}?playsinline=1&rel=0&modestbranding=1";

        var html =
            "<!DOCTYPE html>" +
            "<html><head><meta name='viewport' content='width=device-width, initial-scale=1'>" +
            "<style>" +
            // html/body con alto real (100% del alto del WebView, que es fijo). Sin esto, el
            // documento no tiene altura de referencia y el embed queda sin lugar donde pintar.
            "html,body{margin:0;padding:0;height:100%;width:100%;background:#000;overflow:hidden;}" +
            // El iframe llena TODO el WebView con posicion absoluta (no depende de un % del ancho).
            ".box{position:absolute;top:0;left:0;right:0;bottom:0;}" +
            ".box iframe{width:100%;height:100%;border:0;display:block;}" +
            "</style></head>" +
            "<body><div class='box'>" +
            $"<iframe src='{embedSrc}' " +
            "allow='accelerometer; autoplay; clipboard-write; encrypted-media; gyroscope; picture-in-picture; web-share' " +
            "allowfullscreen></iframe>" +
            "</div></body></html>";
        return new HtmlWebViewSource { Html = html, BaseUrl = "https://www.youtube.com" };
    }

    // --- Video: expandir a pantalla completa y volver (solo camino .mp4 / MediaElement) ---

    // "Pantalla completa": pausa el reproductor inline, carga la misma fuente en el overlay a
    // pantalla completa y lo reproduce. Suscribe MediaEnded para que, al terminar, el overlay se
    // cierre solo y el usuario regrese a la misma noticia (no al Home). No aplica a YouTube: ese
    // boton se oculta en ConfigurarVideo cuando el video es de YouTube.
    private void OnExpandirVideoTapped(object sender, TappedEventArgs e)
    {
        if (_news is null || string.IsNullOrWhiteSpace(_news.videoUrl)
            || EsYoutube(_news.videoUrl)
            || !Uri.TryCreate(_news.videoUrl, UriKind.Absolute, out var videoUri))
        {
            return;
        }

        try { VideoInline.Pause(); } catch { /* best-effort */ }

        VideoFull.Source = CommunityToolkit.Maui.Views.MediaSource.FromUri(videoUri);
        VideoFull.MediaEnded += OnVideoFullEnded;
        VideoFullscreenOverlay.IsVisible = true;
        try { VideoFull.Play(); } catch { /* el control autoreproduce igual (ShouldAutoPlay) */ }
    }

    // Al terminar el video en pantalla completa: cerrar el overlay y volver a la noticia.
    private void OnVideoFullEnded(object? sender, EventArgs e)
    {
        CerrarVideoFull();
    }

    private void OnCerrarVideoFullTapped(object sender, TappedEventArgs e)
    {
        CerrarVideoFull();
    }

    // Cierra el overlay de pantalla completa, detiene y libera el video grande, y deja la noticia
    // tal cual (el reproductor inline sigue disponible para volver a ver).
    private void CerrarVideoFull()
    {
        VideoFull.MediaEnded -= OnVideoFullEnded;
        try { VideoFull.Stop(); } catch { /* best-effort */ }
        VideoFull.Source = null;
        VideoFullscreenOverlay.IsVisible = false;
    }

    // Al salir de la pagina: detener y liberar ambos reproductores (bateria y memoria).
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        try { VideoInline.Stop(); } catch { /* best-effort */ }
        // YouTube: vaciar el WebView para que el iframe deje de reproducir/sonar al volver (de lo
        // contrario el audio de YouTube seguiria aun fuera de la pantalla).
        try { VideoYoutube.Source = new HtmlWebViewSource { Html = "<html></html>" }; } catch { /* best-effort */ }
        if (VideoFullscreenOverlay.IsVisible)
        {
            CerrarVideoFull();
        }
    }
}
