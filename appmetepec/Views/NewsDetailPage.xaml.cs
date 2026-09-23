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
}
