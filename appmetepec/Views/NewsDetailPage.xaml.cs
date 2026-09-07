using System.Globalization;
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

    private sealed class ComentarioItem
    {
        public long Id { get; init; }
        public string NombreCiudadano { get; init; } = "Ciudadano";
        public DateTime FechaComentario { get; init; }
        public string Comentario { get; init; } = "";
        public bool EsPropio { get; init; }
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
        ContentLabel.Text = _news.content ?? _news.shortContent;
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
        CommentInputRow.IsVisible = idCiudadano > 0;
        CommentEditor.Text = string.Empty;

        _ = _api.RegistrarVistaPublicacionAsync(_news.id, idCiudadano);

        if (idCiudadano > 0)
        {
            _miReaccion = await _api.GetMiReaccionPublicacionAsync(_news.id, idCiudadano);
            ActualizarBotonesReaccion();
        }

        await CargarComentariosAsync();
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
                NombreCiudadano = string.IsNullOrWhiteSpace(c.NombreCiudadano) ? "Ciudadano" : c.NombreCiudadano,
                FechaComentario = c.FechaComentario,
                Comentario = c.Comentario,
                EsPropio = idCiudadano > 0 && c.IdCiudadano == idCiudadano
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
            await _api.ComentarPublicacionAsync(_news.id, idCiudadano, texto);
            CommentEditor.Text = string.Empty;
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
